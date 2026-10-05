import { z } from "zod";
import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import { activationView, attemptView } from "@/lib/views";
import { isRecoverable, oid } from "../../_lib/keys";
import type { LicenseKeyDoc, ActivationDoc, ActivationAttemptDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// GET /api/admin/keys/:id — full detail: the device holding it, the person who
// registered, every past device, and every refused attempt.
export async function GET(_req: Request, { params }: { params: { id: string } }) {
  if (!(await getSession())) return fail("unauthorized", 401);
  const _id = oid(params.id);
  if (!_id) return fail("not_found", 404);

  const db = await getDb();
  const key = await db.collection<LicenseKeyDoc>("licenseKeys").findOne({ _id });
  if (!key) return fail("not_found", 404);

  const [activations, attempts] = await Promise.all([
    db.collection<ActivationDoc>("activations").find({ keyId: _id }).sort({ activatedUtc: -1 }).toArray(),
    db.collection<ActivationAttemptDoc>("activationAttempts").find({ keyId: _id }).sort({ createdUtc: -1 }).limit(200).toArray(),
  ]);

  const now = new Date();
  const holder = activations.find((a) => a.status === "active") ?? null;
  return ok({
    id: key._id!.toHexString(),
    prefix: key.keyPrefix,
    /** POST …/reveal can show the full key; false for keys issued before keys were sealed. */
    recoverable: isRecoverable(key),
    reissuedUtc: key.reissuedUtc ? key.reissuedUtc.toISOString() : null,
    reissuedBy: key.reissuedBy ?? "",
    serial: key.serial ?? null,
    seq: key.seq ?? null,
    edition: key.edition,
    status: key.status,
    expired: !!key.expiryUtc && now > key.expiryUtc,
    expiryUtc: key.expiryUtc ? key.expiryUtc.toISOString() : null,
    customerName: key.customerName ?? "",
    customerEmail: key.customerEmail ?? "",
    note: key.note ?? "",
    createdBy: key.createdBy,
    createdUtc: key.createdUtc.toISOString(),
    lastSeenUtc: key.lastSeenUtc ? key.lastSeenUtc.toISOString() : null,
    device: holder ? activationView(holder) : null,
    history: activations.filter((a) => a.status !== "active").map(activationView),
    attempts: attempts.map(attemptView),
  });
}

const PatchBody = z.object({
  action: z.enum(["revoke", "unrevoke", "extend", "setExpiry", "releaseDevice"]),
  days: z.number().int().min(1).max(3650).optional(),
  expiryUtc: z.string().datetime().nullable().optional(),
  reason: z.string().max(200).optional(),
});

// PATCH /api/admin/keys/:id — revoke / extend / move to another device.
export async function PATCH(req: Request, { params }: { params: { id: string } }) {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);
  const _id = oid(params.id);
  if (!_id) return fail("not_found", 404);

  let b;
  try {
    b = PatchBody.parse(await req.json());
  } catch {
    return fail("bad_request", 400);
  }

  const db = await getDb();
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");
  const key = await keys.findOne({ _id });
  if (!key) return fail("not_found", 404);

  switch (b.action) {
    case "revoke":
      await keys.updateOne({ _id }, { $set: { status: "revoked" } });
      break;
    case "unrevoke":
      await keys.updateOne({ _id }, { $set: { status: "active" } });
      break;
    case "extend": {
      if (!b.days) return fail("bad_request", 400);
      const base = key.expiryUtc && key.expiryUtc > new Date() ? key.expiryUtc : new Date();
      const next = new Date(base);
      next.setUTCDate(next.getUTCDate() + b.days);
      await keys.updateOne({ _id }, { $set: { expiryUtc: next } });
      break;
    }
    case "setExpiry":
      await keys.updateOne({ _id }, { $set: { expiryUtc: b.expiryUtc ? new Date(b.expiryUtc) : null } });
      break;
    case "releaseDevice": {
      // "Move to another device": the current PC loses the key at its next
      // check-in and the next PC to activate takes it. Only an admin can do this.
      const acts = db.collection<ActivationDoc>("activations");
      const res = await acts.updateOne(
        { keyId: _id, status: "active" },
        {
          $set: {
            status: "released",
            releasedUtc: new Date(),
            releasedBy: session.email,
            releaseReason: b.reason?.trim() || "Moved to another device by support",
          },
        },
      );
      if (res.matchedCount === 0) return fail("not_bound", 409);
      break;
    }
  }

  await audit(session.email, `key.${b.action}`, `${key.serial} #${key.seq}`, { ...b });
  return ok({ status: "updated" });
}

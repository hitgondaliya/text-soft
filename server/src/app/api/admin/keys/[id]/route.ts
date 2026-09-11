import { z } from "zod";
import { ObjectId } from "mongodb";
import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import type { LicenseKeyDoc, ActivationDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

function oid(id: string): ObjectId | null {
  try {
    return new ObjectId(id);
  } catch {
    return null;
  }
}

// GET /api/admin/keys/:id — full detail including per-device activations.
export async function GET(_req: Request, { params }: { params: { id: string } }) {
  if (!(await getSession())) return fail("unauthorized", 401);
  const _id = oid(params.id);
  if (!_id) return fail("not_found", 404);

  const db = await getDb();
  const key = await db.collection<LicenseKeyDoc>("licenseKeys").findOne({ _id });
  if (!key) return fail("not_found", 404);

  const activations = await db
    .collection<ActivationDoc>("activations")
    .find({ keyId: _id })
    .sort({ activatedUtc: -1 })
    .toArray();

  const now = new Date();
  return ok({
    id: key._id!.toHexString(),
    prefix: key.keyPrefix,
    edition: key.edition,
    status: key.status,
    expired: !!key.expiryUtc && now > key.expiryUtc,
    expiryUtc: key.expiryUtc ? key.expiryUtc.toISOString() : null,
    deviceLimit: key.deviceLimit,
    customerName: key.customerName ?? "",
    customerEmail: key.customerEmail ?? "",
    note: key.note ?? "",
    createdBy: key.createdBy,
    createdUtc: key.createdUtc.toISOString(),
    lastSeenUtc: key.lastSeenUtc ? key.lastSeenUtc.toISOString() : null,
    activations: activations.map((a) => ({
      id: a._id!.toHexString(),
      deviceFp: a.deviceFp,
      deviceName: a.deviceName ?? "",
      ip: a.ip ?? "",
      appVersion: a.appVersion ?? "",
      status: a.status,
      activatedUtc: a.activatedUtc.toISOString(),
      lastSeenUtc: a.lastSeenUtc.toISOString(),
    })),
  });
}

const PatchBody = z.object({
  action: z.enum(["revoke", "unrevoke", "extend", "setExpiry", "setDeviceLimit", "deactivateDevice"]),
  days: z.number().int().min(1).max(3650).optional(),
  expiryUtc: z.string().datetime().nullable().optional(),
  deviceLimit: z.number().int().min(1).max(100).optional(),
  deviceFp: z.string().optional(),
});

// PATCH /api/admin/keys/:id — revoke / extend / change limit / free a device.
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
      await keys.updateOne(
        { _id },
        { $set: { expiryUtc: b.expiryUtc ? new Date(b.expiryUtc) : null } },
      );
      break;
    case "setDeviceLimit":
      if (!b.deviceLimit) return fail("bad_request", 400);
      await keys.updateOne({ _id }, { $set: { deviceLimit: b.deviceLimit } });
      break;
    case "deactivateDevice": {
      if (!b.deviceFp) return fail("bad_request", 400);
      await db
        .collection<ActivationDoc>("activations")
        .updateOne({ keyId: _id, deviceFp: b.deviceFp }, { $set: { status: "deactivated" } });
      break;
    }
  }

  await audit(session.email, `key.${b.action}`, key.keyPrefix, { ...b });
  return ok({ status: "updated" });
}

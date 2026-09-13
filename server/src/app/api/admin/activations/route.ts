import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { ok, fail } from "@/lib/http";
import { activationView } from "@/lib/views";
import type { ActivationDoc, LicenseKeyDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// GET /api/admin/activations — the end users: every device that holds a key,
// with the person who registered on it.
//   ?q=          name / email / phone / device / serial
//   ?status=     active (default) | released | all
//   ?registered= yes | no
export async function GET(req: Request) {
  if (!(await getSession())) return fail("unauthorized", 401);
  const url = new URL(req.url);
  const q = url.searchParams.get("q")?.trim();
  const status = url.searchParams.get("status") ?? "active";
  const registered = url.searchParams.get("registered");

  const filter: Record<string, unknown> = {};
  if (status !== "all") filter.status = status;
  if (registered === "yes") filter.user = { $exists: true };
  if (registered === "no") filter.user = { $exists: false };
  if (q) {
    const digits = q.replace(/[^\d]/g, "");
    filter.$or = [
      { "user.name": { $regex: q, $options: "i" } },
      { "user.email": { $regex: q, $options: "i" } },
      ...(digits ? [{ "user.phoneE164": { $regex: digits } }] : []),
      { deviceName: { $regex: q, $options: "i" } },
      { serial: { $regex: q, $options: "i" } },
    ];
  }

  const db = await getDb();
  const rows = await db
    .collection<ActivationDoc>("activations")
    .find(filter)
    .sort({ activatedUtc: -1 })
    .limit(500)
    .toArray();

  const keyIds = Array.from(new Set(rows.map((a) => a.keyId.toHexString())));
  const keys = await db
    .collection<LicenseKeyDoc>("licenseKeys")
    .find({ _id: { $in: rows.map((a) => a.keyId) } })
    .toArray();
  const keyBy = new Map(keys.map((k) => [k._id!.toHexString(), k]));
  void keyIds;

  const now = new Date();
  return ok({
    activations: rows.map((a) => {
      const k = keyBy.get(a.keyId.toHexString());
      return {
        ...activationView(a),
        keyPrefix: k?.keyPrefix ?? "",
        seq: k?.seq ?? null,
        edition: k?.edition ?? "",
        keyStatus: k?.status ?? "",
        expired: !!k?.expiryUtc && now > k.expiryUtc,
        expiryUtc: k?.expiryUtc ? k.expiryUtc.toISOString() : null,
        customerName: k?.customerName ?? "",
      };
    }),
  });
}

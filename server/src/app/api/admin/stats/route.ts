import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { ok, fail } from "@/lib/http";
import type { LicenseKeyDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// GET /api/admin/stats — dashboard totals.
export async function GET() {
  if (!(await getSession())) return fail("unauthorized", 401);

  const db = await getDb();
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");
  const now = new Date();
  const soon = new Date();
  soon.setUTCDate(soon.getUTCDate() + 7);

  const [total, revoked, active, expiringSoon, activations] = await Promise.all([
    keys.countDocuments({}),
    keys.countDocuments({ status: "revoked" }),
    keys.countDocuments({ status: "active", $or: [{ expiryUtc: null }, { expiryUtc: { $gt: now } }] }),
    keys.countDocuments({ status: "active", expiryUtc: { $gt: now, $lte: soon } }),
    db.collection("activations").countDocuments({ status: "active" }),
  ]);

  const recent = await keys.find({}).sort({ lastSeenUtc: -1 }).limit(8).toArray();

  return ok({
    total,
    active,
    revoked,
    expiringSoon,
    activations,
    recent: recent
      .filter((k) => k.lastSeenUtc)
      .map((k) => ({
        id: k._id!.toHexString(),
        prefix: k.keyPrefix,
        edition: k.edition,
        lastSeenUtc: k.lastSeenUtc!.toISOString(),
      })),
  });
}

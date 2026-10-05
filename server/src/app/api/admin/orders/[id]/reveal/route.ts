import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import { limitReached, rateLimited, LIMITS } from "@/lib/ratelimit";
import { findOrder, revealKey } from "../../../_lib/keys";
import type { LicenseKeyDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// POST /api/admin/orders/:idOrSerial/reveal — every key of the purchase in full,
// for "copy all". Keys issued before keys were sealed come back as key: null and
// are counted in `missing`. Each key carries its current status and whether it has
// expired, so the page leaves out revoked keys even if it loaded them before a
// revoke in another tab. Audited once per call (the keys themselves never are).
// Each revealed key counts against the admin's hourly reveal allowance.
//   200 { serial, keys: [{ id, seq, prefix, key | null, status, expired }], missing }
//   404 not_found · 429 rate_limited
export async function POST(_req: Request, { params }: { params: { id: string } }) {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);

  const bucket = `reveal:${session.email}`;
  if (limitReached(bucket, LIMITS.revealPerAdmin.limit, LIMITS.revealPerAdmin.windowMs)) {
    await audit(session.email, "order.reveal_refused", params.id, { reason: "rate_limited" });
    return fail("rate_limited", 429);
  }

  const order = await findOrder(params.id);
  if (!order) return fail("not_found", 404);

  const db = await getDb();
  const rows = await db.collection<LicenseKeyDoc>("licenseKeys").find({ orderId: order._id! }).sort({ seq: 1 }).toArray();

  const now = new Date();
  const keys = rows.map((k) => ({
    id: k._id!.toHexString(),
    seq: k.seq,
    prefix: k.keyPrefix,
    key: revealKey(k),
    status: k.status,
    expired: !!k.expiryUtc && now > k.expiryUtc,
  }));
  const missing = keys.filter((k) => k.key === null).length;
  const revealed = keys.length - missing;
  if (revealed) rateLimited(bucket, LIMITS.revealPerAdmin.limit, LIMITS.revealPerAdmin.windowMs, revealed);

  await audit(session.email, "order.reveal", order.serial, { revealed, missing });
  return ok({ serial: order.serial, keys, missing });
}

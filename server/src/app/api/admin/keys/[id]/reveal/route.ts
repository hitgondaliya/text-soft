import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import { limitReached, recordHit, LIMITS } from "@/lib/ratelimit";
import { oid, revealKey } from "../../../_lib/keys";
import type { LicenseKeyDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// POST /api/admin/keys/:id/reveal — the full key string again, to copy it for a
// customer who lost the email. POST (not GET) so it is never cached or prefetched,
// and every reveal is written to the audit log (never the key itself). Counts
// against the admin's hourly reveal allowance, so a session cannot dump every key.
//   200 { key, serial, seq } · 404 not_found · 409 not_recoverable (issued before keys were sealed)
//   429 rate_limited
export async function POST(_req: Request, { params }: { params: { id: string } }) {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);
  const _id = oid(params.id);
  if (!_id) return fail("not_found", 404);

  const bucket = `reveal:${session.email}`;
  if (limitReached(bucket, LIMITS.revealPerAdmin.limit, LIMITS.revealPerAdmin.windowMs)) {
    await audit(session.email, "key.reveal_refused", params.id, { reason: "rate_limited" });
    return fail("rate_limited", 429);
  }

  const db = await getDb();
  const key = await db.collection<LicenseKeyDoc>("licenseKeys").findOne({ _id });
  if (!key) return fail("not_found", 404);

  const plain = revealKey(key);
  if (!plain) return fail("not_recoverable", 409);

  recordHit(bucket);
  await audit(session.email, "key.reveal", `${key.serial} #${key.seq}`, { keyId: params.id, prefix: key.keyPrefix });
  return ok({ key: plain, serial: key.serial, seq: key.seq });
}

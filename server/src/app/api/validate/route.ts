import { z } from "zod";
import { ObjectId } from "mongodb";
import { getDb } from "@/lib/mongo";
import { issueToken, readToken } from "@/lib/token";
import { ok, fail, clientIp } from "@/lib/http";
import { formatPhone } from "@/lib/phone";
import { rateLimited, LIMITS } from "@/lib/ratelimit";
import type { LicenseKeyDoc, ActivationDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const Body = z.object({
  token: z.string().min(16),
  appVersion: z.string().max(64).optional(),
});

// POST /api/validate — heartbeat. The DB is the source of truth: confirm the key
// is still active/unexpired and the device still holds it, then re-issue a fresh
// token carrying the authoritative server time (for the client's clock guard).
export async function POST(req: Request) {
  const ip = clientIp(req);
  if (rateLimited(`val:ip:${ip}`, LIMITS.heartbeatPerIp.limit, LIMITS.heartbeatPerIp.windowMs)) return fail("rate_limited", 429);

  let parsed;
  try {
    parsed = Body.parse(await req.json());
  } catch {
    return fail("bad_request", 400);
  }

  const payload = readToken(parsed.token);
  if (!payload) return fail("invalid", 400);

  let keyOid: ObjectId;
  try {
    keyOid = new ObjectId(payload.keyId);
  } catch {
    return fail("invalid", 400);
  }

  const db = await getDb();
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");
  const acts = db.collection<ActivationDoc>("activations");

  const keyDoc = await keys.findOne({ _id: keyOid });
  if (!keyDoc) return fail("invalid", 404);
  if (keyDoc.status === "revoked") return fail("revoked", 403);

  const now = new Date();
  if (keyDoc.expiryUtc && now > keyDoc.expiryUtc) return fail("expired", 403);

  const act = await acts.findOne({ keyId: keyOid, deviceFp: payload.deviceFp });
  if (!act) return fail("released", 403);
  if (act.status !== "active") return fail("released", 403, { reason: act.status });

  await acts.updateOne(
    { _id: act._id },
    { $set: { lastSeenUtc: now, ip, appVersion: parsed.appVersion ?? act.appVersion } },
  );
  await keys.updateOne({ _id: keyOid }, { $set: { lastSeenUtc: now } });

  return ok({
    token: issueToken(keyDoc, act, now),
    serial: keyDoc.serial,
    edition: keyDoc.edition,
    expiryUtc: keyDoc.expiryUtc ? keyDoc.expiryUtc.toISOString() : null,
    registered: !!act.user,
    user: act.user ? { name: act.user.name, email: act.user.email, phone: formatPhone(act.user.phoneE164) } : null,
    serverTimeUtc: now.toISOString(),
  });
}

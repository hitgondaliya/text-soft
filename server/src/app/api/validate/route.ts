import { z } from "zod";
import crypto from "node:crypto";
import { ObjectId } from "mongodb";
import { getDb } from "@/lib/mongo";
import { makeToken, readToken } from "@/lib/token";
import { env } from "@/lib/env";
import { ok, fail, clientIp } from "@/lib/http";
import type { LicenseKeyDoc, ActivationDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const Body = z.object({
  token: z.string().min(16),
  appVersion: z.string().max(64).optional(),
});

// POST /api/validate — heartbeat. The DB is the source of truth: confirm the key
// is still active/unexpired and the device is still bound, then re-issue a fresh
// token carrying the authoritative server time (for the client's clock guard).
export async function POST(req: Request) {
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
  if (!act || act.status === "deactivated") return fail("deactivated", 403);

  await acts.updateOne(
    { _id: act._id },
    { $set: { lastSeenUtc: now, ip: clientIp(req), appVersion: parsed.appVersion ?? act.appVersion } },
  );
  await keys.updateOne({ _id: keyOid }, { $set: { lastSeenUtc: now } });

  const token = makeToken({
    v: 1,
    keyId: keyDoc._id!.toHexString(),
    edition: keyDoc.edition,
    expiryUtc: keyDoc.expiryUtc ? keyDoc.expiryUtc.toISOString() : null,
    deviceFp: payload.deviceFp,
    issuedUtc: now.toISOString(),
    serverTimeUtc: now.toISOString(),
    graceDays: env.offlineGraceDays(),
    heartbeatHours: env.heartbeatHours(),
    nonce: crypto.randomBytes(8).toString("hex"),
  });

  return ok({
    token,
    edition: keyDoc.edition,
    expiryUtc: keyDoc.expiryUtc ? keyDoc.expiryUtc.toISOString() : null,
    serverTimeUtc: now.toISOString(),
  });
}

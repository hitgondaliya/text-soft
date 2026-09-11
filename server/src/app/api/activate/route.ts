import { z } from "zod";
import crypto from "node:crypto";
import { getDb } from "@/lib/mongo";
import { hashKey } from "@/lib/licenseKey";
import { makeToken } from "@/lib/token";
import { env } from "@/lib/env";
import { ok, fail, clientIp } from "@/lib/http";
import type { LicenseKeyDoc, ActivationDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const Body = z.object({
  key: z.string().min(4).max(64),
  deviceFp: z.string().min(4).max(128),
  deviceName: z.string().max(128).optional(),
  appVersion: z.string().max(64).optional(),
});

// POST /api/activate — bind a device to a key, return a signed license token.
export async function POST(req: Request) {
  let parsed;
  try {
    parsed = Body.parse(await req.json());
  } catch {
    return fail("bad_request", 400);
  }
  const { key, deviceFp, deviceName, appVersion } = parsed;

  const db = await getDb();
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");
  const acts = db.collection<ActivationDoc>("activations");

  const keyDoc = await keys.findOne({ keyHash: hashKey(key) });
  if (!keyDoc) return fail("invalid", 404);
  if (keyDoc.status === "revoked") return fail("revoked", 403);

  const now = new Date();
  if (keyDoc.expiryUtc && now > keyDoc.expiryUtc) return fail("expired", 403);

  // Reuse an existing slot for this device, else claim a new one within the limit.
  const existing = await acts.findOne({ keyId: keyDoc._id!, deviceFp });
  if (!existing || existing.status === "deactivated") {
    const activeCount = await acts.countDocuments({ keyId: keyDoc._id!, status: "active" });
    if (!existing && activeCount >= keyDoc.deviceLimit) {
      return fail("device_limit_reached", 403, { limit: keyDoc.deviceLimit });
    }
    await acts.updateOne(
      { keyId: keyDoc._id!, deviceFp },
      {
        $set: {
          deviceName,
          ip: clientIp(req),
          appVersion,
          status: "active",
          lastSeenUtc: now,
        },
        $setOnInsert: { keyId: keyDoc._id!, deviceFp, activatedUtc: now },
      },
      { upsert: true },
    );
  } else {
    await acts.updateOne(
      { _id: existing._id },
      { $set: { lastSeenUtc: now, ip: clientIp(req), appVersion, deviceName } },
    );
  }

  await keys.updateOne({ _id: keyDoc._id! }, { $set: { lastSeenUtc: now } });

  const token = makeToken({
    v: 1,
    keyId: keyDoc._id!.toHexString(),
    edition: keyDoc.edition,
    expiryUtc: keyDoc.expiryUtc ? keyDoc.expiryUtc.toISOString() : null,
    deviceFp,
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

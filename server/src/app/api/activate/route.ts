import { z } from "zod";
import { getDb } from "@/lib/mongo";
import { hashKey } from "@/lib/licenseKey";
import { issueToken } from "@/lib/token";
import { ok, fail, clientIp } from "@/lib/http";
import { formatPhone } from "@/lib/phone";
import { lookupKey, normalizeSerial, hashHint, SERIAL_RE } from "@/lib/licensing";
import { logAttempt } from "@/lib/attempts";
import { rateLimited, LIMITS } from "@/lib/ratelimit";
import type { LicenseKeyDoc, ActivationDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const Body = z.object({
  serial: z.string().min(8).max(32),
  key: z.string().min(4).max(64),
  deviceFp: z.string().min(4).max(128),
  machineHint: z.string().max(128).optional(),
  deviceName: z.string().max(128).optional(),
  appVersion: z.string().max(64).optional(),
});

// POST /api/activate — bind THIS device to a key. One key, one device: if the
// key already lives on another PC the call is refused and the attempt is stored
// with the caller's device name and IP, so the panel shows who tried.
export async function POST(req: Request) {
  const ip = clientIp(req);
  if (rateLimited(`act:ip:${ip}`, LIMITS.perIp.limit, LIMITS.perIp.windowMs)) return fail("rate_limited", 429);

  let parsed;
  try {
    parsed = Body.parse(await req.json());
  } catch {
    return fail("bad_request", 400);
  }
  const { key, deviceFp, deviceName, appVersion } = parsed;
  const serial = normalizeSerial(parsed.serial);
  if (!SERIAL_RE.test(serial)) return fail("serial_mismatch", 403);
  if (rateLimited(`act:key:${hashKey(key)}`, LIMITS.perKey.limit, LIMITS.perKey.windowMs)) return fail("rate_limited", 429);

  const machineHint = hashHint(parsed.machineHint);
  const ctx = { ip, source: "app" as const, deviceFp, machineHint, deviceName, appVersion };

  const found = await lookupKey(serial, key, ctx);
  if ("error" in found) return fail(found.error, found.error === "invalid" ? 404 : 403);
  const keyDoc = found.key;

  const db = await getDb();
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");
  const acts = db.collection<ActivationDoc>("activations");
  const now = new Date();

  const holder = await acts.findOne({ keyId: keyDoc._id!, status: "active" });

  // Another PC holds this key → refuse, record, tell the app who has it.
  if (holder && holder.deviceFp !== deviceFp) {
    await logAttempt({ key: keyDoc, reason: "already_bound", boundDeviceName: holder.deviceName, ...ctx });
    return fail("already_bound", 403, { boundDeviceName: holder.deviceName ?? "", boundUtc: holder.activatedUtc.toISOString() });
  }

  // This PC once had the key and an admin moved it away → it stays out.
  const mine = await acts.findOne({ keyId: keyDoc._id!, deviceFp });
  if (!holder && mine && mine.status === "released") {
    await logAttempt({ key: keyDoc, reason: "released", ...ctx });
    return fail("released", 403);
  }

  let act: ActivationDoc;
  if (holder) {
    // Same device again (reinstall, re-login) — refresh and re-issue.
    await acts.updateOne(
      { _id: holder._id },
      { $set: { lastSeenUtc: now, ip, appVersion, deviceName, ...(machineHint ? { machineHint } : {}) } },
    );
    act = { ...holder, lastSeenUtc: now };
  } else {
    // A PC never holds two keys: drop any other key bound to this device.
    await acts.updateMany(
      { deviceFp, status: "active", keyId: { $ne: keyDoc._id! } },
      { $set: { status: "replaced", releasedUtc: now, releasedBy: "system", releaseReason: `Replaced by ${keyDoc.serial} #${keyDoc.seq}` } },
    );
    const fresh: ActivationDoc = {
      keyId: keyDoc._id!,
      serial: keyDoc.serial,
      deviceFp,
      machineHint,
      deviceName,
      ip,
      appVersion,
      status: "active",
      activatedUtc: now,
      lastSeenUtc: now,
    };
    if (mine) {
      // A `replaced` row for this key+device from the past: revive it.
      await acts.updateOne(
        { _id: mine._id },
        { $set: { ...fresh, user: mine.user }, $unset: { releasedUtc: "", releasedBy: "", releaseReason: "" } },
      );
      act = { ...fresh, _id: mine._id, user: mine.user };
    } else {
      const ins = await acts.insertOne(fresh);
      act = { ...fresh, _id: ins.insertedId };
    }
  }

  await keys.updateOne({ _id: keyDoc._id! }, { $set: { lastSeenUtc: now } });

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

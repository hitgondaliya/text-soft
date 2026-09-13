import { z } from "zod";
import { hashKey } from "@/lib/licenseKey";
import { ok, fail, clientIp } from "@/lib/http";
import { lookupKey, normalizeSerial, hashHint, boundDevice, SERIAL_RE } from "@/lib/licensing";
import { logAttempt } from "@/lib/attempts";
import { rateLimited, LIMITS } from "@/lib/ratelimit";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const Body = z.object({
  serial: z.string().min(8).max(32),
  key: z.string().min(4).max(64),
  /** Raw Windows MachineGuid from the installer. Hashed before storage. */
  machineHint: z.string().max(128).optional(),
  deviceName: z.string().max(128).optional(),
  installerVersion: z.string().max(64).optional(),
});

// POST /api/precheck — the installer's question before it copies a single file:
// "is this serial+key real, live, and free for this PC?" Nothing is bound here;
// the app binds on first run. A key already on another PC stops the install and
// is logged exactly like an in-app attempt, so the panel shows it.
export async function POST(req: Request) {
  const ip = clientIp(req);
  if (rateLimited(`pre:ip:${ip}`, LIMITS.perIp.limit, LIMITS.perIp.windowMs)) return fail("rate_limited", 429);

  let parsed;
  try {
    parsed = Body.parse(await req.json());
  } catch {
    return fail("bad_request", 400);
  }
  const serial = normalizeSerial(parsed.serial);
  if (!SERIAL_RE.test(serial)) return fail("serial_mismatch", 403);
  if (rateLimited(`pre:key:${hashKey(parsed.key)}`, LIMITS.perKey.limit, LIMITS.perKey.windowMs)) return fail("rate_limited", 429);

  const machineHint = hashHint(parsed.machineHint);
  const ctx = {
    ip,
    source: "installer" as const,
    machineHint,
    deviceName: parsed.deviceName,
    appVersion: parsed.installerVersion ? `setup ${parsed.installerVersion}` : "setup",
  };

  const found = await lookupKey(serial, parsed.key, ctx);
  if ("error" in found) return fail(found.error, found.error === "invalid" ? 404 : 403);
  const keyDoc = found.key;

  const holder = await boundDevice(keyDoc._id);
  if (holder) {
    const samePc = !!machineHint && !!holder.machineHint && holder.machineHint === machineHint;
    if (!samePc) {
      await logAttempt({ key: keyDoc, reason: "already_bound", boundDeviceName: holder.deviceName, ...ctx });
      return fail("already_bound", 403, { boundDeviceName: holder.deviceName ?? "" });
    }
  }

  return ok({
    ok: true,
    serial: keyDoc.serial,
    edition: keyDoc.edition,
    expiryUtc: keyDoc.expiryUtc ? keyDoc.expiryUtc.toISOString() : null,
    reinstall: !!holder,
  });
}

import { getDb } from "./mongo";
import type { ActivationAttemptDoc, AttemptReason, LicenseKeyDoc } from "./models";

/**
 * Record a refused activation. Called from /api/activate and /api/precheck for
 * every failure that involves a real key, and for unknown keys too (prefix only)
 * so a brute-force shows up. Best effort: logging must never break the response.
 */
export async function logAttempt(a: {
  key?: LicenseKeyDoc | null;
  typedKey?: string;
  deviceFp?: string;
  machineHint?: string;
  deviceName?: string;
  ip: string;
  appVersion?: string;
  reason: AttemptReason;
  source: "app" | "installer";
  boundDeviceName?: string;
}): Promise<void> {
  try {
    const db = await getDb();
    const doc: ActivationAttemptDoc = {
      keyId: a.key?._id,
      serial: a.key?.serial,
      seq: a.key?.seq,
      keyPrefix: a.key?.keyPrefix,
      typedPrefix: a.key ? undefined : a.typedKey?.toUpperCase().slice(0, 9),
      deviceFp: a.deviceFp,
      machineHint: a.machineHint,
      deviceName: a.deviceName,
      ip: a.ip,
      appVersion: a.appVersion,
      reason: a.reason,
      source: a.source,
      boundDeviceName: a.boundDeviceName,
      createdUtc: new Date(),
    };
    await db.collection<ActivationAttemptDoc>("activationAttempts").insertOne(doc);
  } catch {
    // never fail the caller because the log could not be written
  }
}

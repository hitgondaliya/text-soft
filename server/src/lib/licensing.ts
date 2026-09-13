import crypto from "node:crypto";
import { getDb } from "./mongo";
import { hashKey } from "./licenseKey";
import { logAttempt } from "./attempts";
import type { ActivationDoc, LicenseKeyDoc } from "./models";

export const SERIAL_RE = /^TXS-\d{4}-\d{4}$/;

export function normalizeSerial(s: string): string {
  return s.trim().toUpperCase().replace(/\s+/g, "");
}

/** The installer and app send the raw MachineGuid; we only ever store its hash. */
export function hashHint(raw: string | undefined | null): string | undefined {
  if (!raw) return undefined;
  const v = raw.trim();
  if (!v) return undefined;
  return crypto.createHash("sha256").update(v.toLowerCase(), "utf8").digest("hex");
}

export type LookupFail = "invalid" | "serial_mismatch" | "revoked" | "expired";

/**
 * Find the key for a serial+key pair and check it is sellable-state. Every
 * failure is logged as an attempt (so the panel shows a key being probed) and
 * returned as the error code the client already understands.
 */
export async function lookupKey(
  serial: string,
  key: string,
  ctx: { ip: string; source: "app" | "installer"; deviceFp?: string; machineHint?: string; deviceName?: string; appVersion?: string },
): Promise<{ key: LicenseKeyDoc } | { error: LookupFail }> {
  const db = await getDb();
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");
  const keyDoc = await keys.findOne({ keyHash: hashKey(key) });

  const fail = async (reason: LookupFail) => {
    await logAttempt({ key: keyDoc, typedKey: key, reason, ...ctx });
    return { error: reason } as const;
  };

  if (!keyDoc) return fail("invalid");
  if (keyDoc.serial !== normalizeSerial(serial)) return fail("serial_mismatch");
  if (keyDoc.status === "revoked") return fail("revoked");
  if (keyDoc.expiryUtc && new Date() > keyDoc.expiryUtc) return fail("expired");
  return { key: keyDoc };
}

/** The device currently holding a key, if any. */
export async function boundDevice(keyId: LicenseKeyDoc["_id"]): Promise<ActivationDoc | null> {
  const db = await getDb();
  return db.collection<ActivationDoc>("activations").findOne({ keyId: keyId!, status: "active" });
}

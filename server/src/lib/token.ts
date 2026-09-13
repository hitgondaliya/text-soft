import crypto from "node:crypto";
import { sign, verify, b64url, fromB64url } from "./crypto";
import { env } from "./env";
import type { Edition, LicenseKeyDoc, ActivationDoc } from "./models";

// ---- Signed license token (the contract the WPF client verifies) ----
//
// Wire format:   <b64url(payloadJson)>.<b64url(ed25519sig)>
//
// The client splits on ".", base64url-decodes the first segment, verifies the
// Ed25519 signature over those exact JSON bytes using the embedded PUBLIC key,
// then parses the JSON. Any tampering or wrong device fails verification.
//
// v2 adds `serial`, `registered` and `validUntilUtc`. The client treats a token
// past validUntilUtc as "must talk to the server", which stops an old token
// being replayed forever on a machine that never goes online again.

export interface TokenPayload {
  v: 2;
  keyId: string; // Mongo _id hex of the license key
  serial: string; // "TXS-2026-0001"
  edition: Edition; // "demo" | "full"
  expiryUtc: string | null; // ISO 8601 UTC, or null = perpetual
  deviceFp: string; // device fingerprint this token is bound to
  /** False until the end user submitted name/email/mobile. The app stays on the
   *  registration screen while this is false. */
  registered: boolean;
  issuedUtc: string; // ISO 8601 UTC — when this token was minted
  serverTimeUtc: string; // ISO 8601 UTC — authoritative "now" for the clock guard
  /** Hard ceiling on how long this token may be trusted without a heartbeat. */
  validUntilUtc: string;
  graceDays: number; // offline grace window the client may honor
  heartbeatHours: number; // how often the client should re-validate
  nonce: string; // random, makes each token unique
}

export function makeToken(payload: TokenPayload): string {
  const json = Buffer.from(JSON.stringify(payload), "utf8");
  const sig = sign(json);
  return `${b64url(json)}.${b64url(sig)}`;
}

/** The token a key+device pair gets right now. One place, so activate,
 *  validate and register all mint the same shape. */
export function issueToken(key: LicenseKeyDoc, act: ActivationDoc, now = new Date()): string {
  const graceDays = env.offlineGraceDays();
  const heartbeatHours = env.heartbeatHours();
  const validUntil = new Date(now.getTime() + (graceDays * 24 + heartbeatHours + 12) * 3600 * 1000);
  return makeToken({
    v: 2,
    keyId: key._id!.toHexString(),
    serial: key.serial,
    edition: key.edition,
    expiryUtc: key.expiryUtc ? key.expiryUtc.toISOString() : null,
    deviceFp: act.deviceFp,
    registered: !!act.user,
    issuedUtc: now.toISOString(),
    serverTimeUtc: now.toISOString(),
    validUntilUtc: validUntil.toISOString(),
    graceDays,
    heartbeatHours,
    nonce: crypto.randomBytes(8).toString("hex"),
  });
}

/**
 * Verify a token's signature and return its payload, or null if malformed/forged.
 * Used by the heartbeat so the client can't smuggle in an arbitrary keyId.
 */
export function readToken(token: string): TokenPayload | null {
  const dot = token.indexOf(".");
  if (dot <= 0) return null;
  try {
    const json = fromB64url(token.slice(0, dot));
    const sig = fromB64url(token.slice(dot + 1));
    if (!verify(json, sig)) return null;
    return JSON.parse(json.toString("utf8")) as TokenPayload;
  } catch {
    return null;
  }
}

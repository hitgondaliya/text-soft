import { sign, verify, b64url, fromB64url } from "./crypto";
import type { Edition } from "./models";

// ---- Signed license token (the contract the WPF client verifies) ----
//
// Wire format:   <b64url(payloadJson)>.<b64url(ed25519sig)>
//
// The client splits on ".", base64url-decodes the first segment, verifies the
// Ed25519 signature over those exact JSON bytes using the embedded PUBLIC key,
// then parses the JSON. Any tampering or wrong device fails verification.
//
// Keep this shape STABLE. If it must change, bump `v` and have the client accept
// both versions during rollout.

export interface TokenPayload {
  v: 1;
  keyId: string; // Mongo _id hex of the license key
  edition: Edition; // "demo" | "full"
  expiryUtc: string | null; // ISO 8601 UTC, or null = perpetual
  deviceFp: string; // device fingerprint this token is bound to
  issuedUtc: string; // ISO 8601 UTC — when this token was minted
  serverTimeUtc: string; // ISO 8601 UTC — authoritative "now" for the clock guard
  graceDays: number; // offline grace window the client may honor
  heartbeatHours: number; // how often the client should re-validate
  nonce: string; // random, makes each token unique
}

export function makeToken(payload: TokenPayload): string {
  const json = Buffer.from(JSON.stringify(payload), "utf8");
  const sig = sign(json);
  return `${b64url(json)}.${b64url(sig)}`;
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

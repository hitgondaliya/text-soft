import crypto from "node:crypto";
import { env } from "./env";

// Ed25519 signing. The private key lives only in env (base64 of its PEM).
// Node's crypto.sign(null, msg, ed25519Key) produces a raw 64-byte signature,
// which the WPF client verifies against the embedded public key.

let cachedPrivate: crypto.KeyObject | null = null;
let cachedPublic: crypto.KeyObject | null = null;

function privateKey(): crypto.KeyObject {
  if (!cachedPrivate) {
    const pem = Buffer.from(env.privateKeyB64(), "base64").toString("utf8");
    cachedPrivate = crypto.createPrivateKey(pem);
  }
  return cachedPrivate;
}

function publicKey(): crypto.KeyObject {
  if (!cachedPublic) {
    cachedPublic = crypto.createPublicKey(privateKey());
  }
  return cachedPublic;
}

/** Sign raw bytes with Ed25519. Returns a 64-byte signature. */
export function sign(message: Buffer): Buffer {
  // For Ed25519 the algorithm arg must be null.
  return crypto.sign(null, message, privateKey());
}

/** Verify an Ed25519 signature over message using our own public key. */
export function verify(message: Buffer, signature: Buffer): boolean {
  try {
    return crypto.verify(null, message, publicKey(), signature);
  } catch {
    return false;
  }
}

/** Decode a base64url string back to bytes. */
export function fromB64url(s: string): Buffer {
  return Buffer.from(s.replace(/-/g, "+").replace(/_/g, "/"), "base64");
}

/** SHA-256 hex of a UTF-8 string. Used to hash license key strings before storage. */
export function sha256Hex(input: string): string {
  return crypto.createHash("sha256").update(input, "utf8").digest("hex");
}

/** URL-safe base64 without padding. */
export function b64url(buf: Buffer): string {
  return buf.toString("base64").replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

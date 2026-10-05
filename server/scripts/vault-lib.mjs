// The vault's crypto for the maintenance scripts (vault-check.mjs, vault-reseal.mjs).
// It MUST stay byte-for-byte compatible with src/lib/vault.ts: same HKDF salt/info,
// same AAD, same wire format  v1.<sid>.<iv>.<tag>.<ct>  (base64url).
// The scripts are plain Node (no TypeScript loader on the VPS), hence the copy.

import crypto from "node:crypto";

export const INFO = { keys: "textsoft/keys/v1", smtp: "textsoft/smtp/v1" };

/** The configured secret for "k" (KEY_VAULT_SECRET) or "c" (COOKIE_SECRET), or null. */
export function secretBytes(sid) {
  const raw = sid === "k" ? process.env.KEY_VAULT_SECRET ?? "" : process.env.COOKIE_SECRET ?? "";
  const v = raw.trim();
  return v ? Buffer.from(v, "utf8") : null;
}

function deriveKey(sid, purpose) {
  const ikm = secretBytes(sid);
  if (!ikm) return null;
  const salt = Buffer.from(`textsoft-vault/${sid}`, "utf8");
  return Buffer.from(crypto.hkdfSync("sha256", ikm, salt, Buffer.from(INFO[purpose], "utf8"), 32));
}

const aad = (sid, purpose) => Buffer.from(`v1.${sid}.${INFO[purpose]}`, "utf8");

/** "k" | "c" | null — which secret sealed the value. */
export function sealedWith(sealed) {
  if (typeof sealed !== "string") return null;
  const p = sealed.split(".");
  return p.length === 5 && p[0] === "v1" && (p[1] === "k" || p[1] === "c") ? p[1] : null;
}

/** Plaintext, or null (junk, tampered, wrong purpose, or the sealing secret is missing/different). */
export function open(purpose, sealed) {
  try {
    const sid = sealedWith(sealed);
    if (!sid) return null;
    const [, , ivB, tagB, ctB] = sealed.split(".");
    const iv = Buffer.from(ivB, "base64url");
    const tag = Buffer.from(tagB, "base64url");
    if (iv.length !== 12 || tag.length !== 16) return null;
    const key = deriveKey(sid, purpose);
    if (!key) return null;
    const d = crypto.createDecipheriv("aes-256-gcm", key, iv);
    d.setAAD(aad(sid, purpose));
    d.setAuthTag(tag);
    return Buffer.concat([d.update(Buffer.from(ctB, "base64url")), d.final()]).toString("utf8");
  } catch {
    return null;
  }
}

/** Seal with an explicit secret id (the app itself always picks "k" when it is set). */
export function seal(purpose, plaintext, sid) {
  const key = deriveKey(sid, purpose);
  if (!key) throw new Error(`no secret for "${sid}"`);
  const iv = crypto.randomBytes(12);
  const c = crypto.createCipheriv("aes-256-gcm", key, iv);
  c.setAAD(aad(sid, purpose));
  const ct = Buffer.concat([c.update(plaintext, "utf8"), c.final()]);
  return ["v1", sid, iv.toString("base64url"), c.getAuthTag().toString("base64url"), ct.toString("base64url")].join(".");
}

/** Same as src/lib/licenseKey.ts hashKey(): sha256 hex of the normalised key. */
export function hashKey(input) {
  const n = input
    .toUpperCase()
    .replace(/\s+/g, "")
    .replace(/[IL]/g, "1")
    .replace(/O/g, "0")
    .replace(/U/g, "V")
    .trim();
  return crypto.createHash("sha256").update(n, "utf8").digest("hex");
}

/** A minimum for KEY_VAULT_SECRET: HKDF does not stretch, so a short passphrase could be guessed offline. */
export const MIN_VAULT_SECRET = 32;

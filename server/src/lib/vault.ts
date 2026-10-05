import crypto from "node:crypto";
import { env } from "./env";

// A small envelope for the secrets the panel must be able to read back later:
//   - the licence key strings (so an admin can copy a key again after the sale),
//   - the SMTP password saved from Settings → Email.
//
// AES-256-GCM with a fresh 96-bit IV per value. The AES key is never stored: it is
// derived with HKDF-SHA256 from a server secret and a per-purpose label, so a key
// ciphertext can never be opened as an SMTP password or the other way round.
//
// Which server secret: KEY_VAULT_SECRET when it is set, otherwise COOKIE_SECRET
// (always set — the panel cannot sign anyone in without it). The ciphertext records
// which one sealed it, so setting KEY_VAULT_SECRET later does not orphan what was
// sealed before: old values still open with COOKIE_SECRET, new ones use the vault
// secret. Changing or losing the secret that sealed a value makes it unreadable —
// keys then show as "not recoverable" and can be reissued; the SMTP password must
// be typed in again. The keys themselves keep working: activation checks the hash.
//
// Wire format (all base64url):  v1.<sid>.<iv>.<tag>.<ciphertext>
//   sid  "k" = KEY_VAULT_SECRET,  "c" = COOKIE_SECRET

type SecretId = "k" | "c";
type Purpose = "keys" | "smtp";

const VERSION = "v1";
const INFO: Record<Purpose, string> = {
  keys: "textsoft/keys/v1",
  smtp: "textsoft/smtp/v1",
};

function secretBytes(sid: SecretId): Buffer | null {
  const raw = sid === "k" ? env.keyVaultSecret() : process.env.COOKIE_SECRET ?? "";
  const v = raw.trim();
  return v ? Buffer.from(v, "utf8") : null;
}

function deriveKey(sid: SecretId, purpose: Purpose): Buffer | null {
  const ikm = secretBytes(sid);
  if (!ikm) return null;
  // The salt keeps the two sources apart; the info label keeps the purposes apart.
  const salt = Buffer.from(`textsoft-vault/${sid}`, "utf8");
  return Buffer.from(crypto.hkdfSync("sha256", ikm, salt, Buffer.from(INFO[purpose], "utf8"), 32));
}

function aad(sid: SecretId, purpose: Purpose): Buffer {
  return Buffer.from(`${VERSION}.${sid}.${INFO[purpose]}`, "utf8");
}

function seal(purpose: Purpose, plaintext: string): string {
  const sid: SecretId = secretBytes("k") ? "k" : "c";
  const key = deriveKey(sid, purpose);
  if (!key) throw new Error("vault: no secret available (set KEY_VAULT_SECRET or COOKIE_SECRET)");
  const iv = crypto.randomBytes(12);
  const cipher = crypto.createCipheriv("aes-256-gcm", key, iv);
  cipher.setAAD(aad(sid, purpose));
  const ct = Buffer.concat([cipher.update(plaintext, "utf8"), cipher.final()]);
  const tag = cipher.getAuthTag();
  return [VERSION, sid, iv.toString("base64url"), tag.toString("base64url"), ct.toString("base64url")].join(".");
}

interface Parsed {
  sid: SecretId;
  iv: Buffer;
  tag: Buffer;
  ct: Buffer;
}

function parse(sealed: unknown): Parsed | null {
  if (typeof sealed !== "string") return null;
  const parts = sealed.split(".");
  if (parts.length !== 5 || parts[0] !== VERSION) return null;
  const sid = parts[1];
  if (sid !== "k" && sid !== "c") return null;
  const iv = Buffer.from(parts[2], "base64url");
  const tag = Buffer.from(parts[3], "base64url");
  const ct = Buffer.from(parts[4], "base64url");
  if (iv.length !== 12 || tag.length !== 16) return null;
  return { sid, iv, tag, ct };
}

/** Never throws: null for junk, a tampered value, a wrong purpose or a missing secret. */
function open(purpose: Purpose, sealed: unknown): string | null {
  try {
    const p = parse(sealed);
    if (!p) return null;
    const key = deriveKey(p.sid, purpose);
    if (!key) return null;
    const decipher = crypto.createDecipheriv("aes-256-gcm", key, p.iv);
    decipher.setAAD(aad(p.sid, purpose));
    decipher.setAuthTag(p.tag);
    return Buffer.concat([decipher.update(p.ct), decipher.final()]).toString("utf8");
  } catch {
    return null;
  }
}

/** True when the value is well-formed and the secret that sealed it is configured. Does not decrypt. */
export function canOpen(sealed: unknown): boolean {
  const p = parse(sealed);
  return !!p && !!secretBytes(p.sid);
}

/** Which secret sealed the value: "k" (KEY_VAULT_SECRET), "c" (COOKIE_SECRET) or null. */
export function sealedWith(sealed: unknown): SecretId | null {
  return parse(sealed)?.sid ?? null;
}

export const sealKey = (key: string): string => seal("keys", key);
export const openKey = (sealed: unknown): string | null => open("keys", sealed);
export const sealSecret = (secret: string): string => seal("smtp", secret);
export const openSecret = (sealed: unknown): string | null => open("smtp", sealed);

/**
 * Round-trips, tamper and cross-purpose checks. Pure crypto, no database.
 * Run it with any loader that resolves extensionless TS imports, or call it from a route.
 */
export function vaultSelfCheck(): { ok: boolean; sealedWith: SecretId | null; checks: Array<{ name: string; ok: boolean }> } {
  const checks: Array<{ name: string; ok: boolean }> = [];
  const check = (name: string, fn: () => boolean) => {
    let ok = false;
    try {
      ok = fn();
    } catch {
      ok = false;
    }
    checks.push({ name, ok });
  };

  const sample = "TXSF-9F2K-7QH4-M3TP";
  const a = sealKey(sample);
  const b = sealKey(sample);
  check("key round-trip", () => openKey(a) === sample);
  check("fresh IV per seal", () => a !== b && openKey(b) === sample);
  check("format v1.<sid>.<iv>.<tag>.<ct>", () => /^v1\.[kc]\.[A-Za-z0-9_-]{16}\.[A-Za-z0-9_-]{22}\.[A-Za-z0-9_-]+$/.test(a));
  check("smtp round-trip (unicode, spaces)", () => openSecret(sealSecret("pässwörd with spaces ✓")) === "pässwörd with spaces ✓");
  check("empty string round-trip", () => openSecret(sealSecret("")) === "");
  check("key ciphertext refused as smtp secret", () => openSecret(a) === null);
  check("smtp ciphertext refused as key", () => openKey(sealSecret("x")) === null);
  check("tampered ciphertext → null", () => {
    const parts = a.split(".");
    const ct = Buffer.from(parts[4], "base64url");
    ct[0] ^= 0x01;
    parts[4] = ct.toString("base64url");
    return openKey(parts.join(".")) === null;
  });
  check("tampered tag → null", () => {
    const parts = a.split(".");
    const tag = Buffer.from(parts[3], "base64url");
    tag[5] ^= 0x80;
    parts[3] = tag.toString("base64url");
    return openKey(parts.join(".")) === null;
  });
  check("relabelled secret id → null", () => {
    const parts = a.split(".");
    parts[1] = parts[1] === "k" ? "c" : "k";
    return openKey(parts.join(".")) === null;
  });
  check("junk → null, never throws", () =>
    [undefined, null, 42, "", "v1", "v1.k.a.b.c", "v2.k.AAAAAAAAAAAAAAAA.AAAAAAAAAAAAAAAAAAAAAA.AAAA", a + "."].every((x) => openKey(x) === null),
  );
  check("canOpen on own ciphertext", () => canOpen(a) && !canOpen("nope"));

  return { ok: checks.every((c) => c.ok), sealedWith: sealedWith(a), checks };
}

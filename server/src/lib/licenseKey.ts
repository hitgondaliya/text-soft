import crypto from "node:crypto";
import { sha256Hex } from "./crypto";

// Human-readable license key strings: TXSF-XXXX-XXXX-XXXX
// Crockford Base32 alphabet (no I L O U) so keys are easy to read/dictate.
// This MUST match the client's Base32 decode table if the client ever parses the
// raw key — but in the online model the client only forwards the string, so the
// server alphabet is authoritative.

const ALPHABET = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
const GROUPS = 3; // groups after the TXSF prefix
const GROUP_LEN = 4;

/** Generate a fresh, random key string like "TXSF-9F2K-7QH4-M3TP". */
export function generateKeyString(): string {
  const parts: string[] = ["TXSF"];
  for (let g = 0; g < GROUPS; g++) {
    let s = "";
    const bytes = crypto.randomBytes(GROUP_LEN);
    for (let i = 0; i < GROUP_LEN; i++) {
      s += ALPHABET[bytes[i] % ALPHABET.length];
    }
    parts.push(s);
  }
  return parts.join("-");
}

/** Normalize user input: upper-case, strip spaces, unify look-alikes. */
export function normalizeKey(input: string): string {
  return input
    .toUpperCase()
    .replace(/\s+/g, "")
    .replace(/[IL]/g, "1")
    .replace(/O/g, "0")
    .replace(/U/g, "V")
    .trim();
}

/** Hash for storage/lookup. Never store the raw key. */
export function hashKey(input: string): string {
  return sha256Hex(normalizeKey(input));
}

/** Short display prefix, e.g. "TXSF-9F2K". */
export function keyPrefix(keyString: string): string {
  const parts = keyString.split("-");
  return parts.slice(0, 2).join("-");
}

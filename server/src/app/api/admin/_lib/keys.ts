import { ObjectId } from "mongodb";
import { getDb } from "@/lib/mongo";
import { hashKey } from "@/lib/licenseKey";
import { canOpen, openKey } from "@/lib/vault";
import type { LicenseKeyDoc, OrderDoc } from "@/lib/models";

// Shared by the admin key/order routes. The leading underscore keeps this folder
// out of Next's routing.

export function oid(id: string): ObjectId | null {
  if (!/^[0-9a-fA-F]{24}$/.test(id)) return null;
  try {
    return new ObjectId(id);
  } catch {
    return null;
  }
}

/** Accepts either the Mongo id or the serial itself, so TXS-2026-0001 is a URL. */
export async function findOrder(idOrSerial: string): Promise<OrderDoc | null> {
  const db = await getDb();
  const orders = db.collection<OrderDoc>("orders");
  const _id = oid(idOrSerial);
  if (_id) {
    const byId = await orders.findOne({ _id });
    if (byId) return byId;
  }
  return orders.findOne({ serial: idOrSerial.trim().toUpperCase() });
}

/** Can this key be shown again? Cheap — checks the sealed copy exists and its secret is configured. */
export function isRecoverable(k: Pick<LicenseKeyDoc, "keyEnc">): boolean {
  return !!k.keyEnc && canOpen(k.keyEnc);
}

/**
 * The key string, or null when it cannot be recovered. The decrypted value must
 * hash to the stored keyHash, so a damaged or misplaced ciphertext is never shown
 * as if it were this key.
 */
export function revealKey(k: Pick<LicenseKeyDoc, "keyEnc" | "keyHash">): string | null {
  if (!k.keyEnc) return null;
  const plain = openKey(k.keyEnc);
  if (!plain) return null;
  return hashKey(plain) === k.keyHash ? plain : null;
}

/** Literal text for a Mongo $regex, so a search for "+91" or "(" is not a syntax error. */
export function escapeRegex(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

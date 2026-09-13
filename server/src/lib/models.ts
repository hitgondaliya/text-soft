import { ObjectId } from "mongodb";

// ---- MongoDB document shapes (collections, not tables) ----

export type Edition = "demo" | "full";
export type KeyStatus = "active" | "revoked";
export type ActivationStatus = "active" | "deactivated";

/** admins — who can log into the panel. Password is bcrypt-hashed. */
export interface AdminDoc {
  _id?: ObjectId;
  email: string;
  passwordHash: string;
  role: "owner" | "staff";
  createdUtc: Date;
}

/**
 * orders — one per purchase, identified by a SERIAL NUMBER. A customer buying 40
 * keys gets one serial and 40 keys under it, so the sale is a single record you
 * can look up, re-send or revoke as a whole, instead of 40 unrelated rows.
 *
 * A repeat purchase by the same customer gets its own serial — a serial is the
 * sale, not the person. Their history stays together because every order carries
 * the customer's email, which the list filters on.
 */
export interface OrderDoc {
  _id?: ObjectId;
  serial: string; // "TXS-2026-0001" — unique, sequential within the year
  customerName: string;
  customerEmail?: string;
  customerPhone?: string;
  company?: string;
  /** What was sold. Every key under the serial is issued on these terms. */
  edition: Edition;
  expiryUtc: Date | null; // null = perpetual
  deviceLimit: number;
  quantity: number;
  note?: string;
  createdBy: string; // admin email
  createdUtc: Date;
}

/**
 * licenseKeys — one per issued key. We store only the SHA-256 hash of the key
 * string (like a password) plus a short prefix for display. Raw key is never kept.
 */
export interface LicenseKeyDoc {
  _id?: ObjectId;
  keyHash: string; // sha256(normalizedKey) hex
  keyPrefix: string; // e.g. "TXSF-9F2K" for the list UI
  /** The purchase this key was issued under. */
  orderId: ObjectId;
  /** Copied from the order so a key can be listed and searched without a join. */
  serial: string;
  /** Position within the purchase, 1..quantity — "key 7 of 40". */
  seq: number;
  edition: Edition;
  expiryUtc: Date | null; // null = perpetual
  deviceLimit: number;
  status: KeyStatus;
  customerName?: string;
  customerEmail?: string;
  note?: string;
  createdBy: string; // admin email
  createdUtc: Date;
  lastSeenUtc?: Date | null;
}

/** counters — atomic sequence generator, one document per counter name. */
export interface CounterDoc {
  _id: string; // e.g. "serial-2026"
  seq: number;
}

/** activations — a device bound to a key. Unique on (keyId, deviceFp). */
export interface ActivationDoc {
  _id?: ObjectId;
  keyId: ObjectId;
  deviceFp: string;
  deviceName?: string;
  ip?: string;
  appVersion?: string;
  status: ActivationStatus;
  activatedUtc: Date;
  lastSeenUtc: Date;
}

/** auditLog — every admin mutation, for accountability. */
export interface AuditDoc {
  _id?: ObjectId;
  adminEmail: string;
  action: string; // "key.create" | "key.revoke" | "key.extend" | ...
  target?: string; // key id / prefix
  detail?: Record<string, unknown>;
  createdUtc: Date;
}

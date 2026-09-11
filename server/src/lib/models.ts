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
 * licenseKeys — one per issued key. We store only the SHA-256 hash of the key
 * string (like a password) plus a short prefix for display. Raw key is never kept.
 */
export interface LicenseKeyDoc {
  _id?: ObjectId;
  keyHash: string; // sha256(normalizedKey) hex
  keyPrefix: string; // e.g. "TXSF-9F2K" for the list UI
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

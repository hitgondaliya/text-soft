import { ObjectId } from "mongodb";

// ---- MongoDB document shapes (collections, not tables) ----

export type Edition = "demo" | "full";
export type KeyStatus = "active" | "revoked";
/**
 * active   — this device holds the key.
 * released — an admin moved the key off this device ("Move to another device");
 *            the device is locked at its next check-in. Kept as history.
 * replaced — the same device activated a different key; the old binding is
 *            dropped so a PC never holds two keys.
 */
export type ActivationStatus = "active" | "released" | "replaced";

/**
 * admins — who can log into the panel. Password is bcrypt-hashed.
 * owner: everything, including managing other admins. staff: everything else.
 */
export interface AdminDoc {
  _id?: ObjectId;
  email: string;
  name?: string;
  passwordHash: string;
  role: "owner" | "staff";
  status?: "active" | "disabled";
  createdUtc: Date;
  createdBy?: string;
  lastLoginUtc?: Date | null;
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
  /** Phone as typed (national digits) plus its country and E.164 form. */
  customerPhone?: string;
  customerPhoneCountry?: string; // ISO 3166-1 alpha-2, e.g. "IN"
  customerPhoneE164?: string; // "+919825000000"
  company?: string;
  /** What was sold. Every key under the serial is issued on these terms. */
  edition: Edition;
  expiryUtc: Date | null; // null = perpetual
  /** Always 1 — one key, one device. Kept for old documents. */
  deviceLimit: number;
  quantity: number;
  note?: string;
  createdBy: string; // admin email
  createdUtc: Date;
  /** When the serial + keys were emailed to the customer (null = failed / not sent). */
  emailedUtc?: Date | null;
  emailError?: string;
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
  /** Always 1 — one key, one device. Kept for old documents. */
  deviceLimit: number;
  status: KeyStatus;
  customerName?: string;
  customerEmail?: string;
  note?: string;
  createdBy: string; // admin email
  createdUtc: Date;
  lastSeenUtc?: Date | null;
  /** Copy of the end user who registered on the bound device, for lists/search. */
  userName?: string;
  userEmail?: string;
  userPhoneE164?: string;
}

/** counters — atomic sequence generator, one document per counter name. */
export interface CounterDoc {
  _id: string; // e.g. "serial-2026"
  seq: number;
}

/** The person who registered the software on a device (asked after activation). */
export interface EndUser {
  name: string;
  email: string;
  phoneCountry: string; // ISO 3166-1 alpha-2
  phone: string; // national number as typed, digits only
  phoneE164: string; // "+919825000000"
  registeredUtc: Date;
}

/** activations — a device bound to a key. Unique on (keyId, deviceFp). */
export interface ActivationDoc {
  _id?: ObjectId;
  keyId: ObjectId;
  serial: string;
  deviceFp: string;
  /** sha256 of the Windows MachineGuid — what the installer can send before the
   *  app exists, so a reinstall on the same PC is recognised. */
  machineHint?: string;
  deviceName?: string;
  ip?: string;
  appVersion?: string;
  status: ActivationStatus;
  activatedUtc: Date;
  lastSeenUtc: Date;
  user?: EndUser;
  releasedUtc?: Date;
  releasedBy?: string; // admin email, or "system" for `replaced`
  releaseReason?: string;
}

export type AttemptReason =
  | "already_bound" // key is on another device — the one the customer asked us to block
  | "serial_mismatch" // key exists but not under that serial
  | "invalid" // unknown key
  | "revoked"
  | "expired"
  | "released"; // a device whose key was moved away keeps trying

/**
 * activationAttempts — every refused activation, so the panel can show when a
 * key was tried on a second PC. Never blocks anything itself; it is the record.
 */
export interface ActivationAttemptDoc {
  _id?: ObjectId;
  keyId?: ObjectId; // absent when the key itself is unknown
  serial?: string;
  seq?: number;
  keyPrefix?: string;
  /** What was typed when the key is unknown (prefix only, never the full key). */
  typedPrefix?: string;
  deviceFp?: string;
  machineHint?: string;
  deviceName?: string;
  ip: string;
  appVersion?: string;
  reason: AttemptReason;
  source: "app" | "installer";
  /** For already_bound: the device that legitimately holds the key. */
  boundDeviceName?: string;
  createdUtc: Date;
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

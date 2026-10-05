// Shape of GET /api/admin/orders/:idOrSerial, shared by the purchase page and its dialogs.

export interface KeyRow {
  id: string;
  seq: number;
  prefix: string;
  /** The full key can be revealed / copied / re-sent (false for keys issued before keys were stored). */
  recoverable: boolean;
  status: string;
  expired: boolean;
  expiryUtc: string | null;
  bound: boolean;
  deviceName: string;
  registered: boolean;
  userName: string;
  userEmail: string;
  userPhone: string;
  userPhoneCountry: string;
  attempts: number;
  lastSeenUtc: string | null;
}

export interface Order {
  id: string;
  serial: string;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  customerPhoneCountry: string;
  customerPhoneDisplay: string;
  company: string;
  edition: string;
  quantity: number;
  attemptsTotal: number;
  expiryUtc: string | null;
  expired: boolean;
  note: string;
  emailedUtc: string | null;
  emailError: string;
  /** Last re-send from this page, to whichever address (the customer's or another). */
  lastSentTo?: string;
  lastSentUtc?: string | null;
  createdBy: string;
  createdUtc: string;
  keys: KeyRow[];
}

/** Counts the header, the resend dialog and the keys note all agree on. */
export function keyCounts(keys: KeyRow[]) {
  const revoked = keys.filter((k) => k.status === "revoked").length;
  const live = keys.filter((k) => k.status !== "revoked");
  return {
    total: keys.length,
    revoked,
    bound: keys.filter((k) => k.bound).length,
    registered: keys.filter((k) => k.registered).length,
    /** Stored and not revoked: what Copy all / Download / Resend can hand out. */
    usable: live.filter((k) => k.recoverable).length,
    /** Not revoked but not stored: cannot be shown again. */
    missing: live.filter((k) => !k.recoverable).length,
    /** Not stored, not revoked, not on a PC: the ones Reissue is for. */
    reissuable: live.filter((k) => !k.recoverable && !k.bound).length,
  };
}

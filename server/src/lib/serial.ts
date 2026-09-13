import type { Db } from "mongodb";
import type { CounterDoc } from "./models";

// Serial numbers for purchases: TXS-2026-0001.
//
// A serial has to be unique and it has to be readable over the phone, so it is a
// plain running number per year rather than anything random. Counting existing
// orders would race — two admins creating a purchase in the same second would
// both read 41 and both write TXS-2026-0042 — so the number comes from an atomic
// $inc on a counter document, which MongoDB serialises for us.

const PREFIX = "TXS";

/** Reserve the next serial for this year. Never returns the same value twice. */
export async function nextSerial(db: Db, when: Date = new Date()): Promise<string> {
  const year = when.getUTCFullYear();
  const counters = db.collection<CounterDoc>("counters");

  const res = await counters.findOneAndUpdate(
    { _id: `serial-${year}` },
    { $inc: { seq: 1 } },
    { upsert: true, returnDocument: "after" },
  );

  const seq = res?.seq ?? 1;
  return `${PREFIX}-${year}-${String(seq).padStart(4, "0")}`;
}

/** True for a well-formed serial, so the API can reject junk before querying. */
export function isSerial(s: string): boolean {
  return /^TXS-\d{4}-\d{4,}$/.test(s.trim().toUpperCase());
}

export function normalizeSerial(s: string): string {
  return s.trim().toUpperCase();
}

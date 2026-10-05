// Text the purchase pages hand out: "Copy all keys" and "Download keys .txt".
// One builder, so the clipboard, the file and the new-purchase screen agree.
//
//   Serial: TXS-2026-0001
//   Key 1: TXSF-AAAA-BBBB-CCCC
//   Key 2: TXSF-DDDD-EEEE-FFFF

import { fmtDate, fmtDateTime } from "@/lib/utils";

export interface KeyLine {
  seq: number;
  /** null when the key cannot be shown again (issued before keys were stored). */
  key: string | null;
  revoked?: boolean;
}

/** The keys a customer can actually use: stored and not revoked. */
export function usableKeys(keys: KeyLine[]): Array<{ seq: number; key: string }> {
  return keys.filter((k): k is { seq: number; key: string; revoked?: boolean } => !!k.key && !k.revoked);
}

/** "Serial: …" then one "Key n: …" line per usable key. */
export function keysClipboardText(serial: string, keys: KeyLine[]): string {
  return [`Serial: ${serial}`, ...usableKeys(keys).map((k) => `Key ${k.seq}: ${k.key}`)].join("\n");
}

/** Short summary of what was left out, for a toast description. Empty when nothing was. */
export function leftOutSummary(keys: KeyLine[]): string {
  const missing = keys.filter((k) => !k.key && !k.revoked).length;
  const revoked = keys.filter((k) => k.revoked).length;
  const parts: string[] = [];
  if (missing) parts.push(`${missing} not stored (issued before keys were saved)`);
  if (revoked) parts.push(`${revoked} revoked`);
  return parts.length ? `Left out: ${parts.join(", ")}.` : "";
}

export interface KeysFileInfo {
  serial: string;
  customerName?: string;
  company?: string;
  edition?: string;
  expiryUtc?: string | null;
}

/** Plain-text file for the customer or the records. CRLF so old Notepad shows it right. */
export function keysFileText(info: KeysFileInfo, keys: KeyLine[]): string {
  const usable = usableKeys(keys);
  const missing = keys.filter((k) => !k.key && !k.revoked).map((k) => k.seq);
  const revoked = keys.filter((k) => k.revoked).map((k) => k.seq);
  const customer = [info.customerName, info.company].filter(Boolean).join(" - ");

  const lines = ["TextSoft licence keys", "", `Serial:      ${info.serial}`];
  if (customer) lines.push(`Customer:    ${customer}`);
  if (info.edition) lines.push(`Edition:     ${info.edition === "full" ? "Full" : "Demo"}`);
  if (info.expiryUtc !== undefined) lines.push(`Valid until: ${info.expiryUtc ? fmtDate(info.expiryUtc) : "Perpetual"}`);
  lines.push(`Exported:    ${fmtDateTime(new Date().toISOString())}`, "");
  for (const k of usable) lines.push(`Key ${k.seq}: ${k.key}`);
  if (usable.length === 0) lines.push("(no key in this purchase can be shown)");
  if (missing.length) lines.push("", `Not stored, so not listed: key ${missing.join(", ")} (issued before keys were saved).`);
  if (revoked.length) lines.push("", `Revoked, so not listed: key ${revoked.join(", ")}.`);
  lines.push("", "Enter the serial and one key on each PC. Each key works on one PC.");
  return lines.join("\r\n") + "\r\n";
}

export function downloadText(filename: string, text: string): void {
  const blob = new Blob([text], { type: "text/plain;charset=utf-8" });
  const a = document.createElement("a");
  a.href = URL.createObjectURL(blob);
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}

export interface RevealedKey {
  id: string;
  seq: number;
  prefix: string;
  /** null when the key cannot be shown again (issued before keys were stored). */
  key: string | null;
  /** The key's status right now (not when the page loaded). */
  status?: "active" | "revoked";
  expired?: boolean;
}

/** POST /api/admin/orders/:idOrSerial/reveal — every key of the purchase (key null when not stored). */
export async function revealOrderKeys(idOrSerial: string): Promise<{ serial: string; keys: RevealedKey[]; missing: number }> {
  const res = await fetch(`/api/admin/orders/${encodeURIComponent(idOrSerial)}/reveal`, { method: "POST" });
  const body = await res.json().catch(() => ({}));
  if (res.ok && Array.isArray(body?.keys)) return body;
  if (res.status === 401) throw new Error("Your session has expired. Sign in again.");
  if (res.status === 404) throw new Error("This purchase no longer exists.");
  if (res.status === 429) throw new Error("Too many keys shown this hour. Try again later.");
  throw new Error("Could not fetch the keys. Try again.");
}

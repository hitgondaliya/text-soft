import { getDb } from "@/lib/mongo";
import { ok } from "@/lib/http";
import type { AuditDoc } from "@/lib/models";
import { escapeRegex } from "../_lib/keys";
import { requireOwner } from "../_lib/smtp";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

/** Which actions each filter tab shows. */
const GROUPS: Record<string, RegExp> = {
  // every time a full key left the server, or was refused for going over the limit, or was replaced
  keys: /^(key\.reveal|order\.reveal|key\.reveal_refused|order\.reveal_refused|key\.reissue|order\.resend)/,
  sales: /^(order\.|key\.)/,
  team: /^admin\./,
  settings: /^settings\./,
};

// GET /api/admin/audit — the activity log, newest first. Owner only: it shows who
// revealed which keys and every change to the team and the email settings.
//   ?group= all (default) | keys | sales | team | settings
//   ?q=     admin email, action or target (serial, "TXS-… #3", email)
//   ?limit= 1..200 (default 100)
//   200 { entries: [{ id, adminEmail, action, target, detail, createdUtc }] } · 401 · 403
export async function GET(req: Request) {
  const g = await requireOwner();
  if (g.res) return g.res;

  const url = new URL(req.url);
  const group = url.searchParams.get("group") ?? "all";
  const q = url.searchParams.get("q")?.trim();
  const limit = Math.min(200, Math.max(1, Number(url.searchParams.get("limit")) || 100));

  const filter: Record<string, unknown> = {};
  if (GROUPS[group]) filter.action = { $regex: GROUPS[group] };
  if (q) {
    const rx = escapeRegex(q);
    filter.$or = [
      { adminEmail: { $regex: rx, $options: "i" } },
      { action: { $regex: rx, $options: "i" } },
      { target: { $regex: rx, $options: "i" } },
    ];
  }

  const db = await getDb();
  const rows = await db.collection<AuditDoc>("auditLog").find(filter).sort({ createdUtc: -1 }).limit(limit).toArray();
  return ok({
    entries: rows.map((r) => ({
      id: r._id!.toHexString(),
      adminEmail: r.adminEmail,
      action: r.action,
      target: r.target ?? "",
      detail: r.detail ?? {},
      createdUtc: r.createdUtc.toISOString(),
    })),
  });
}

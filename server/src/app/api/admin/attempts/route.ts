import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { ok, fail } from "@/lib/http";
import { attemptView } from "@/lib/views";
import type { ActivationAttemptDoc } from "@/lib/models";
import { escapeRegex } from "../_lib/keys";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// GET /api/admin/attempts — every refused activation, newest first.
//   ?q=      serial / key prefix / device name / IP
//   ?reason= already_bound | serial_mismatch | invalid | revoked | expired | released
//   ?days=   only the last N days
export async function GET(req: Request) {
  if (!(await getSession())) return fail("unauthorized", 401);
  const url = new URL(req.url);
  const q = url.searchParams.get("q")?.trim();
  const reason = url.searchParams.get("reason");
  const days = Number(url.searchParams.get("days") ?? "0");

  const filter: Record<string, unknown> = {};
  if (reason && reason !== "all") filter.reason = reason;
  if (days > 0) filter.createdUtc = { $gte: new Date(Date.now() - days * 86_400_000) };
  if (q) {
    // Literal text, so "(" or "[" is a search, not a regex syntax error.
    const rx = escapeRegex(q);
    filter.$or = [
      { serial: { $regex: rx, $options: "i" } },
      { keyPrefix: { $regex: rx, $options: "i" } },
      { typedPrefix: { $regex: rx, $options: "i" } },
      { deviceName: { $regex: rx, $options: "i" } },
      { boundDeviceName: { $regex: rx, $options: "i" } },
      { ip: { $regex: rx, $options: "i" } },
    ];
  }

  const db = await getDb();
  const rows = await db
    .collection<ActivationAttemptDoc>("activationAttempts")
    .find(filter)
    .sort({ createdUtc: -1 })
    .limit(500)
    .toArray();

  return ok({ attempts: rows.map(attemptView) });
}

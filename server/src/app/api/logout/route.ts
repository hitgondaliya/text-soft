import { clearSessionCookie } from "@/lib/auth";
import { ok } from "@/lib/http";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// POST /api/logout — clear the admin session.
export async function POST() {
  clearSessionCookie();
  return ok({ status: "logged_out" });
}

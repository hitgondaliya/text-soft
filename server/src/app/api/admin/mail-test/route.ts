import { getSession } from "@/lib/auth";
import { ok, fail } from "@/lib/http";
import { mailConfigured, sendTestEmail } from "@/lib/mail";
import { env } from "@/lib/env";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// GET — is SMTP set up? POST — send a test message to the signed-in admin.
export async function GET() {
  if (!(await getSession())) return fail("unauthorized", 401);
  return ok({ configured: mailConfigured(), from: mailConfigured() ? env.mailFrom() : "" });
}

export async function POST() {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);
  const r = await sendTestEmail(session.email);
  if (!r.ok) return fail("mail_failed", 502, { detail: r.error });
  return ok({ sent: true, to: session.email });
}

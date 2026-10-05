import { getSession } from "@/lib/auth";
import { ok, fail } from "@/lib/http";
import { mailConfigured, mailFromDisplay, sendTestEmail } from "@/lib/mail";
import { getEffectiveSmtp } from "@/lib/settings";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// The original "is email working?" check, kept for older pages. It uses the same
// effective settings as everything else (Settings → Email, else .env).
// GET — is SMTP set up? POST — send a test message to the signed-in admin.
export async function GET() {
  if (!(await getSession())) return fail("unauthorized", 401);
  const smtp = await getEffectiveSmtp();
  const configured = await mailConfigured(smtp);
  return ok({ configured, from: configured ? await mailFromDisplay(smtp) : "", source: smtp.source });
}

export async function POST() {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);
  const r = await sendTestEmail(session.email);
  if (!r.ok) return fail("mail_failed", 502, { detail: r.error });
  return ok({ sent: true, to: session.email });
}

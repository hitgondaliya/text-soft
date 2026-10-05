import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import { deleteSmtpSettings, getEffectiveSmtp, normalizePassword, saveSmtpSettings, smtpView } from "@/lib/settings";
import { SmtpBody, missingCredential, requireOwner, smtpTargetProblem, zodField, type SmtpBodyInput } from "../../_lib/smtp";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// Settings → Email. Owner only. The password is write-only: it is never returned,
// only `hasPassword`; leaving it blank on save keeps the stored one.

// GET — what mail is sent with right now (panel settings, else .env).
export async function GET() {
  const g = await requireOwner();
  if (g.res) return g.res;
  return ok(smtpView(await getEffectiveSmtp(true)));
}

// PUT — save the panel's SMTP settings; from now on they override .env.
export async function PUT(req: Request) {
  const g = await requireOwner();
  if (g.res) return g.res;

  let b: SmtpBodyInput;
  try {
    b = SmtpBody.parse(await req.json());
  } catch (e) {
    return fail("bad_request", 400, { field: zodField(e) });
  }

  // A blank password keeps the stored one only for the same host + username;
  // a new server needs its own password (the stored one never goes elsewhere).
  const missing = await missingCredential(b);
  if (missing) return fail("bad_request", 400, { field: missing });
  const target = await smtpTargetProblem(b.host, b.port);
  if (target) return fail("bad_request", 400, target);

  const typed = b.password ? normalizePassword(b.password, b.host) : "";
  const { effective, passwordChanged } = await saveSmtpSettings({ ...b, password: typed || undefined }, g.session.email);
  await audit(g.session.email, "settings.smtp", "smtp", {
    host: effective.host,
    port: effective.port,
    security: effective.security,
    user: effective.user,
    fromName: effective.fromName,
    fromEmail: effective.fromEmail,
    replyTo: effective.replyTo,
    passwordChanged,
  });
  return ok(smtpView(effective));
}

// DELETE — forget the panel's settings; mail goes back to the SMTP_* values in .env.
export async function DELETE() {
  const g = await requireOwner();
  if (g.res) return g.res;
  const { effective, deleted } = await deleteSmtpSettings();
  await audit(g.session.email, "settings.smtp_reset", "smtp", { deleted, nowUsing: effective.source });
  return ok(smtpView(effective));
}

import { z } from "zod";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import { sendTestEmail } from "@/lib/mail";
import { configFromInput } from "@/lib/settings";
import { SmtpBody, missingCredential, requireOwner, smtpTargetProblem, zodField } from "../../../_lib/smtp";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const Body = z.object({
  /** Defaults to the signed-in owner. */
  to: z.string().trim().email().max(254).optional(),
  /** Unsaved form values to try before saving (blank password = the stored one). */
  config: SmtpBody.optional(),
});

// POST /api/admin/settings/smtp/test — send a short test message.
// With `config` it tries those unsaved values; without, the saved/effective ones.
//   200 { ok: true, to } · 400 bad_request { field } · 502 mail_failed { detail }
// A blank config password means the stored one, for the same host + username only.
export async function POST(req: Request) {
  const g = await requireOwner();
  if (g.res) return g.res;

  let b: z.infer<typeof Body>;
  try {
    b = Body.parse(await req.json());
  } catch (e) {
    return fail("bad_request", 400, { field: zodField(e) });
  }

  if (b.config) {
    const missing = await missingCredential(b.config);
    if (missing) return fail("bad_request", 400, { field: `config.${missing}` });
    const target = await smtpTargetProblem(b.config.host, b.config.port);
    if (target) return fail("bad_request", 400, { field: `config.${target.field}`, detail: target.detail });
  }

  const to = b.to || g.session.email;
  const override = b.config ? await configFromInput(b.config) : undefined;
  const r = await sendTestEmail(to, override);

  await audit(g.session.email, "settings.smtp_test", "smtp", {
    to,
    ok: r.ok,
    unsaved: !!b.config,
    ...(override ? { host: override.host, port: override.port, security: override.security } : {}),
    ...(r.ok ? {} : { error: r.error }),
  });
  if (!r.ok) return fail("mail_failed", 502, { detail: r.error });
  return ok({ ok: true, to });
}

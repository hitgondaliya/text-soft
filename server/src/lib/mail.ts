import nodemailer from "nodemailer";
import type SMTPTransport from "nodemailer/lib/smtp-transport";
import { env } from "./env";
import { formatPhone } from "./phone";
import { getEffectiveSmtp, smtpReady, type SmtpConfig } from "./settings";

// Outgoing mail. The SMTP server comes from Settings → Email in the panel when an
// owner has saved it there, otherwise from SMTP_* / MAIL_* in .env (lib/settings.ts).
// When neither is complete, mail is simply skipped and the purchase still succeeds —
// the panel says so, and the keys are on screen (and can be copied again later).

/** `error` is safe to show an admin: no password, at most 300 characters. */
export type MailResult = { ok: boolean; error?: string };

/** Can mail be sent right now? (host + user + password, or a host with security "none") */
export async function mailConfigured(config?: SmtpConfig): Promise<boolean> {
  return smtpReady(config ?? (await getEffectiveSmtp()));
}

function transportOptions(c: SmtpConfig): SMTPTransport.Options {
  const base: SMTPTransport.Options = {
    host: c.host,
    port: c.port,
    // Fail in seconds, not minutes: the admin is waiting on the dialog.
    connectionTimeout: 15_000,
    greetingTimeout: 15_000,
    socketTimeout: 30_000,
    ...(c.user ? { auth: { user: c.user, pass: c.pass } } : {}),
  };
  switch (c.security) {
    case "ssl":
      return { ...base, secure: true };
    case "starttls":
      // .env keeps its old behaviour (upgrade when offered); the panel's choice is strict.
      return c.opportunisticTls ? { ...base, secure: false } : { ...base, secure: false, requireTLS: true };
    case "none":
      return { ...base, secure: false, ignoreTLS: true };
  }
}

function transporter(c: SmtpConfig) {
  return nodemailer.createTransport(transportOptions(c));
}

function fromHeader(c: SmtpConfig): { name: string; address: string } | string {
  const name = c.fromName.trim() || "TextSoft";
  return c.fromEmail ? { name, address: c.fromEmail } : name;
}

function fromDisplay(c: SmtpConfig): string {
  const name = c.fromName.trim() || "TextSoft";
  return c.fromEmail ? `${name} <${c.fromEmail}>` : name;
}

/** Where a customer should write for help: reply-to, else the sender address. */
function helpAddress(c: SmtpConfig): string {
  return c.replyTo || c.fromEmail || env.mailReplyTo();
}

/** An error message for the admin, with the credentials scrubbed out and the length capped. */
function cleanError(e: unknown, c: SmtpConfig): string {
  let msg = e instanceof Error ? e.message : String(e);
  const secrets = [c.pass, c.pass && Buffer.from(c.pass).toString("base64"), c.pass && Buffer.from(`\u0000${c.user}\u0000${c.pass}`).toString("base64")];
  for (const s of secrets) if (s && s.length >= 3) msg = msg.split(s).join("***");
  msg = msg.replace(/\s+/g, " ").trim();
  // "Invalid greeting. response=<whatever answered>": a non-mail service's banner is
  // not the admin's business (and would make the test a service-banner reader).
  msg = msg.replace(/\s*response=.*$/i, "").trim();
  return msg.length > 300 ? msg.slice(0, 297) + "..." : msg || "Unknown mail error";
}

/** The effective config for display: "TextSoft <office@…>". */
export async function mailFromDisplay(config?: SmtpConfig): Promise<string> {
  return fromDisplay(config ?? (await getEffectiveSmtp()));
}

export interface PurchaseMail {
  to: string;
  customerName: string;
  company?: string;
  phoneE164?: string;
  serial: string;
  edition: "demo" | "full";
  expiryUtc: Date | null;
  quantity: number;
  keys: Array<{ seq: number; key: string }>;
  note?: string;
}

const BRAND = {
  orange: "#EA580C",
  orangeDeep: "#9A3412",
  orangeLight: "#FDECE1",
  ink: "#24262B",
  muted: "#6B7280",
  line: "#E7E5E2",
  ground: "#F6F5F3",
};

function esc(s: string): string {
  return s.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]!);
}

function fmtDate(d: Date): string {
  return d.toLocaleDateString("en-IN", { day: "numeric", month: "long", year: "numeric", timeZone: "Asia/Kolkata" });
}

/**
 * The premium-looking purchase email: serial, validity, every key, how to install.
 * `helpEmail` is the address the footer points to (the effective reply-to); it
 * defaults to MAIL_REPLY_TO from .env so the preview works without a database.
 */
export function renderPurchaseEmail(
  m: PurchaseMail,
  opts: { helpEmail?: string } = {},
): { subject: string; html: string; text: string } {
  const help = opts.helpEmail ?? env.mailReplyTo();
  const base = env.appBaseUrl();
  const logo = `${base}/logo.png`;
  const validity = m.expiryUtc ? `Valid until ${fmtDate(m.expiryUtc)}` : "Perpetual — never expires";
  const editionLabel = m.edition === "full" ? "Full licence" : "Demo licence";
  const many = m.keys.length > 1;

  const keyRows = m.keys
    .map(
      (k) => `
        <tr>
          <td style="padding:10px 14px;border-bottom:1px solid ${BRAND.line};font:13px/1.4 Arial,sans-serif;color:${BRAND.muted};width:44px;">#${k.seq}</td>
          <td style="padding:10px 14px;border-bottom:1px solid ${BRAND.line};font:600 15px/1.4 'Courier New',Courier,monospace;color:${BRAND.ink};letter-spacing:0.5px;">${esc(k.key)}</td>
        </tr>`,
    )
    .join("");

  const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Your TextSoft licence · ${esc(m.serial)}</title></head>
<body style="margin:0;padding:0;background:${BRAND.ground};">
<table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:${BRAND.ground};padding:32px 12px;">
<tr><td align="center">
<table role="presentation" width="600" cellspacing="0" cellpadding="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:16px;overflow:hidden;border:1px solid ${BRAND.line};">

  <!-- header -->
  <tr><td style="background:${BRAND.ink};padding:28px 32px;">
    <table role="presentation" cellspacing="0" cellpadding="0"><tr>
      <td style="padding-right:14px;"><img src="${logo}" width="44" height="44" alt="TextSoft" style="display:block;border-radius:12px;background:#fff;"></td>
      <td>
        <div style="font:700 20px/1.2 Arial,sans-serif;color:#ffffff;">Text<span style="color:${BRAND.orange};">Soft</span></div>
        <div style="font:600 11px/1.4 Arial,sans-serif;color:${BRAND.orange};letter-spacing:2px;text-transform:uppercase;">Beyond Typing</div>
      </td>
    </tr></table>
  </td></tr>

  <!-- hero -->
  <tr><td style="padding:36px 32px 8px;">
    <div style="font:600 12px/1.4 Arial,sans-serif;color:${BRAND.orange};letter-spacing:2px;text-transform:uppercase;">Your licence is ready</div>
    <h1 style="margin:10px 0 12px;font:700 26px/1.25 Arial,sans-serif;color:${BRAND.ink};">Thank you, ${esc(m.customerName)}.</h1>
    <p style="margin:0;font:15px/1.6 Arial,sans-serif;color:${BRAND.muted};">
      Here ${many ? "are your licence keys" : "is your licence key"} for TextSoft. Keep this email safe — setup asks for the serial number and a key.
    </p>
  </td></tr>

  <!-- serial card -->
  <tr><td style="padding:24px 32px 8px;">
    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:${BRAND.orangeLight};border-radius:12px;">
      <tr>
        <td style="padding:18px 20px;">
          <div style="font:600 11px/1.4 Arial,sans-serif;color:${BRAND.orangeDeep};letter-spacing:1.5px;text-transform:uppercase;">Serial number</div>
          <div style="font:700 24px/1.3 'Courier New',Courier,monospace;color:${BRAND.orangeDeep};letter-spacing:1px;">${esc(m.serial)}</div>
        </td>
        <td style="padding:18px 20px;text-align:right;vertical-align:top;">
          <div style="display:inline-block;padding:6px 12px;border-radius:999px;background:${BRAND.orange};font:700 12px/1 Arial,sans-serif;color:#fff;">${editionLabel}</div>
          <div style="margin-top:8px;font:600 13px/1.4 Arial,sans-serif;color:${BRAND.orangeDeep};">${validity}</div>
        </td>
      </tr>
    </table>
  </td></tr>

  <!-- keys -->
  <tr><td style="padding:20px 32px 8px;">
    <div style="font:600 11px/1.4 Arial,sans-serif;color:${BRAND.muted};letter-spacing:1.5px;text-transform:uppercase;margin-bottom:8px;">
      ${m.keys.length} licence key${many ? "s" : ""} · one key works on one PC
    </div>
    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border:1px solid ${BRAND.line};border-radius:12px;overflow:hidden;">
      ${keyRows}
    </table>
  </td></tr>

  <!-- steps -->
  <tr><td style="padding:24px 32px 8px;">
    <div style="font:700 15px/1.4 Arial,sans-serif;color:${BRAND.ink};margin-bottom:12px;">How to activate</div>
    <table role="presentation" cellspacing="0" cellpadding="0" width="100%">
      ${[
        "Run the TextSoft setup on the PC that will use the licence.",
        `Enter the serial number <b>${esc(m.serial)}</b> and one licence key when setup asks.`,
        "Open TextSoft, fill in your name, email and mobile number, and start typing.",
        "Each key is bound to the PC it is activated on. To move a key to a new PC, contact us with the serial number.",
      ]
        .map(
          (t, i) => `
      <tr>
        <td style="width:32px;vertical-align:top;padding:0 0 12px;"><div style="width:24px;height:24px;border-radius:12px;background:${BRAND.orange};color:#fff;font:700 12px/24px Arial,sans-serif;text-align:center;">${i + 1}</div></td>
        <td style="vertical-align:top;padding:2px 0 12px 8px;font:14px/1.6 Arial,sans-serif;color:${BRAND.ink};">${t}</td>
      </tr>`,
        )
        .join("")}
    </table>
  </td></tr>

  <!-- details -->
  <tr><td style="padding:12px 32px 28px;">
    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:${BRAND.ground};border-radius:12px;">
      <tr><td style="padding:16px 20px;font:13px/1.8 Arial,sans-serif;color:${BRAND.muted};">
        <b style="color:${BRAND.ink};">Purchase details</b><br>
        Customer: ${esc(m.customerName)}${m.company ? ` · ${esc(m.company)}` : ""}<br>
        ${m.phoneE164 ? `Mobile: ${esc(formatPhone(m.phoneE164))}<br>` : ""}
        Edition: ${editionLabel} · Keys: ${m.quantity} · ${validity}
        ${m.note ? `<br>Reference: ${esc(m.note)}` : ""}
      </td></tr>
    </table>
  </td></tr>

  <!-- footer -->
  <tr><td style="background:${BRAND.ink};padding:20px 32px;">
    <div style="font:12px/1.7 Arial,sans-serif;color:#A0A4AB;">
      Need help? Reply to this email${help ? ` or write to <a href="mailto:${esc(help)}" style="color:${BRAND.orange};text-decoration:none;">${esc(help)}</a>` : ""}.<br>
      © ${new Date().getFullYear()} TextSoft · Beyond Typing
    </div>
  </td></tr>
</table>
</td></tr></table>
</body></html>`;

  const text = [
    `TextSoft — Your licence is ready`,
    ``,
    `Thank you, ${m.customerName}.`,
    ``,
    `Serial number: ${m.serial}`,
    `Edition: ${editionLabel}`,
    `${validity}`,
    ``,
    `Licence key${many ? "s" : ""} (one key works on one PC):`,
    ...m.keys.map((k) => `  #${k.seq}  ${k.key}`),
    ``,
    `How to activate:`,
    `1. Run the TextSoft setup on the PC that will use the licence.`,
    `2. Enter the serial number and one licence key when setup asks.`,
    `3. Open TextSoft, fill in your name, email and mobile number, and start typing.`,
    `4. Each key is bound to the PC it is activated on. To move a key, contact us with the serial number.`,
    ``,
    help ? `Help: ${help}` : `Help: reply to this email`,
  ].join("\n");

  return { subject: `Your TextSoft licence · ${m.serial}`, html, text };
}

/** "SMTP is not configured" in words an admin can act on. */
function notConfiguredReason(c: SmtpConfig): string {
  if (!c.host) return "SMTP is not configured: no server (host) set.";
  if (!c.fromEmail) return "SMTP is not configured: no sender address set.";
  if (c.security !== "none" && !c.user) return "SMTP is not configured: no username set.";
  if (c.security !== "none" && !c.pass) return "SMTP is not configured: no password set.";
  return "SMTP is not configured.";
}

export async function sendPurchaseEmail(m: PurchaseMail, config?: SmtpConfig): Promise<MailResult> {
  const c = config ?? (await getEffectiveSmtp());
  if (!smtpReady(c)) return { ok: false, error: notConfiguredReason(c) };
  const { subject, html, text } = renderPurchaseEmail(m, { helpEmail: helpAddress(c) });
  try {
    await transporter(c).sendMail({
      from: fromHeader(c),
      to: m.to,
      ...(c.replyTo ? { replyTo: c.replyTo } : {}),
      subject,
      html,
      text,
    });
    return { ok: true };
  } catch (e) {
    return { ok: false, error: cleanError(e, c) };
  }
}

/**
 * A short branded test message so an admin can confirm SMTP. With `override` it
 * uses those (unsaved) settings, otherwise the effective ones.
 */
export async function sendTestEmail(to: string, override?: SmtpConfig): Promise<MailResult> {
  const c = override ?? (await getEffectiveSmtp());
  if (!smtpReady(c)) return { ok: false, error: notConfiguredReason(c) };
  const base = env.appBaseUrl();
  const from = fromDisplay(c);
  const server = `${c.host}:${c.port} (${c.security === "ssl" ? "SSL/TLS" : c.security === "starttls" ? "STARTTLS" : "no encryption"})`;
  try {
    await transporter(c).sendMail({
      from: fromHeader(c),
      to,
      ...(c.replyTo ? { replyTo: c.replyTo } : {}),
      subject: "TextSoft licence panel — email is working",
      text: `This is a test message from the TextSoft licence panel at ${base}.\nPurchase emails will be sent from ${from} through ${server}.`,
      html: `<p style="font:15px/1.6 Arial,sans-serif;color:#24262B;">This is a test message from the <b>TextSoft</b> licence panel at <a href="${esc(base)}">${esc(base)}</a>.<br>Purchase emails will be sent from <b>${esc(from)}</b> through ${esc(server)}.</p>`,
    });
    return { ok: true };
  } catch (e) {
    return { ok: false, error: cleanError(e, c) };
  }
}

/** Connect and log in without sending anything — "are these settings right?". */
export async function verifySmtp(override?: SmtpConfig): Promise<MailResult> {
  const c = override ?? (await getEffectiveSmtp());
  if (!smtpReady(c)) return { ok: false, error: notConfiguredReason(c) };
  const t = transporter(c);
  try {
    await t.verify();
    return { ok: true };
  } catch (e) {
    return { ok: false, error: cleanError(e, c) };
  } finally {
    t.close();
  }
}

import { getDb } from "./mongo";
import { env } from "./env";
import { sealSecret, openSecret } from "./vault";
import type { SmtpSecurity, SmtpSettingsDoc } from "./models";

// Outgoing-mail settings. Two sources, in this order:
//   1. the `settings` document `_id: "smtp"`, saved from Settings → Email (owner only);
//   2. the SMTP_* / MAIL_* values in .env — exactly the behaviour before the panel
//      could edit them (TLS from the start on port 465, otherwise upgrade if offered).
// The effective config is cached in-process for a few seconds and dropped on every
// save/delete, so a purchase right after a settings change uses the new values.

export interface SmtpConfig {
  host: string;
  port: number;
  security: SmtpSecurity;
  user: string;
  pass: string;
  fromName: string;
  fromEmail: string;
  replyTo: string;
  /** .env only: upgrade to TLS when the server offers it but do not insist (the old behaviour). */
  opportunisticTls?: boolean;
}

export type SmtpSource = "db" | "env" | "none";

export interface EffectiveSmtp extends SmtpConfig {
  source: SmtpSource;
  /** A password is stored but the vault cannot open it (its secret changed) — type it again. */
  passUnreadable?: boolean;
  updatedBy?: string;
  updatedUtc?: Date;
}

/** What the settings form sends (PUT body). A blank password keeps the current one. */
export interface SmtpInput {
  host: string;
  port: number;
  security: SmtpSecurity;
  user: string;
  password?: string;
  fromName: string;
  fromEmail: string;
  replyTo?: string;
}

/** The GET /api/admin/settings/smtp shape. Never carries the password. */
export interface SmtpView {
  configured: boolean;
  source: SmtpSource;
  host: string;
  port: number;
  security: SmtpSecurity;
  user: string;
  hasPassword: boolean;
  fromName: string;
  fromEmail: string;
  replyTo: string;
  updatedBy?: string;
  updatedUtc?: string;
}

const CACHE_MS = 30_000;
const DEFAULT_FROM_NAME = "TextSoft";

declare global {
  // eslint-disable-next-line no-var
  var __textsoftSmtpCache: { value: EffectiveSmtp; at: number } | undefined;
}

/** Enough to send: a host, and credentials unless it is a plain local relay. */
export function smtpReady(c: SmtpConfig): boolean {
  if (!c.host || !Number.isFinite(c.port) || c.port <= 0) return false;
  if (!c.fromEmail) return false;
  if (c.security === "none") return true;
  return !!(c.user && c.pass);
}

/** "TextSoft <a@b.c>" → { name, email }; a bare address keeps the default name. */
export function parseMailbox(s: string): { name: string; email: string } {
  const m = /^\s*"?([^"<]*?)"?\s*<\s*([^>\s]+)\s*>\s*$/.exec(s);
  if (m) return { name: m[1].trim(), email: m[2].trim() };
  return { name: DEFAULT_FROM_NAME, email: s.trim() };
}

/** Google shows app passwords in groups of four with spaces; anything else is kept as typed. */
export function normalizePassword(password: string, host: string): string {
  const p = password.trim();
  return /(^|\.)(gmail|googlemail|google)\.com$/i.test(host.trim()) ? p.replace(/\s+/g, "") : p;
}

function fromEnv(): EffectiveSmtp {
  const port = env.smtpPort();
  const from = parseMailbox(env.mailFrom());
  const user = env.smtpUser();
  const pass = env.smtpPass();
  return {
    source: user && pass ? "env" : "none",
    host: env.smtpHost(),
    port,
    security: port === 465 ? "ssl" : "starttls",
    opportunisticTls: port !== 465,
    user,
    pass,
    fromName: from.name,
    fromEmail: from.email,
    replyTo: env.mailReplyTo(),
  };
}

function fromDoc(doc: SmtpSettingsDoc): EffectiveSmtp {
  const pass = doc.passEnc ? openSecret(doc.passEnc) : "";
  return {
    source: "db",
    host: doc.host,
    port: doc.port,
    security: doc.security,
    user: doc.user ?? "",
    pass: pass ?? "",
    passUnreadable: !!doc.passEnc && pass === null,
    fromName: doc.fromName ?? "",
    fromEmail: doc.fromEmail,
    replyTo: doc.replyTo ?? "",
    updatedBy: doc.updatedBy,
    updatedUtc: doc.updatedUtc,
  };
}

async function readDoc(): Promise<SmtpSettingsDoc | null> {
  const db = await getDb();
  return db.collection<SmtpSettingsDoc>("settings").findOne({ _id: "smtp" });
}

export function invalidateSmtpCache(): void {
  global.__textsoftSmtpCache = undefined;
}

/** The config mail is sent with right now: the panel's settings, else .env. */
export async function getEffectiveSmtp(fresh = false): Promise<EffectiveSmtp> {
  const c = global.__textsoftSmtpCache;
  if (!fresh && c && Date.now() - c.at < CACHE_MS) return c.value;
  const doc = await readDoc();
  const value = doc ? fromDoc(doc) : fromEnv();
  global.__textsoftSmtpCache = { value, at: Date.now() };
  return value;
}

export function smtpView(e: EffectiveSmtp): SmtpView {
  return {
    configured: smtpReady(e),
    source: e.source,
    host: e.host,
    port: e.port,
    security: e.security,
    user: e.user,
    hasPassword: !!e.pass,
    fromName: e.fromName,
    fromEmail: e.fromEmail,
    replyTo: e.replyTo,
    ...(e.updatedBy ? { updatedBy: e.updatedBy } : {}),
    ...(e.updatedUtc ? { updatedUtc: e.updatedUtc.toISOString() } : {}),
  };
}

/**
 * The stored password, but only for the server and account it belongs to. A blank
 * password on the form means "keep the stored one" — as long as host and username
 * are unchanged. Pointing the settings at another server needs that server's
 * password typed in, so the stored one is never handed to a host it was not meant for.
 */
export async function storedPasswordFor(host: string, user: string): Promise<string> {
  const e = await getEffectiveSmtp(true);
  const sameServer = e.host.trim().toLowerCase() === host.trim().toLowerCase() && e.user.trim() === user.trim();
  return sameServer ? e.pass : "";
}

/** A config built from form input; a blank password means the stored one (same server only). */
export async function configFromInput(input: SmtpInput): Promise<SmtpConfig> {
  const typed = input.password ? normalizePassword(input.password, input.host) : "";
  const pass = typed || (await storedPasswordFor(input.host, input.user));
  return {
    host: input.host.trim(),
    port: input.port,
    security: input.security,
    user: input.user.trim(),
    pass,
    fromName: input.fromName.trim(),
    fromEmail: input.fromEmail.trim(),
    replyTo: (input.replyTo ?? "").trim(),
  };
}

/**
 * Save the panel's SMTP settings (they then override .env). A blank password keeps
 * the stored one when host and username are unchanged — on the first save that is
 * the .env password, so changing only the sender name does not silently break mail.
 */
export async function saveSmtpSettings(input: SmtpInput, by: string): Promise<{ effective: EffectiveSmtp; passwordChanged: boolean }> {
  const db = await getDb();
  const col = db.collection<SmtpSettingsDoc>("settings");
  const existing = await col.findOne({ _id: "smtp" });

  const typed = input.password ? normalizePassword(input.password, input.host) : "";
  let passEnc: string | undefined;
  if (typed) {
    passEnc = sealSecret(typed);
  } else {
    const sameServer = (h: string, u: string) => h.trim().toLowerCase() === input.host.trim().toLowerCase() && u.trim() === input.user.trim();
    if (existing) {
      // Keep the sealed value as it is (even one this server cannot open right now).
      if (existing.passEnc && sameServer(existing.host, existing.user ?? "")) passEnc = existing.passEnc;
    } else {
      const env0 = fromEnv();
      if (env0.pass && sameServer(env0.host, env0.user)) passEnc = sealSecret(env0.pass);
    }
  }

  const doc: SmtpSettingsDoc = {
    _id: "smtp",
    host: input.host.trim(),
    port: input.port,
    security: input.security,
    user: input.user.trim(),
    fromName: input.fromName.trim(),
    fromEmail: input.fromEmail.trim(),
    updatedBy: by,
    updatedUtc: new Date(),
  };
  if (passEnc) doc.passEnc = passEnc;
  const replyTo = (input.replyTo ?? "").trim();
  if (replyTo) doc.replyTo = replyTo;

  await col.replaceOne({ _id: "smtp" }, doc, { upsert: true });
  invalidateSmtpCache();
  return { effective: await getEffectiveSmtp(true), passwordChanged: !!typed };
}

/** Forget the panel's SMTP settings; mail falls back to the .env values. */
export async function deleteSmtpSettings(): Promise<{ effective: EffectiveSmtp; deleted: boolean }> {
  const db = await getDb();
  const res = await db.collection<SmtpSettingsDoc>("settings").deleteOne({ _id: "smtp" });
  invalidateSmtpCache();
  return { effective: await getEffectiveSmtp(true), deleted: res.deletedCount > 0 };
}

/** May the form leave the password blank for this server and account? */
export async function hasUsablePassword(host: string, user: string): Promise<boolean> {
  return !!(await storedPasswordFor(host, user));
}

// Centralised env access. Throws early and loudly if a required secret is missing,
// so a misconfigured deploy fails at boot rather than at first activation.

function required(name: string): string {
  const v = process.env[name];
  if (!v || v.trim() === "") {
    throw new Error(`Missing required env var: ${name}`);
  }
  return v;
}

function optional(name: string, fallback: string): string {
  const v = process.env[name];
  return v && v.trim() !== "" ? v : fallback;
}

/** The port the panel listens on. Production (pm2, nginx) and `npm run dev:db` both use 3100. */
const DEFAULT_PORT = "3100";

export const env = {
  mongoUri: () => required("MONGODB_URI"),
  mongoDb: () => optional("MONGODB_DB", "textsoft_licenses"),
  privateKeyB64: () => required("LICENSE_PRIVATE_KEY"),
  publicKeyB64: () => optional("LICENSE_PUBLIC_KEY", ""),
  cookieSecret: () => required("COOKIE_SECRET"),
  adminEmail: () => optional("ADMIN_EMAIL", "admin@textsoft.local"),
  adminPassword: () => optional("ADMIN_PASSWORD", ""),
  offlineGraceDays: () => Number(optional("OFFLINE_GRACE_DAYS", "5")),
  heartbeatHours: () => Number(optional("HEARTBEAT_HOURS", "24")),

  /**
   * PORT — informational inside the app. The listener itself is chosen before the
   * app loads: ecosystem.config.cjs passes `next start -p $PORT` (PORT from the
   * environment, else from .env, else 3100) and nginx proxies to 127.0.0.1:$PORT.
   */
  port: () => Number(optional("PORT", DEFAULT_PORT)),

  /**
   * KEY_VAULT_SECRET — optional. Encrypts the recoverable copy of every licence key
   * and the SMTP password stored from the panel (see lib/vault.ts). When unset, the
   * vault key is derived from COOKIE_SECRET instead. Set it once and never change it.
   */
  keyVaultSecret: () => optional("KEY_VAULT_SECRET", ""),

  /** Public URL of this panel — used for the logo in emails. */
  appBaseUrl: () => optional("APP_BASE_URL", `http://localhost:${optional("PORT", DEFAULT_PORT)}`).replace(/\/+$/, ""),

  // ---- outgoing mail: the .env fallback. Settings → Email in the panel overrides these. ----
  smtpHost: () => optional("SMTP_HOST", "smtp.gmail.com"),
  smtpPort: () => Number(optional("SMTP_PORT", "465")),
  smtpUser: () => optional("SMTP_USER", ""),
  smtpPass: () => optional("SMTP_PASS", "").replace(/\s+/g, ""), // Google shows app passwords with spaces
  mailFrom: () => optional("MAIL_FROM", `TextSoft <${optional("SMTP_USER", "noreply@textsoft.local")}>`),
  mailReplyTo: () => optional("MAIL_REPLY_TO", optional("SMTP_USER", "")),
};

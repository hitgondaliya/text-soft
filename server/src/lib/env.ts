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

  /** Public URL of this panel — used for the logo in emails. */
  appBaseUrl: () => optional("APP_BASE_URL", "http://localhost:3000").replace(/\/+$/, ""),

  // ---- outgoing mail (Gmail SMTP with an app password) ----
  smtpHost: () => optional("SMTP_HOST", "smtp.gmail.com"),
  smtpPort: () => Number(optional("SMTP_PORT", "465")),
  smtpUser: () => optional("SMTP_USER", ""),
  smtpPass: () => optional("SMTP_PASS", "").replace(/\s+/g, ""), // Google shows app passwords with spaces
  mailFrom: () => optional("MAIL_FROM", `TextSoft <${optional("SMTP_USER", "noreply@textsoft.local")}>`),
  mailReplyTo: () => optional("MAIL_REPLY_TO", optional("SMTP_USER", "")),
};

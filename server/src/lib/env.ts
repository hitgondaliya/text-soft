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
};

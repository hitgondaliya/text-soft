import { cookies } from "next/headers";
import { SignJWT, jwtVerify } from "jose";
import bcrypt from "bcryptjs";
import { env } from "./env";
import { getDb } from "./mongo";
import type { AdminDoc } from "./models";

// Admin auth: bcrypt-checked credentials → a signed JWT in an HttpOnly cookie.
// No public signup. jose is used so verification works in the Edge middleware too.

const COOKIE = "txsf_admin";
const MAX_AGE = 60 * 60 * 12; // 12h

function secret(): Uint8Array {
  return new TextEncoder().encode(env.cookieSecret());
}

export interface Session {
  email: string;
  role: string;
}

export async function verifyCredentials(email: string, password: string): Promise<AdminDoc | null> {
  const db = await getDb();
  const admin = await db.collection<AdminDoc>("admins").findOne({ email: email.toLowerCase().trim() });
  if (!admin || admin.status === "disabled") return null;
  const ok = await bcrypt.compare(password, admin.passwordHash);
  if (!ok) return null;
  await db.collection<AdminDoc>("admins").updateOne({ _id: admin._id }, { $set: { lastLoginUtc: new Date() } });
  return admin;
}

export async function createSessionCookie(admin: AdminDoc): Promise<void> {
  // sv: the account's session version; getSession() refuses a cookie whose sv is out of date.
  const jwt = await new SignJWT({ email: admin.email, role: admin.role, sv: admin.sessionVersion ?? 0 })
    .setProtectedHeader({ alg: "HS256" })
    .setIssuedAt()
    .setExpirationTime(`${MAX_AGE}s`)
    .sign(secret());

  cookies().set(COOKIE, jwt, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge: MAX_AGE,
  });
}

export function clearSessionCookie(): void {
  cookies().set(COOKIE, "", { httpOnly: true, path: "/", maxAge: 0 });
}

// The cookie only proves "this browser signed in as X". Whether X may still use the
// panel, and with which role, is read from the admins collection on every request
// (cached for a few seconds), so disabling, removing or demoting someone takes effect
// at once, not when their 12-hour cookie runs out. The admin routes drop the cached
// entry whenever they change an account.
const ACCOUNT_CACHE_MS = 5_000;
type AccountRow = Pick<AdminDoc, "email" | "role" | "status" | "sessionVersion">;

declare global {
  // eslint-disable-next-line no-var
  var __textsoftAccounts: Map<string, { at: number; row: AccountRow | null }> | undefined;
}

function accountCache(): Map<string, { at: number; row: AccountRow | null }> {
  return (global.__textsoftAccounts ??= new Map());
}

/** Forget the cached account row so the next request re-reads it (after any change to it). */
export function forgetAccount(email: string): void {
  accountCache().delete(email.toLowerCase());
}

async function loadAccount(email: string): Promise<AccountRow | null> {
  const key = email.toLowerCase();
  const hit = accountCache().get(key);
  if (hit && Date.now() - hit.at < ACCOUNT_CACHE_MS) return hit.row;
  const db = await getDb();
  const row = await db
    .collection<AdminDoc>("admins")
    .findOne({ email: key }, { projection: { _id: 0, email: 1, role: 1, status: 1, sessionVersion: 1 } });
  accountCache().set(key, { at: Date.now(), row });
  return row;
}

/**
 * Read + verify the session from the incoming cookies. null if not logged in, or if
 * the account was removed, disabled, or its sessions were ended (session version moved
 * on) since this cookie was issued. The role always comes from the database, never from the cookie.
 */
export async function getSession(): Promise<Session | null> {
  const token = cookies().get(COOKIE)?.value;
  if (!token) return null;
  let email: string;
  let sv: number;
  try {
    const { payload } = await jwtVerify(token, secret());
    email = String(payload.email ?? "");
    sv = typeof payload.sv === "number" ? payload.sv : 0;
  } catch {
    return null;
  }
  if (!email) return null;
  const account = await loadAccount(email);
  if (!account || account.status === "disabled") return null;
  if ((account.sessionVersion ?? 0) !== sv) return null;
  return { email: account.email, role: account.role };
}

export function hashPassword(password: string): string {
  return bcrypt.hashSync(password, 10);
}

export const cookieName = COOKIE;

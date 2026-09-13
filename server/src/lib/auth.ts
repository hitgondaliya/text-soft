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
  const jwt = await new SignJWT({ email: admin.email, role: admin.role })
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

/** Read + verify the session from the incoming cookies. null if not logged in. */
export async function getSession(): Promise<Session | null> {
  const token = cookies().get(COOKIE)?.value;
  if (!token) return null;
  try {
    const { payload } = await jwtVerify(token, secret());
    return { email: String(payload.email), role: String(payload.role) };
  } catch {
    return null;
  }
}

export function hashPassword(password: string): string {
  return bcrypt.hashSync(password, 10);
}

export const cookieName = COOKIE;

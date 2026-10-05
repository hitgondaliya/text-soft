import { z } from "zod";
import { verifyCredentials, createSessionCookie } from "@/lib/auth";
import { ok, fail, clientIp } from "@/lib/http";
import { limitReached, recordHit, LIMITS } from "@/lib/ratelimit";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const Body = z.object({ email: z.string().email(), password: z.string().min(1) });

// POST /api/login — admin credential login. Sets the session cookie on success.
//   200 { email, role } · 400 bad_request · 401 invalid_credentials · 429 rate_limited
// Failed attempts count against two buckets: the client IP (one machine guessing
// many accounts) and the email (many machines guessing one account). Once either is
// full, even the right password waits until the window moves on.
export async function POST(req: Request) {
  let parsed;
  try {
    parsed = Body.parse(await req.json());
  } catch {
    return fail("bad_request", 400);
  }

  const ipKey = `login:ip:${clientIp(req)}`;
  const emailKey = `login:email:${parsed.email.toLowerCase().trim()}`;
  if (
    limitReached(ipKey, LIMITS.loginPerIp.limit, LIMITS.loginPerIp.windowMs) ||
    limitReached(emailKey, LIMITS.loginPerEmail.limit, LIMITS.loginPerEmail.windowMs)
  )
    return fail("rate_limited", 429);

  const admin = await verifyCredentials(parsed.email, parsed.password);
  if (!admin) {
    recordHit(ipKey);
    recordHit(emailKey);
    return fail("invalid_credentials", 401);
  }

  await createSessionCookie(admin);
  return ok({ email: admin.email, role: admin.role });
}

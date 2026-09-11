import { z } from "zod";
import { verifyCredentials, createSessionCookie } from "@/lib/auth";
import { ok, fail } from "@/lib/http";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const Body = z.object({ email: z.string().email(), password: z.string().min(1) });

// POST /api/login — admin credential login. Sets the session cookie on success.
export async function POST(req: Request) {
  let parsed;
  try {
    parsed = Body.parse(await req.json());
  } catch {
    return fail("bad_request", 400);
  }

  const admin = await verifyCredentials(parsed.email, parsed.password);
  if (!admin) return fail("invalid_credentials", 401);

  await createSessionCookie(admin);
  return ok({ email: admin.email, role: admin.role });
}

import { z } from "zod";
import { getDb } from "@/lib/mongo";
import { getSession, hashPassword } from "@/lib/auth";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import { adminView } from "@/lib/views";
import type { AdminDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// GET /api/admin/admins — everyone who can sign in to the panel.
export async function GET() {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);
  const db = await getDb();
  const rows = await db.collection<AdminDoc>("admins").find({}).sort({ createdUtc: 1 }).toArray();
  return ok({ admins: rows.map(adminView), me: session.email, canManage: session.role === "owner" });
}

const CreateBody = z.object({
  email: z.string().trim().email().max(120),
  name: z.string().trim().max(80).optional(),
  password: z.string().min(8).max(200),
  role: z.enum(["owner", "staff"]).default("staff"),
});

// POST /api/admin/admins — add a panel user. Owners only.
export async function POST(req: Request) {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);
  if (session.role !== "owner") return fail("forbidden", 403);

  let b;
  try {
    b = CreateBody.parse(await req.json());
  } catch (e) {
    const field = e instanceof z.ZodError ? e.issues[0]?.path?.[0] : undefined;
    return fail("bad_request", 400, { field });
  }

  const db = await getDb();
  const admins = db.collection<AdminDoc>("admins");
  const email = b.email.toLowerCase();
  if (await admins.findOne({ email })) return fail("exists", 409);

  const doc: AdminDoc = {
    email,
    name: b.name || undefined,
    passwordHash: hashPassword(b.password),
    role: b.role,
    status: "active",
    createdUtc: new Date(),
    createdBy: session.email,
    lastLoginUtc: null,
  };
  const res = await admins.insertOne(doc);
  await audit(session.email, "admin.create", email, { role: b.role });
  return ok(adminView({ ...doc, _id: res.insertedId }), 201);
}

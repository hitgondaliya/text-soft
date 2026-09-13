import { z } from "zod";
import { ObjectId } from "mongodb";
import { getDb } from "@/lib/mongo";
import { getSession, hashPassword, verifyCredentials } from "@/lib/auth";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import type { AdminDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const PatchBody = z.object({
  action: z.enum(["setPassword", "setRole", "disable", "enable", "rename"]),
  password: z.string().min(8).max(200).optional(),
  /** Required when a user changes their own password. */
  currentPassword: z.string().optional(),
  role: z.enum(["owner", "staff"]).optional(),
  name: z.string().trim().max(80).optional(),
});

// PATCH /api/admin/admins/:id — owners manage anyone; anyone may change their own
// password (with the current one) and name.
export async function PATCH(req: Request, { params }: { params: { id: string } }) {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);
  let _id: ObjectId;
  try {
    _id = new ObjectId(params.id);
  } catch {
    return fail("not_found", 404);
  }

  let b;
  try {
    b = PatchBody.parse(await req.json());
  } catch {
    return fail("bad_request", 400);
  }

  const db = await getDb();
  const admins = db.collection<AdminDoc>("admins");
  const target = await admins.findOne({ _id });
  if (!target) return fail("not_found", 404);

  const self = target.email === session.email;
  const owner = session.role === "owner";
  if (!owner && !self) return fail("forbidden", 403);

  switch (b.action) {
    case "setPassword": {
      if (!b.password) return fail("bad_request", 400, { field: "password" });
      if (!owner || self) {
        // Changing your own password needs the current one, owner or not.
        if (!b.currentPassword || !(await verifyCredentials(target.email, b.currentPassword)))
          return fail("wrong_password", 403);
      }
      await admins.updateOne({ _id }, { $set: { passwordHash: hashPassword(b.password) } });
      break;
    }
    case "rename":
      await admins.updateOne({ _id }, { $set: { name: b.name || undefined } });
      break;
    case "setRole":
      if (!owner) return fail("forbidden", 403);
      if (!b.role) return fail("bad_request", 400);
      if (self && b.role !== "owner") return fail("last_owner", 409);
      await admins.updateOne({ _id }, { $set: { role: b.role } });
      break;
    case "disable":
      if (!owner) return fail("forbidden", 403);
      if (self) return fail("cannot_disable_self", 409);
      await admins.updateOne({ _id }, { $set: { status: "disabled" } });
      break;
    case "enable":
      if (!owner) return fail("forbidden", 403);
      await admins.updateOne({ _id }, { $set: { status: "active" } });
      break;
  }

  await audit(session.email, `admin.${b.action}`, target.email, { role: b.role, name: b.name });
  return ok({ status: "updated" });
}

// DELETE /api/admin/admins/:id — owners only, never yourself.
export async function DELETE(_req: Request, { params }: { params: { id: string } }) {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);
  if (session.role !== "owner") return fail("forbidden", 403);
  let _id: ObjectId;
  try {
    _id = new ObjectId(params.id);
  } catch {
    return fail("not_found", 404);
  }
  const db = await getDb();
  const admins = db.collection<AdminDoc>("admins");
  const target = await admins.findOne({ _id });
  if (!target) return fail("not_found", 404);
  if (target.email === session.email) return fail("cannot_delete_self", 409);
  await admins.deleteOne({ _id });
  await audit(session.email, "admin.delete", target.email);
  return ok({ status: "deleted" });
}

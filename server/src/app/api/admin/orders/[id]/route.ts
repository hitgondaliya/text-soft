import { z } from "zod";
import { ObjectId } from "mongodb";
import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import type { ActivationDoc, LicenseKeyDoc, OrderDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

function oid(id: string): ObjectId | null {
  try {
    return new ObjectId(id);
  } catch {
    return null;
  }
}

/** Accepts either the Mongo id or the serial itself, so TXS-2026-0001 is a URL. */
async function findOrder(idOrSerial: string): Promise<OrderDoc | null> {
  const db = await getDb();
  const orders = db.collection<OrderDoc>("orders");
  const _id = oid(idOrSerial);
  if (_id) {
    const byId = await orders.findOne({ _id });
    if (byId) return byId;
  }
  return orders.findOne({ serial: idOrSerial.trim().toUpperCase() });
}

// GET /api/admin/orders/:idOrSerial — the purchase and every key under it.
export async function GET(_req: Request, { params }: { params: { id: string } }) {
  if (!(await getSession())) return fail("unauthorized", 401);

  const order = await findOrder(params.id);
  if (!order) return fail("not_found", 404);

  const db = await getDb();
  const keys = await db
    .collection<LicenseKeyDoc>("licenseKeys")
    .find({ orderId: order._id! })
    .sort({ seq: 1 })
    .toArray();

  const acts = db.collection<ActivationDoc>("activations");
  const now = new Date();

  const keyList = await Promise.all(
    keys.map(async (k) => ({
      id: k._id!.toHexString(),
      seq: k.seq,
      prefix: k.keyPrefix,
      status: k.status,
      expired: !!k.expiryUtc && now > k.expiryUtc,
      expiryUtc: k.expiryUtc ? k.expiryUtc.toISOString() : null,
      deviceLimit: k.deviceLimit,
      devicesUsed: await acts.countDocuments({ keyId: k._id!, status: "active" }),
      lastSeenUtc: k.lastSeenUtc ? k.lastSeenUtc.toISOString() : null,
    })),
  );

  return ok({
    id: order._id!.toHexString(),
    serial: order.serial,
    customerName: order.customerName,
    customerEmail: order.customerEmail ?? "",
    customerPhone: order.customerPhone ?? "",
    company: order.company ?? "",
    edition: order.edition,
    deviceLimit: order.deviceLimit,
    quantity: order.quantity,
    expiryUtc: order.expiryUtc ? order.expiryUtc.toISOString() : null,
    expired: !!order.expiryUtc && now > order.expiryUtc,
    note: order.note ?? "",
    createdBy: order.createdBy,
    createdUtc: order.createdUtc.toISOString(),
    keys: keyList,
  });
}

// Whole-purchase actions. A sale of 40 keys is refunded, extended or suspended as
// one thing; doing it key by key is how mistakes happen.
const PatchBody = z.object({
  action: z.enum(["revokeAll", "unrevokeAll", "extendAll", "setExpiryAll", "updateCustomer"]),
  days: z.number().int().min(1).max(3650).optional(),
  expiryUtc: z.string().datetime().nullable().optional(),
  customerName: z.string().min(1).max(128).optional(),
  customerEmail: z.string().email().optional().or(z.literal("")),
  customerPhone: z.string().max(40).optional(),
  company: z.string().max(128).optional(),
  note: z.string().max(500).optional(),
});

export async function PATCH(req: Request, { params }: { params: { id: string } }) {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);

  const order = await findOrder(params.id);
  if (!order) return fail("not_found", 404);

  let b;
  try {
    b = PatchBody.parse(await req.json());
  } catch {
    return fail("bad_request", 400);
  }

  const db = await getDb();
  const orders = db.collection<OrderDoc>("orders");
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");
  const scope = { orderId: order._id! };

  switch (b.action) {
    case "revokeAll":
      await keys.updateMany(scope, { $set: { status: "revoked" } });
      break;
    case "unrevokeAll":
      await keys.updateMany(scope, { $set: { status: "active" } });
      break;
    case "extendAll": {
      if (!b.days) return fail("bad_request", 400);
      // Extend from today when the purchase has already lapsed, otherwise from
      // the date it runs to — so extending twice adds up instead of overlapping.
      const base = order.expiryUtc && order.expiryUtc > new Date() ? order.expiryUtc : new Date();
      const next = new Date(base);
      next.setUTCDate(next.getUTCDate() + b.days);
      await orders.updateOne({ _id: order._id! }, { $set: { expiryUtc: next } });
      await keys.updateMany(scope, { $set: { expiryUtc: next } });
      break;
    }
    case "setExpiryAll": {
      const when = b.expiryUtc ? new Date(b.expiryUtc) : null;
      await orders.updateOne({ _id: order._id! }, { $set: { expiryUtc: when } });
      await keys.updateMany(scope, { $set: { expiryUtc: when } });
      break;
    }
    case "updateCustomer": {
      const set: Partial<OrderDoc> = {};
      if (b.customerName) set.customerName = b.customerName;
      if (b.customerEmail !== undefined) set.customerEmail = b.customerEmail || undefined;
      if (b.customerPhone !== undefined) set.customerPhone = b.customerPhone || undefined;
      if (b.company !== undefined) set.company = b.company || undefined;
      if (b.note !== undefined) set.note = b.note || undefined;
      if (Object.keys(set).length === 0) return fail("bad_request", 400);
      await orders.updateOne({ _id: order._id! }, { $set: set });
      // The keys carry a copy for searching, so keep it in step.
      await keys.updateMany(scope, {
        $set: {
          customerName: set.customerName ?? order.customerName,
          customerEmail: set.customerEmail ?? order.customerEmail,
          note: set.note ?? order.note,
        },
      });
      break;
    }
  }

  await audit(session.email, `order.${b.action}`, order.serial, { ...b });
  return ok({ status: "updated" });
}

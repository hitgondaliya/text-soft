import { z } from "zod";
import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import { parsePhone, formatPhone } from "@/lib/phone";
import { findOrder, isRecoverable } from "../../_lib/keys";
import type { ActivationAttemptDoc, ActivationDoc, LicenseKeyDoc, OrderDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

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

  const now = new Date();
  const ids = keys.map((k) => k._id!);
  const [holders, attemptCounts] = await Promise.all([
    db.collection<ActivationDoc>("activations").find({ keyId: { $in: ids }, status: "active" }).toArray(),
    db
      .collection<ActivationAttemptDoc>("activationAttempts")
      .aggregate<{ _id: string; n: number }>([
        { $match: { keyId: { $in: ids } } },
        { $group: { _id: { $toString: "$keyId" }, n: { $sum: 1 } } },
      ])
      .toArray(),
  ]);
  const holderBy = new Map(holders.map((a) => [a.keyId.toHexString(), a]));
  const attemptsBy = new Map(attemptCounts.map((c) => [c._id, c.n]));

  const keyList = keys.map((k) => {
    const id = k._id!.toHexString();
    const h = holderBy.get(id);
    return {
      id,
      seq: k.seq,
      prefix: k.keyPrefix,
      /** The full key can be revealed / re-sent (false for keys issued before keys were sealed). */
      recoverable: isRecoverable(k),
      status: k.status,
      expired: !!k.expiryUtc && now > k.expiryUtc,
      expiryUtc: k.expiryUtc ? k.expiryUtc.toISOString() : null,
      bound: !!h,
      deviceName: h?.deviceName ?? "",
      registered: !!h?.user,
      userName: h?.user?.name ?? "",
      userEmail: h?.user?.email ?? "",
      userPhone: formatPhone(h?.user?.phoneE164),
      userPhoneCountry: h?.user?.phoneCountry ?? "",
      attempts: attemptsBy.get(id) ?? 0,
      lastSeenUtc: k.lastSeenUtc ? k.lastSeenUtc.toISOString() : null,
    };
  });
  const attemptsTotal = Array.from(attemptsBy.values()).reduce((a, b) => a + b, 0);

  return ok({
    id: order._id!.toHexString(),
    serial: order.serial,
    customerName: order.customerName,
    customerEmail: order.customerEmail ?? "",
    customerPhone: order.customerPhone ?? "",
    customerPhoneCountry: order.customerPhoneCountry ?? "",
    customerPhoneDisplay: formatPhone(order.customerPhoneE164) || order.customerPhone || "",
    company: order.company ?? "",
    edition: order.edition,
    attemptsTotal,
    quantity: order.quantity,
    expiryUtc: order.expiryUtc ? order.expiryUtc.toISOString() : null,
    expired: !!order.expiryUtc && now > order.expiryUtc,
    note: order.note ?? "",
    emailedUtc: order.emailedUtc ? order.emailedUtc.toISOString() : null,
    emailError: order.emailError ?? "",
    lastSentTo: order.lastSentTo ?? "",
    lastSentUtc: order.lastSentUtc ? order.lastSentUtc.toISOString() : null,
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
  customerPhoneCountry: z.string().length(2).optional(),
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
      if (b.customerPhone !== undefined) {
        if (b.customerPhone.trim()) {
          const phone = parsePhone(b.customerPhoneCountry ?? order.customerPhoneCountry ?? "IN", b.customerPhone);
          if (!phone) return fail("bad_request", 400, { field: "customerPhone" });
          set.customerPhone = phone.national;
          set.customerPhoneCountry = phone.country;
          set.customerPhoneE164 = phone.e164;
        } else {
          set.customerPhone = undefined;
          set.customerPhoneCountry = undefined;
          set.customerPhoneE164 = undefined;
        }
      }
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

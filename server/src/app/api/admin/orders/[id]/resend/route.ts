import { z } from "zod";
import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import { mailConfigured, sendPurchaseEmail } from "@/lib/mail";
import { getEffectiveSmtp } from "@/lib/settings";
import { findOrder, revealKey } from "../../../_lib/keys";
import type { LicenseKeyDoc, OrderDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const Body = z.object({
  /** Defaults to the purchase's customer email. */
  to: z.string().trim().email().max(254).optional().or(z.literal("")),
});

// POST /api/admin/orders/:idOrSerial/resend — send the purchase email again with
// every key that can be recovered. Revoked keys are left out (and counted), keys
// issued before keys were sealed cannot be included (counted in `missing`).
//   200 { sent: true, to, included, missing, revoked }
//   400 no_recipient | bad_request · 404 not_found · 409 no_recoverable_keys
//   503 mail_not_configured · 502 mail_failed { detail }
export async function POST(req: Request, { params }: { params: { id: string } }) {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);

  let b: z.infer<typeof Body> = {};
  const raw = await req.text();
  if (raw.trim()) {
    // Only JSON. A text/plain body is what a cross-site form or no-cors fetch can send.
    if (!req.headers.get("content-type")?.toLowerCase().includes("application/json"))
      return fail("unsupported_media_type", 415);
    try {
      b = Body.parse(JSON.parse(raw));
    } catch {
      return fail("bad_request", 400, { field: "to" });
    }
  }

  const order = await findOrder(params.id);
  if (!order) return fail("not_found", 404);

  const to = (b.to || order.customerEmail || "").trim();
  if (!to) return fail("no_recipient", 400);

  const db = await getDb();
  const rows = await db.collection<LicenseKeyDoc>("licenseKeys").find({ orderId: order._id! }).sort({ seq: 1 }).toArray();
  const live = rows.filter((k) => k.status !== "revoked");
  const revoked = rows.length - live.length;
  const keys: Array<{ seq: number; key: string }> = [];
  for (const k of live) {
    const plain = revealKey(k);
    if (plain) keys.push({ seq: k.seq, key: plain });
  }
  const missing = live.length - keys.length;
  if (keys.length === 0) return fail("no_recoverable_keys", 409, { missing, revoked });

  const smtp = await getEffectiveSmtp();
  if (!(await mailConfigured(smtp))) return fail("mail_not_configured", 503);

  const r = await sendPurchaseEmail(
    {
      to,
      customerName: order.customerName,
      company: order.company,
      phoneE164: order.customerPhoneE164,
      serial: order.serial,
      edition: order.edition,
      expiryUtc: order.expiryUtc,
      quantity: order.quantity,
      keys,
      note: order.note,
    },
    smtp,
  );

  // The purchase's delivery state (emailedUtc / emailError) is about the customer's
  // own address. A copy to anyone else is recorded as lastSentTo only.
  const toCustomer = !!order.customerEmail && to.toLowerCase() === order.customerEmail.trim().toLowerCase();
  const orders = db.collection<OrderDoc>("orders");
  if (!r.ok) {
    if (toCustomer) await orders.updateOne({ _id: order._id! }, { $set: { emailError: r.error } });
    await audit(session.email, "order.resend_failed", order.serial, { to, toCustomer, included: keys.length, missing, revoked, error: r.error });
    return fail("mail_failed", 502, { detail: r.error });
  }

  const now = new Date();
  await orders.updateOne(
    { _id: order._id! },
    toCustomer
      ? { $set: { emailedUtc: now, lastSentTo: to, lastSentUtc: now }, $unset: { emailError: "" } }
      : { $set: { lastSentTo: to, lastSentUtc: now } },
  );
  await audit(session.email, "order.resend", order.serial, { to, toCustomer, included: keys.length, missing, revoked });
  return ok({ sent: true, to, toCustomer, included: keys.length, missing, revoked });
}

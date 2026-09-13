import { z } from "zod";
import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { generateKeyString, hashKey, keyPrefix } from "@/lib/licenseKey";
import { nextSerial } from "@/lib/serial";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import type { LicenseKeyDoc, OrderDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// A purchase: one serial number, `quantity` keys issued under it. This is how
// keys are meant to be created — a sale of 40 keys is one record, not 40.
const CreateBody = z.object({
  customerName: z.string().min(1).max(128),
  customerEmail: z.string().email().optional().or(z.literal("")),
  customerPhone: z.string().max(40).optional(),
  company: z.string().max(128).optional(),
  edition: z.enum(["demo", "full"]),
  deviceLimit: z.number().int().min(1).max(100),
  quantity: z.number().int().min(1).max(500),
  // Exactly one of durationDays / expiryUtc / perpetual.
  durationDays: z.number().int().min(1).max(3650).optional(),
  expiryUtc: z.string().datetime().optional(),
  perpetual: z.boolean().optional(),
  note: z.string().max(500).optional(),
});

function computeExpiry(b: z.infer<typeof CreateBody>): Date | null {
  if (b.perpetual) return null;
  if (b.expiryUtc) return new Date(b.expiryUtc);
  if (b.durationDays) {
    const d = new Date();
    d.setUTCDate(d.getUTCDate() + b.durationDays);
    return d;
  }
  return null; // default: perpetual
}

// GET /api/admin/orders — purchases, newest first, with how many keys are in use.
export async function GET(req: Request) {
  if (!(await getSession())) return fail("unauthorized", 401);

  const url = new URL(req.url);
  const q = url.searchParams.get("q")?.trim();

  const db = await getDb();
  const orders = db.collection<OrderDoc>("orders");
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");

  const filter: Record<string, unknown> = {};
  if (q) {
    filter.$or = [
      { serial: { $regex: q, $options: "i" } },
      { customerName: { $regex: q, $options: "i" } },
      { customerEmail: { $regex: q, $options: "i" } },
      { company: { $regex: q, $options: "i" } },
    ];
  }

  const rows = await orders.find(filter).sort({ createdUtc: -1 }).limit(500).toArray();
  const now = new Date();

  const list = await Promise.all(
    rows.map(async (o) => {
      const [issued, revoked, activated] = await Promise.all([
        keys.countDocuments({ orderId: o._id! }),
        keys.countDocuments({ orderId: o._id!, status: "revoked" }),
        keys.countDocuments({ orderId: o._id!, lastSeenUtc: { $ne: null } }),
      ]);
      return {
        id: o._id!.toHexString(),
        serial: o.serial,
        customerName: o.customerName,
        customerEmail: o.customerEmail ?? "",
        customerPhone: o.customerPhone ?? "",
        company: o.company ?? "",
        edition: o.edition,
        deviceLimit: o.deviceLimit,
        quantity: o.quantity,
        issued,
        revoked,
        activated,
        expiryUtc: o.expiryUtc ? o.expiryUtc.toISOString() : null,
        expired: !!o.expiryUtc && now > o.expiryUtc,
        note: o.note ?? "",
        createdUtc: o.createdUtc.toISOString(),
      };
    }),
  );

  return ok({ orders: list });
}

// POST /api/admin/orders — record a purchase and issue its keys.
// Returns the RAW key strings ONCE; only their hashes are stored.
export async function POST(req: Request) {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);

  let b;
  try {
    b = CreateBody.parse(await req.json());
  } catch {
    return fail("bad_request", 400);
  }

  const expiryUtc = computeExpiry(b);
  const db = await getDb();
  const orders = db.collection<OrderDoc>("orders");
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");

  const serial = await nextSerial(db);
  const createdUtc = new Date();

  const order: OrderDoc = {
    serial,
    customerName: b.customerName,
    customerEmail: b.customerEmail || undefined,
    customerPhone: b.customerPhone || undefined,
    company: b.company || undefined,
    edition: b.edition,
    expiryUtc,
    deviceLimit: b.deviceLimit,
    quantity: b.quantity,
    note: b.note,
    createdBy: session.email,
    createdUtc,
  };
  const orderRes = await orders.insertOne(order);
  const orderId = orderRes.insertedId;

  const docs: LicenseKeyDoc[] = [];
  const created: Array<{ seq: number; key: string; prefix: string }> = [];
  for (let i = 1; i <= b.quantity; i++) {
    // Retry on the astronomically-unlikely hash collision.
    let keyString = generateKeyString();
    let hash = hashKey(keyString);
    while (await keys.findOne({ keyHash: hash })) {
      keyString = generateKeyString();
      hash = hashKey(keyString);
    }
    docs.push({
      keyHash: hash,
      keyPrefix: keyPrefix(keyString),
      orderId,
      serial,
      seq: i,
      edition: b.edition,
      expiryUtc,
      deviceLimit: b.deviceLimit,
      status: "active",
      customerName: b.customerName,
      customerEmail: b.customerEmail || undefined,
      note: b.note,
      createdBy: session.email,
      createdUtc,
      lastSeenUtc: null,
    });
    created.push({ seq: i, key: keyString, prefix: keyPrefix(keyString) });
  }

  try {
    await keys.insertMany(docs, { ordered: true });
  } catch (e) {
    // Leaving an order with no keys behind would show a sale that cannot be
    // fulfilled, so take the order back out with it.
    await orders.deleteOne({ _id: orderId });
    await keys.deleteMany({ orderId });
    return fail("key_issue_failed", 500, { detail: String(e) });
  }

  await audit(session.email, "order.create", serial, {
    customer: b.customerName,
    quantity: b.quantity,
    edition: b.edition,
    deviceLimit: b.deviceLimit,
    expiryUtc: expiryUtc ? expiryUtc.toISOString() : "perpetual",
  });

  return ok({ orderId: orderId.toHexString(), serial, created }, 201);
}

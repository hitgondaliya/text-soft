import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { ok, fail } from "@/lib/http";
import { formatPhone } from "@/lib/phone";
import type { LicenseKeyDoc, ActivationDoc, ActivationAttemptDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// GET /api/admin/keys — list with optional status/search filter.
export async function GET(req: Request) {
  if (!(await getSession())) return fail("unauthorized", 401);

  const url = new URL(req.url);
  const status = url.searchParams.get("status"); // active|revoked|expired|unused|bound|attempts
  const q = url.searchParams.get("q")?.trim();

  const db = await getDb();
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");

  const filter: Record<string, unknown> = {};
  if (status === "revoked") filter.status = "revoked";
  if (status === "active") filter.status = "active";
  if (q) {
    filter.$or = [
      { keyPrefix: { $regex: q, $options: "i" } },
      { serial: { $regex: q, $options: "i" } },
      { customerName: { $regex: q, $options: "i" } },
      { customerEmail: { $regex: q, $options: "i" } },
      { userName: { $regex: q, $options: "i" } },
      { userEmail: { $regex: q, $options: "i" } },
      { userPhoneE164: { $regex: q.replace(/[^\d+]/g, "") || q, $options: "i" } },
    ];
  }

  const rows = await keys.find(filter).sort({ createdUtc: -1 }).limit(500).toArray();
  const ids = rows.map((k) => k._id!);
  const acts = db.collection<ActivationDoc>("activations");
  const attempts = db.collection<ActivationAttemptDoc>("activationAttempts");
  const now = new Date();

  // Two queries instead of two per row.
  const [holders, attemptCounts] = await Promise.all([
    acts.find({ keyId: { $in: ids }, status: "active" }).toArray(),
    attempts
      .aggregate<{ _id: string; n: number }>([
        { $match: { keyId: { $in: ids } } },
        { $group: { _id: { $toString: "$keyId" }, n: { $sum: 1 } } },
      ])
      .toArray(),
  ]);
  const holderBy = new Map(holders.map((a) => [a.keyId.toHexString(), a]));
  const attemptsBy = new Map(attemptCounts.map((c) => [c._id, c.n]));

  const list = rows.map((k) => {
    const id = k._id!.toHexString();
    const h = holderBy.get(id);
    const expired = !!k.expiryUtc && now > k.expiryUtc;
    return {
      id,
      prefix: k.keyPrefix,
      serial: k.serial ?? "",
      seq: k.seq ?? 0,
      edition: k.edition,
      status: k.status,
      expired,
      expiryUtc: k.expiryUtc ? k.expiryUtc.toISOString() : null,
      bound: !!h,
      deviceName: h?.deviceName ?? "",
      registered: !!h?.user,
      userName: h?.user?.name ?? "",
      userPhone: formatPhone(h?.user?.phoneE164),
      attempts: attemptsBy.get(id) ?? 0,
      customerName: k.customerName ?? "",
      customerEmail: k.customerEmail ?? "",
      note: k.note ?? "",
      createdUtc: k.createdUtc.toISOString(),
      lastSeenUtc: k.lastSeenUtc ? k.lastSeenUtc.toISOString() : null,
    };
  });

  // Filters that depend on computed fields.
  const filtered = list.filter((k) => {
    if (status === "expired") return k.expired;
    if (status === "unused") return !k.bound && !k.expired && k.status === "active";
    if (status === "bound") return k.bound;
    if (status === "attempts") return k.attempts > 0;
    return true;
  });

  return ok({ keys: filtered });
}

// Keys are issued by POST /api/admin/orders, which records the purchase and its
// serial number first. Creating a key with no purchase behind it would leave a
// licence nobody can trace back to a sale, which is the whole point of the serial.
export async function POST() {
  return fail("use_orders_endpoint", 410, {
    hint: "POST /api/admin/orders — a purchase creates its serial and all its keys.",
  });
}

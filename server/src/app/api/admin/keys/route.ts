import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { ok, fail } from "@/lib/http";
import type { LicenseKeyDoc, ActivationDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";


// GET /api/admin/keys — list with optional status/search filter.
export async function GET(req: Request) {
  if (!(await getSession())) return fail("unauthorized", 401);

  const url = new URL(req.url);
  const status = url.searchParams.get("status"); // active|revoked|expired|unused
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
    ];
  }

  const rows = await keys.find(filter).sort({ createdUtc: -1 }).limit(500).toArray();
  const acts = db.collection<ActivationDoc>("activations");
  const now = new Date();

  const list = await Promise.all(
    rows.map(async (k) => {
      const used = await acts.countDocuments({ keyId: k._id!, status: "active" });
      const expired = !!k.expiryUtc && now > k.expiryUtc;
      return {
        id: k._id!.toHexString(),
        prefix: k.keyPrefix,
        serial: k.serial ?? "",
        seq: k.seq ?? 0,
        edition: k.edition,
        status: k.status,
        expired,
        expiryUtc: k.expiryUtc ? k.expiryUtc.toISOString() : null,
        deviceLimit: k.deviceLimit,
        devicesUsed: used,
        customerName: k.customerName ?? "",
        customerEmail: k.customerEmail ?? "",
        note: k.note ?? "",
        createdUtc: k.createdUtc.toISOString(),
        lastSeenUtc: k.lastSeenUtc ? k.lastSeenUtc.toISOString() : null,
      };
    }),
  );

  // Client-side "expired"/"unused" filters that depend on computed fields.
  const filtered = list.filter((k) => {
    if (status === "expired") return k.expired;
    if (status === "unused") return k.devicesUsed === 0 && !k.expired && k.status === "active";
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

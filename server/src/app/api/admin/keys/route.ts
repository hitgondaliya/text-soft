import { z } from "zod";
import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { generateKeyString, hashKey, keyPrefix } from "@/lib/licenseKey";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import type { LicenseKeyDoc, ActivationDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const CreateBody = z.object({
  edition: z.enum(["demo", "full"]),
  deviceLimit: z.number().int().min(1).max(100),
  // Exactly one of durationDays / expiryUtc / perpetual.
  durationDays: z.number().int().min(1).max(3650).optional(),
  expiryUtc: z.string().datetime().optional(),
  perpetual: z.boolean().optional(),
  customerName: z.string().max(128).optional(),
  customerEmail: z.string().email().optional().or(z.literal("")),
  note: z.string().max(500).optional(),
  count: z.number().int().min(1).max(100).default(1),
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

// POST /api/admin/keys — generate one or more keys. Returns the RAW strings ONCE.
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
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");

  const created: Array<{ id: string; key: string; prefix: string }> = [];
  for (let i = 0; i < b.count; i++) {
    // Retry on the astronomically-unlikely hash collision.
    let keyString = generateKeyString();
    let hash = hashKey(keyString);
    while (await keys.findOne({ keyHash: hash })) {
      keyString = generateKeyString();
      hash = hashKey(keyString);
    }

    const doc: LicenseKeyDoc = {
      keyHash: hash,
      keyPrefix: keyPrefix(keyString),
      edition: b.edition,
      expiryUtc,
      deviceLimit: b.deviceLimit,
      status: "active",
      customerName: b.customerName,
      customerEmail: b.customerEmail || undefined,
      note: b.note,
      createdBy: session.email,
      createdUtc: new Date(),
      lastSeenUtc: null,
    };
    const res = await keys.insertOne(doc);
    created.push({ id: res.insertedId.toHexString(), key: keyString, prefix: doc.keyPrefix });
  }

  await audit(session.email, "key.create", `${b.count} key(s)`, {
    edition: b.edition,
    deviceLimit: b.deviceLimit,
    expiryUtc: expiryUtc ? expiryUtc.toISOString() : "perpetual",
  });

  return ok({ created }, 201);
}

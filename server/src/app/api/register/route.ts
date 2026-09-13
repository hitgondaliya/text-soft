import { z } from "zod";
import { ObjectId } from "mongodb";
import { getDb } from "@/lib/mongo";
import { issueToken, readToken } from "@/lib/token";
import { ok, fail, clientIp } from "@/lib/http";
import { parsePhone, formatPhone } from "@/lib/phone";
import { rateLimited, LIMITS } from "@/lib/ratelimit";
import type { LicenseKeyDoc, ActivationDoc, EndUser } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const Body = z.object({
  token: z.string().min(16),
  name: z.string().trim().min(2).max(80),
  email: z.string().trim().email().max(120),
  phoneCountry: z.string().trim().length(2),
  phone: z.string().trim().min(4).max(20),
});

// POST /api/register — the person behind the device. Called once after a
// successful activation; the app is held on the registration screen until this
// returns a token with `registered: true`. Validation is the same libphonenumber
// rules the app applies, so a number that passes there passes here.
export async function POST(req: Request) {
  const ip = clientIp(req);
  if (rateLimited(`reg:ip:${ip}`, LIMITS.perIp.limit, LIMITS.perIp.windowMs)) return fail("rate_limited", 429);

  let b;
  try {
    b = Body.parse(await req.json());
  } catch (e) {
    const issue = e instanceof z.ZodError ? e.issues[0]?.path?.[0] : undefined;
    return fail("bad_request", 400, { field: issue });
  }

  const payload = readToken(b.token);
  if (!payload) return fail("invalid", 400);

  const phone = parsePhone(b.phoneCountry, b.phone);
  if (!phone) return fail("bad_request", 400, { field: "phone" });

  let keyOid: ObjectId;
  try {
    keyOid = new ObjectId(payload.keyId);
  } catch {
    return fail("invalid", 400);
  }

  const db = await getDb();
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");
  const acts = db.collection<ActivationDoc>("activations");

  const keyDoc = await keys.findOne({ _id: keyOid });
  if (!keyDoc) return fail("invalid", 404);
  if (keyDoc.status === "revoked") return fail("revoked", 403);
  const now = new Date();
  if (keyDoc.expiryUtc && now > keyDoc.expiryUtc) return fail("expired", 403);

  const act = await acts.findOne({ keyId: keyOid, deviceFp: payload.deviceFp });
  if (!act || act.status !== "active") return fail("released", 403);

  const user: EndUser = {
    name: b.name,
    email: b.email.toLowerCase(),
    phoneCountry: phone.country,
    phone: phone.national,
    phoneE164: phone.e164,
    registeredUtc: act.user?.registeredUtc ?? now,
  };

  await acts.updateOne({ _id: act._id }, { $set: { user, lastSeenUtc: now, ip } });
  await keys.updateOne(
    { _id: keyOid },
    { $set: { userName: user.name, userEmail: user.email, userPhoneE164: user.phoneE164, lastSeenUtc: now } },
  );

  const updated: ActivationDoc = { ...act, user };
  return ok({
    token: issueToken(keyDoc, updated, now),
    serial: keyDoc.serial,
    edition: keyDoc.edition,
    expiryUtc: keyDoc.expiryUtc ? keyDoc.expiryUtc.toISOString() : null,
    registered: true,
    user: { name: user.name, email: user.email, phone: formatPhone(user.phoneE164) },
    serverTimeUtc: now.toISOString(),
  });
}

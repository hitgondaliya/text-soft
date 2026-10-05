import { getDb } from "@/lib/mongo";
import { getSession } from "@/lib/auth";
import { audit } from "@/lib/audit";
import { ok, fail } from "@/lib/http";
import { generateKeyString, hashKey, keyPrefix } from "@/lib/licenseKey";
import { sealKey } from "@/lib/vault";
import { oid } from "../../../_lib/keys";
import type { ActivationDoc, LicenseKeyDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// POST /api/admin/keys/:id/reissue — give this key a NEW key string, keeping its
// place in the purchase (same serial, same seq, same terms). Meant for keys issued
// before keys were sealed: they cannot be shown again, so when a customer lost the
// email and never used the key, this hands out a replacement. The old string stops
// working immediately.
//   200 { key, prefix } · 404 not_found · 409 revoked · 409 in_use { deviceName }
export async function POST(_req: Request, { params }: { params: { id: string } }) {
  const session = await getSession();
  if (!session) return fail("unauthorized", 401);
  const _id = oid(params.id);
  if (!_id) return fail("not_found", 404);

  const db = await getDb();
  const keys = db.collection<LicenseKeyDoc>("licenseKeys");
  const acts = db.collection<ActivationDoc>("activations");

  const key = await keys.findOne({ _id });
  if (!key) return fail("not_found", 404);
  if (key.status === "revoked") return fail("revoked", 409);

  // A PC holds this key: changing the string under it would strand the customer.
  // Move it off the device first (key page → Move to another device).
  const holder = await acts.findOne({ keyId: _id, status: "active" });
  if (holder) return fail("in_use", 409, { deviceName: holder.deviceName ?? "" });

  let keyString = generateKeyString();
  let hash = hashKey(keyString);
  while (await keys.findOne({ keyHash: hash })) {
    keyString = generateKeyString();
    hash = hashKey(keyString);
  }
  const prefix = keyPrefix(keyString);
  const now = new Date();

  // Only replace the string we checked: a second admin reissuing at the same moment,
  // or a revoke in between, makes this match nothing instead of overwriting it.
  const res = await keys.updateOne(
    { _id, keyHash: key.keyHash, status: "active" },
    { $set: { keyHash: hash, keyPrefix: prefix, keyEnc: sealKey(keyString), reissuedUtc: now, reissuedBy: session.email } },
  );
  if (res.matchedCount === 0) {
    const latest = await keys.findOne({ _id });
    if (!latest) return fail("not_found", 404);
    if (latest.status === "revoked") return fail("revoked", 409);
    return fail("conflict", 409);
  }

  // A device may have activated with the old string between the check and the update.
  // Then it holds the key; put the old string back rather than strand it.
  const late = await acts.findOne({ keyId: _id, status: "active" });
  if (late) {
    const restore: Partial<LicenseKeyDoc> = { keyHash: key.keyHash, keyPrefix: key.keyPrefix };
    const unset: Record<string, ""> = {};
    if (key.keyEnc) restore.keyEnc = key.keyEnc;
    else unset.keyEnc = "";
    if (key.reissuedUtc) {
      restore.reissuedUtc = key.reissuedUtc;
      restore.reissuedBy = key.reissuedBy;
    } else {
      unset.reissuedUtc = "";
      unset.reissuedBy = "";
    }
    await keys.updateOne({ _id }, { $set: restore, ...(Object.keys(unset).length ? { $unset: unset } : {}) });
    return fail("in_use", 409, { deviceName: late.deviceName ?? "" });
  }

  await audit(session.email, "key.reissue", `${key.serial} #${key.seq}`, {
    keyId: params.id,
    oldPrefix: key.keyPrefix,
    newPrefix: prefix,
    wasRecoverable: !!key.keyEnc,
  });
  return ok({ key: keyString, prefix, serial: key.serial, seq: key.seq });
}

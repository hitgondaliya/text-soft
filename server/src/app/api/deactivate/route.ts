import { z } from "zod";
import { ObjectId } from "mongodb";
import { getDb } from "@/lib/mongo";
import { readToken } from "@/lib/token";
import { ok, fail } from "@/lib/http";
import type { ActivationDoc } from "@/lib/models";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const Body = z.object({ token: z.string().min(16) });

// POST /api/deactivate — free this device's slot so the user can move machines.
export async function POST(req: Request) {
  let parsed;
  try {
    parsed = Body.parse(await req.json());
  } catch {
    return fail("bad_request", 400);
  }

  const payload = readToken(parsed.token);
  if (!payload) return fail("invalid", 400);

  let keyOid: ObjectId;
  try {
    keyOid = new ObjectId(payload.keyId);
  } catch {
    return fail("invalid", 400);
  }

  const db = await getDb();
  const acts = db.collection<ActivationDoc>("activations");
  await acts.updateOne(
    { keyId: keyOid, deviceFp: payload.deviceFp },
    { $set: { status: "deactivated", lastSeenUtc: new Date() } },
  );

  return ok({ status: "deactivated" });
}

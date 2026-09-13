import { fail } from "@/lib/http";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// A key is moved between devices ONLY from the admin panel ("Move to another
// device" on the key page). Letting the app release its own binding would let a
// customer hop a single key across any number of PCs, which is the thing the
// one-key-one-device rule exists to stop. The endpoint stays so old builds get
// a clear answer instead of a 404.
export async function POST() {
  return fail("admin_only", 410, {
    hint: "Ask support to move the licence to another device.",
  });
}

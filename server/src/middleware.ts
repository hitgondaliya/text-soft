import { NextRequest, NextResponse } from "next/server";
import { jwtVerify } from "jose";

// Gate the admin panel and admin API. Public activation endpoints are untouched.
// Runs on the Edge runtime, so it re-implements the cookie check with jose and
// does NOT import lib/auth (which pulls mongodb/bcrypt — not Edge-compatible).
// Keep this cookie name in sync with lib/auth.ts.
const COOKIE = "txsf_admin";

async function isAuthed(req: NextRequest): Promise<boolean> {
  const token = req.cookies.get(COOKIE)?.value;
  if (!token) return false;
  const secret = process.env.COOKIE_SECRET;
  if (!secret) return false;
  try {
    await jwtVerify(token, new TextEncoder().encode(secret));
    return true;
  } catch {
    return false;
  }
}

export async function middleware(req: NextRequest) {
  const { pathname } = req.nextUrl;
  const authed = await isAuthed(req);

  if (pathname.startsWith("/api/admin")) {
    if (!authed) return NextResponse.json({ error: "unauthorized" }, { status: 401 });
    return NextResponse.next();
  }

  if (pathname.startsWith("/admin")) {
    if (!authed) {
      const url = req.nextUrl.clone();
      url.pathname = "/login";
      url.searchParams.set("next", pathname);
      return NextResponse.redirect(url);
    }
  }

  // Already logged in? Skip the login page.
  if (pathname === "/login" && authed) {
    const url = req.nextUrl.clone();
    url.pathname = "/admin";
    url.search = "";
    return NextResponse.redirect(url);
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/admin/:path*", "/api/admin/:path*", "/login"],
};

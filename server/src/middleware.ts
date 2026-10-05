import { NextRequest, NextResponse } from "next/server";
import { jwtVerify } from "jose";

// Gate the admin panel and admin API. Public activation endpoints are untouched.
// Runs on the Edge runtime, so it re-implements the cookie check with jose and
// does NOT import lib/auth (which pulls mongodb/bcrypt — not Edge-compatible).
// Keep this cookie name in sync with lib/auth.ts.
//
// This is only the coarse first check (is the cookie signed and unexpired?). Every
// admin API route and the admin layout also call getSession(), which re-reads the
// account, so a disabled, removed or demoted admin is refused there.
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

const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);

/**
 * A state-changing request must come from the panel's own pages. SameSite=Lax keeps
 * other sites out, but the marketing site and every other *.text-soft.com subdomain
 * count as the same site, so their pages could still POST with the admin's cookie
 * (resend all keys to any address, reissue keys). Browsers send Sec-Fetch-Site and
 * Origin on such requests; scripts without them (node e2e, curl) are not browsers
 * carrying someone's cookie and pass.
 */
function crossOrigin(req: NextRequest): boolean {
  if (SAFE_METHODS.has(req.method)) return false;
  const site = req.headers.get("sec-fetch-site");
  if (site && site !== "same-origin" && site !== "none") return true;
  const origin = req.headers.get("origin");
  if (!origin) return false;
  if (origin === "null") return true;
  const host = req.headers.get("x-forwarded-host") ?? req.headers.get("host");
  if (!host) return true;
  try {
    // Hostnames only: nginx's `Host $host` drops the port, the browser's Origin keeps it.
    // Cookies are not separated by port either, so this loses nothing.
    return new URL(origin).hostname.toLowerCase() !== new URL(`http://${host}`).hostname.toLowerCase();
  } catch {
    return true;
  }
}

export async function middleware(req: NextRequest) {
  const { pathname } = req.nextUrl;

  if (pathname.startsWith("/api/")) {
    if (crossOrigin(req)) return NextResponse.json({ error: "forbidden" }, { status: 403 });
    if (pathname.startsWith("/api/admin") && !(await isAuthed(req)))
      return NextResponse.json({ error: "unauthorized" }, { status: 401 });
    return NextResponse.next();
  }

  const authed = await isAuthed(req);

  if (pathname.startsWith("/admin")) {
    if (!authed) {
      const url = req.nextUrl.clone();
      url.pathname = "/login";
      url.searchParams.set("next", pathname);
      return NextResponse.redirect(url);
    }
  }

  if (pathname === "/login") {
    // The admin layout sends a signed cookie whose account was disabled, removed or
    // had its password reset here. Drop that cookie and show the form, instead of
    // bouncing it back to /admin forever.
    if (req.nextUrl.searchParams.get("reason") === "signed_out") {
      const res = NextResponse.next();
      if (req.cookies.get(COOKIE)) res.cookies.set(COOKIE, "", { httpOnly: true, path: "/", maxAge: 0 });
      return res;
    }
    // Already logged in? Skip the login page.
    if (authed) {
      const url = req.nextUrl.clone();
      url.pathname = "/admin";
      url.search = "";
      return NextResponse.redirect(url);
    }
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/admin/:path*", "/api/admin/:path*", "/api/login", "/api/logout", "/login"],
};

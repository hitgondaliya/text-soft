import { NextResponse } from "next/server";

export function ok(data: unknown, status = 200): NextResponse {
  return NextResponse.json(data, { status });
}

export function fail(error: string, status = 400, extra?: Record<string, unknown>): NextResponse {
  return NextResponse.json({ error, ...extra }, { status });
}

/**
 * Client IP behind nginx, for rate limits and the attempt log. nginx sets X-Real-IP to
 * the address that actually connected, replacing whatever the client sent, so it is
 * read first. The first X-Forwarded-For entry is whatever the client claims and is
 * never trusted; without X-Real-IP the LAST entry (added by our own proxy) is used.
 * The app listens on 127.0.0.1 only, so nothing reaches it except through nginx.
 */
export function clientIp(req: Request): string {
  const real = req.headers.get("x-real-ip")?.trim();
  if (real) return real;
  const xff = req.headers.get("x-forwarded-for");
  if (xff) {
    const parts = xff.split(",").map((s) => s.trim()).filter(Boolean);
    if (parts.length) return parts[parts.length - 1];
  }
  return "unknown";
}

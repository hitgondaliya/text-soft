import { lookup } from "node:dns/promises";
import net from "node:net";
import { z } from "zod";
import { getSession, type Session } from "@/lib/auth";
import { fail } from "@/lib/http";
import { hasUsablePassword } from "@/lib/settings";
import type { NextResponse } from "next/server";

// Shared by /api/admin/settings/smtp and /api/admin/settings/smtp/test.

/** The settings form (PUT body, and `config` of the test call). Blank password = keep the current one. */
export const SmtpBody = z.object({
  host: z
    .string()
    .trim()
    .min(1)
    .max(255)
    .regex(/^[A-Za-z0-9.:\-]+$/, "host"),
  port: z.coerce.number().int().min(1).max(65535),
  security: z.enum(["ssl", "starttls", "none"]),
  user: z.string().trim().max(255).optional().default(""),
  password: z.string().max(500).optional(),
  fromName: z.string().trim().max(100).optional().default(""),
  fromEmail: z.string().trim().email().max(254),
  replyTo: z.string().trim().email().max(254).optional().or(z.literal("")),
});
export type SmtpBodyInput = z.infer<typeof SmtpBody>;

/**
 * Credentials a server with TLS needs: a username, and a password — typed, or the
 * stored one when host and username are unchanged. Returns the missing field, if any.
 */
export async function missingCredential(b: SmtpBodyInput): Promise<"user" | "password" | undefined> {
  if (b.security === "none") return undefined;
  if (!b.user) return "user";
  if (b.password && b.password.trim()) return undefined;
  return (await hasUsablePassword(b.host, b.user)) ? undefined : "password";
}

/** Ports a mail server answers on. Anything else on this server's own network is refused. */
const MAIL_PORTS = new Set([25, 465, 587, 2525]);

function v4Parts(ip: string): number[] | null {
  const m = /^(?:::ffff:)?(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/i.exec(ip);
  return m ? m.slice(1).map(Number) : null;
}

/** Link-local (169.254/16, fe80::/10): cloud metadata lives here, never a mail server. */
function isLinkLocal(ip: string): boolean {
  const v4 = v4Parts(ip);
  if (v4) return v4[0] === 169 && v4[1] === 254;
  return /^fe[89ab][0-9a-f]:/i.test(ip);
}

/** Loopback, private, CGNAT, unspecified or unique-local: this machine or its own network. */
function isInternal(ip: string): boolean {
  const v4 = v4Parts(ip);
  if (v4) {
    const [a, b] = v4;
    return a === 0 || a === 10 || a === 127 || (a === 172 && b >= 16 && b <= 31) || (a === 192 && b === 168) || (a === 100 && b >= 64 && b <= 127) || isLinkLocal(ip);
  }
  const x = ip.toLowerCase();
  return x === "::" || x === "::1" || /^f[cd][0-9a-f]{2}:/.test(x) || isLinkLocal(x);
}

/**
 * Where the panel may connect when it sends (or test-sends) mail. A local relay is
 * fine (127.0.0.1:25, the dev sink on 2525), but an SMTP "test" must not become a
 * port scanner for the VPS's own services (MongoDB on 27017 and the like) or the
 * cloud metadata address. SMTP_ALLOW_PRIVATE_HOSTS=1 lifts this for unusual setups.
 * Returns { field, detail } for a 400, or undefined when the host is acceptable.
 */
export async function smtpTargetProblem(host: string, port: number): Promise<{ field: "host" | "port"; detail: string } | undefined> {
  if (process.env.SMTP_ALLOW_PRIVATE_HOSTS === "1") return undefined;
  const bare = host.replace(/^\[|\]$/g, "");
  let addrs: string[];
  if (net.isIP(bare)) addrs = [bare];
  else {
    try {
      addrs = (await lookup(bare, { all: true })).map((a) => a.address);
    } catch {
      return undefined; // unknown name: the send itself reports ENOTFOUND
    }
  }
  if (addrs.some(isLinkLocal))
    return { field: "host", detail: "That address is link-local (cloud metadata), not a mail server." };
  if (addrs.some(isInternal) && !MAIL_PORTS.has(port))
    return {
      field: "port",
      detail: `On this server's own network only mail ports are allowed: ${Array.from(MAIL_PORTS).join(", ")}.`,
    };
  return undefined;
}

/** First failing field of a zod error, for { error: "bad_request", field }. */
export function zodField(e: unknown): string | undefined {
  if (e instanceof z.ZodError) {
    const p = e.issues[0]?.path;
    return p && p.length ? p.map(String).join(".") : undefined;
  }
  return undefined;
}

/** Settings are for owners. Returns the session, or the response to send instead. */
export async function requireOwner(): Promise<{ session: Session; res?: undefined } | { session?: undefined; res: NextResponse }> {
  const session = await getSession();
  if (!session) return { res: fail("unauthorized", 401) };
  if (session.role !== "owner") return { res: fail("forbidden", 403) };
  return { session };
}

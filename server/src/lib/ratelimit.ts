// Small in-process rate limiter for the public licence endpoints, the admin login and
// key reveals. The server runs as one PM2 process, so a Map is enough; if it is ever
// scaled out, swap the store for Redis behind the same functions. The Map lives on the
// global object so every route bundle (and a dev hot-reload) shares the same counts.
//
// Two buckets per endpoint: per client IP (stops one machine hammering us) and
// per key hash (stops a leaked key being tried from many machines).

interface Bucket {
  hits: number[];
}

declare global {
  // eslint-disable-next-line no-var
  var __textsoftRate: { buckets: Map<string, Bucket>; lastSweep: number } | undefined;
}

function store() {
  return (global.__textsoftRate ??= { buckets: new Map<string, Bucket>(), lastSweep: Date.now() });
}

function sweep(now: number) {
  const s = store();
  if (now - s.lastSweep < 60_000) return;
  s.lastSweep = now;
  for (const [k, b] of s.buckets) {
    if (b.hits.length === 0 || now - b.hits[b.hits.length - 1] > 3_600_000) s.buckets.delete(k);
  }
}

function bucket(id: string, windowMs: number, now: number): Bucket {
  const s = store();
  let b = s.buckets.get(id);
  if (!b) {
    b = { hits: [] };
    s.buckets.set(id, b);
  }
  b.hits = b.hits.filter((t) => now - t < windowMs);
  return b;
}

/** Record `count` hits and say whether `id` is over `limit` hits per `windowMs`. */
export function rateLimited(id: string, limit: number, windowMs: number, count = 1): boolean {
  const now = Date.now();
  sweep(now);
  const b = bucket(id, windowMs, now);
  for (let i = 0; i < count; i++) b.hits.push(now);
  return b.hits.length > limit;
}

/** Has `id` already used up `limit` hits in `windowMs`? Records nothing. */
export function limitReached(id: string, limit: number, windowMs: number): boolean {
  const now = Date.now();
  sweep(now);
  return bucket(id, windowMs, now).hits.length >= limit;
}

/** Count one hit against `id` without checking it (pair with limitReached). */
export function recordHit(id: string): void {
  const now = Date.now();
  sweep(now);
  const s = store();
  const b = s.buckets.get(id) ?? { hits: [] };
  b.hits.push(now);
  s.buckets.set(id, b);
}

export const LIMITS = {
  /** Activation-type calls per IP per 10 minutes. */
  perIp: { limit: 30, windowMs: 10 * 60_000 },
  /** Calls per key per hour, from anywhere. */
  perKey: { limit: 20, windowMs: 60 * 60_000 },
  /** Heartbeats per IP per hour — generous, an office may share one IP. */
  heartbeatPerIp: { limit: 600, windowMs: 60 * 60_000 },
  /** Failed admin sign-ins per IP per 10 minutes. */
  loginPerIp: { limit: 10, windowMs: 10 * 60_000 },
  /** Failed sign-ins per account per 10 minutes, from anywhere (higher, so one attacker cannot lock the owner out alone). */
  loginPerEmail: { limit: 30, windowMs: 10 * 60_000 },
  /** Keys one admin may reveal per hour (an order reveal counts each key; one 500-key purchase fits twice). */
  revealPerAdmin: { limit: 1000, windowMs: 60 * 60_000 },
};

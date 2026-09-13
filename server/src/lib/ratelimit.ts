// Small in-process rate limiter for the public licence endpoints. The server
// runs as one PM2 process, so a Map is enough; if it is ever scaled out, swap the
// store for Redis behind the same two functions.
//
// Two buckets per endpoint: per client IP (stops one machine hammering us) and
// per key hash (stops a leaked key being tried from many machines).

interface Bucket {
  hits: number[];
}

const buckets = new Map<string, Bucket>();
let lastSweep = Date.now();

function sweep(now: number) {
  if (now - lastSweep < 60_000) return;
  lastSweep = now;
  for (const [k, b] of buckets) {
    if (b.hits.length === 0 || now - b.hits[b.hits.length - 1] > 3_600_000) buckets.delete(k);
  }
}

/** Record a hit and say whether `id` is over `limit` hits per `windowMs`. */
export function rateLimited(id: string, limit: number, windowMs: number): boolean {
  const now = Date.now();
  sweep(now);
  let b = buckets.get(id);
  if (!b) {
    b = { hits: [] };
    buckets.set(id, b);
  }
  b.hits = b.hits.filter((t) => now - t < windowMs);
  b.hits.push(now);
  return b.hits.length > limit;
}

export const LIMITS = {
  /** Activation-type calls per IP per 10 minutes. */
  perIp: { limit: 30, windowMs: 10 * 60_000 },
  /** Calls per key per hour, from anywhere. */
  perKey: { limit: 20, windowMs: 60 * 60_000 },
  /** Heartbeats per IP per hour — generous, an office may share one IP. */
  heartbeatPerIp: { limit: 600, windowMs: 60 * 60_000 },
};

"use client";

import * as React from "react";
import { Eye, EyeOff, Loader2, RefreshCw } from "lucide-react";
import { toast } from "sonner";
import { badgeVariants } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Hint } from "@/components/ui/tooltip";
import { CopyButton } from "@/components/copy-button";
import { cn } from "@/lib/utils";

/**
 * SecretKey — a licence key shown masked, with reveal and copy.
 *
 *   <SecretKey prefix={k.prefix} recoverable={k.recoverable} reveal={() => revealKeyById(k.id)}
 *              onReissue={() => setReissueTarget(k)} />
 *
 * Props:
 *   prefix        string                 "TXSF-9F2K" — shown as "TXSF-9F2K-••••-••••"
 *   recoverable   boolean                false → prefix + muted "not stored" badge (+ Reissue when onReissue is set)
 *   reveal        () => Promise<string>  fetches the full key; called at most once (cached); throw an Error to toast its message
 *   onReissue?    () => void             shows a small "Reissue" button on unrecoverable keys (caller confirms + calls the API)
 *   reissueBusy?  boolean                spinner/disabled state for that button
 *   copyable?     boolean                show the copy button (default true)
 *   className?    string
 *
 * Also exported: revealKeyById(id) → POST /api/admin/keys/:id/reveal, resolves to the key or
 * throws an Error with a readable message ("not_recoverable" → explains it was issued before keys were stored).
 */

export const NOT_STORED_HINT = "Issued before keys were stored. Reissue it if it was never used.";

export async function revealKeyById(id: string): Promise<string> {
  const res = await fetch(`/api/admin/keys/${encodeURIComponent(id)}/reveal`, { method: "POST" });
  const body = (await res.json().catch(() => ({}))) as { key?: string; error?: string };
  if (res.ok && typeof body.key === "string") return body.key;
  if (body.error === "not_recoverable") throw new Error(NOT_STORED_HINT);
  if (body.error === "not_found") throw new Error("That key no longer exists.");
  if (res.status === 401) throw new Error("Your session has expired. Sign in again.");
  if (res.status === 429) throw new Error("Too many keys shown this hour. Try again later.");
  throw new Error("Could not fetch the key.");
}

export function maskKey(prefix: string): string {
  const groups = prefix.split("-").filter(Boolean);
  const hidden = Math.max(1, 4 - groups.length);
  return [...groups, ...Array.from({ length: hidden }, () => "••••")].join("-");
}

export function SecretKey({
  prefix,
  recoverable,
  reveal,
  onReissue,
  reissueBusy = false,
  copyable = true,
  className,
}: {
  prefix: string;
  recoverable: boolean;
  reveal: () => Promise<string>;
  onReissue?: () => void;
  reissueBusy?: boolean;
  copyable?: boolean;
  className?: string;
}) {
  const [full, setFull] = React.useState<string | null>(null);
  const [shown, setShown] = React.useState(false);
  const [loading, setLoading] = React.useState(false);
  const inflight = React.useRef<Promise<string> | null>(null);

  // A new key (e.g. after a reissue) must not show the old cached value.
  React.useEffect(() => {
    setFull(null);
    setShown(false);
    inflight.current = null;
  }, [prefix]);

  const getKey = React.useCallback(async (): Promise<string> => {
    if (full) return full;
    if (!inflight.current) {
      inflight.current = reveal().then(
        (k) => {
          setFull(k);
          return k;
        },
        (err) => {
          inflight.current = null;
          throw err;
        },
      );
    }
    return inflight.current;
  }, [full, reveal]);

  async function toggle() {
    if (shown) {
      setShown(false);
      return;
    }
    if (full) {
      setShown(true);
      return;
    }
    setLoading(true);
    try {
      await getKey();
      setShown(true);
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : "Could not fetch the key.");
    } finally {
      setLoading(false);
    }
  }

  if (!recoverable) {
    return (
      <span className={cn("inline-flex items-center gap-1.5 whitespace-nowrap", className)}>
        <span className="font-mono text-[13px] text-muted-foreground">{prefix}…</span>
        <Hint content={NOT_STORED_HINT}>
          <span tabIndex={0} className={cn(badgeVariants({ variant: "secondary" }), "cursor-help font-normal")}>
            not stored
          </span>
        </Hint>
        {onReissue && (
          <Button
            type="button"
            variant="outline"
            size="xs"
            disabled={reissueBusy}
            onClick={(e) => {
              e.preventDefault();
              e.stopPropagation();
              onReissue();
            }}
            title="Issue a new key string for this slot (old key stops working)"
          >
            {reissueBusy ? <Loader2 className="animate-spin" /> : <RefreshCw />}
            Reissue
          </Button>
        )}
      </span>
    );
  }

  const visible = shown && full;
  return (
    <span className={cn("inline-flex items-center gap-0.5 whitespace-nowrap", className)}>
      <span
        className={cn("mr-1 font-mono text-[13px] tracking-wide", visible ? "select-all text-foreground" : "text-muted-foreground")}
        aria-live="polite"
      >
        {visible ? full : maskKey(prefix)}
      </span>
      <Button
        type="button"
        variant="ghost"
        size="icon-xs"
        onClick={(e) => {
          e.preventDefault();
          e.stopPropagation();
          void toggle();
        }}
        disabled={loading}
        aria-label={visible ? "Hide key" : "Show key"}
        title={visible ? "Hide key" : "Show key"}
        className="text-muted-foreground hover:text-foreground"
      >
        {loading ? <Loader2 className="animate-spin" /> : visible ? <EyeOff /> : <Eye />}
      </Button>
      {copyable && (
        <CopyButton
          value={getKey}
          size="icon-xs"
          ariaLabel="Copy key"
          toastText="Key copied"
          className="text-muted-foreground hover:text-foreground"
        />
      )}
    </span>
  );
}

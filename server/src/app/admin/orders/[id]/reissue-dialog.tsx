"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { CheckCircle2, Loader2, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Alert } from "@/components/ui/alert";
import { CopyButton } from "@/components/copy-button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import type { KeyRow } from "./order-types";

/**
 * Confirm → POST /api/admin/keys/:id/reissue → show the new key with copy buttons.
 * For keys issued before keys were stored: the slot (serial + #seq) gets a new key
 * string and the old one stops working.
 */
export function ReissueDialog({
  serial,
  target,
  onClose,
  onReissued,
}: {
  serial: string;
  target: KeyRow | null;
  onClose: () => void;
  /** Called after a successful reissue (reload the purchase). */
  onReissued: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ title: string; body: string; openKey?: boolean } | null>(null);
  const [newKey, setNewKey] = useState<string | null>(null);
  // Keep the last key on screen while the dialog fades out.
  const [last, setLast] = useState<KeyRow | null>(target);

  // A different key opened: start from the confirm step again.
  useEffect(() => {
    if (!target) return;
    setLast(target);
    setBusy(false);
    setError(null);
    setNewKey(null);
  }, [target]);

  async function reissue() {
    if (!target) return;
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/api/admin/keys/${encodeURIComponent(target.id)}/reissue`, { method: "POST" });
      const body = (await res.json().catch(() => ({}))) as { key?: string; error?: string; deviceName?: string };
      if (res.ok && body.key) {
        setNewKey(body.key);
        onReissued();
        return;
      }
      if (body.error === "in_use") {
        setError({
          title: `Key #${target.seq} is in use on ${body.deviceName ? `“${body.deviceName}”` : "a PC"}`,
          body: "Reissuing it would lock that PC out. Open the key and use “Move to another device” first, then reissue.",
          openKey: true,
        });
      } else if (body.error === "revoked") {
        setError({ title: "This key is revoked", body: "Un-revoke it first if it should work again, then reissue." });
      } else if (body.error === "conflict") {
        setError({ title: "The key changed just now", body: "Someone else changed it at the same moment. Close this and try again." });
      } else if (body.error === "not_found") {
        setError({ title: "This key no longer exists", body: "Reload the purchase." });
      } else if (res.status === 401) {
        setError({ title: "Your session has expired", body: "Sign in again, then retry." });
      } else {
        setError({ title: "Couldn't reissue the key", body: "Try again in a moment." });
      }
    } catch {
      setError({ title: "Couldn't reach the server", body: "Check your connection and try again." });
    } finally {
      setBusy(false);
    }
  }

  const k = target ?? last;
  return (
    <Dialog open={!!target} onOpenChange={(v) => !v && !busy && onClose()}>
      <DialogContent className="sm:max-w-md">
        {k && newKey ? (
          <>
            <DialogHeader>
              <div className="mb-1 flex h-8 w-8 items-center justify-center rounded-full bg-success/10 text-success">
                <CheckCircle2 className="h-4 w-4" />
              </div>
              <DialogTitle>New key for #{k.seq}</DialogTitle>
              <DialogDescription>
                Give the customer the serial <span className="font-mono text-foreground">{serial}</span> and this key.
              </DialogDescription>
            </DialogHeader>
            <div className="flex items-center gap-2 rounded-md border bg-muted/40 py-1.5 pl-3 pr-1.5">
              <code className="min-w-0 flex-1 select-all break-all font-mono text-sm font-medium tracking-wide">{newKey}</code>
              <CopyButton value={newKey} ariaLabel="Copy key" toastText="Key copied" />
            </div>
            <p className="text-xs text-muted-foreground">
              The old key <span className="font-mono">{k.prefix}…</span> no longer works. This one is stored, so you can copy it
              again from the purchase page at any time.
            </p>
            <DialogFooter>
              <CopyButton
                value={`Serial: ${serial}\nKey ${k.seq}: ${newKey}`}
                label="Copy serial + key"
                variant="outline"
                size="default"
                toastText="Serial and key copied"
              />
              <Button onClick={onClose}>Done</Button>
            </DialogFooter>
          </>
        ) : k ? (
          <>
            <DialogHeader>
              <DialogTitle>
                Reissue key #{k.seq} of <span className="font-mono">{serial}</span>?
              </DialogTitle>
              <DialogDescription>
                This key was issued before keys were stored, so it can&apos;t be shown again. Reissuing gives this slot a new key
                string — same serial, same number, same validity.
              </DialogDescription>
            </DialogHeader>
            <Alert tone="warning" title={`The old key ${k.prefix}… stops working at once`}>
              Only do this if the customer lost the key and never activated it.
            </Alert>
            {error && (
              <Alert
                tone="danger"
                title={error.title}
                action={
                  error.openKey ? (
                    <Button variant="outline" size="xs" asChild>
                      <Link href={`/admin/keys/${k.id}`}>Open key</Link>
                    </Button>
                  ) : undefined
                }
              >
                {error.body}
              </Alert>
            )}
            <DialogFooter>
              <Button variant="ghost" onClick={onClose} disabled={busy}>
                Cancel
              </Button>
              <Button onClick={() => void reissue()} disabled={busy || error?.openKey}>
                {busy ? <Loader2 className="animate-spin" /> : <RefreshCw />}
                {busy ? "Reissuing…" : "Reissue key"}
              </Button>
            </DialogFooter>
          </>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

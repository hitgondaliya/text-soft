"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import { Ban, CircleCheck, CircleSlash, Loader2, Send } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert } from "@/components/ui/alert";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { keyCounts, type Order } from "./order-types";

type Failure = { title: string; body?: React.ReactNode };

/**
 * Send the purchase email again: POST /api/admin/orders/:idOrSerial/resend { to }.
 * The recipient starts as the customer's email and can be changed. Revoked keys and
 * keys that were never stored are left out; the dialog says so before sending.
 */
export function ResendDialog({
  order,
  open,
  onOpenChange,
  onSent,
}: {
  order: Order;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  onSent: () => void;
}) {
  const [to, setTo] = useState(order.customerEmail);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<Failure | null>(null);

  useEffect(() => {
    if (open) {
      setTo(order.customerEmail);
      setFailure(null);
    }
  }, [open, order.customerEmail]);

  const c = keyCounts(order.keys);
  const nothing = c.usable === 0;

  async function send(e: React.FormEvent) {
    e.preventDefault();
    if (busy || nothing) return;
    const recipient = to.trim();
    if (!recipient) {
      setFailure({ title: "Enter an email address to send to." });
      return;
    }
    setBusy(true);
    setFailure(null);
    try {
      const res = await fetch(`/api/admin/orders/${encodeURIComponent(order.serial)}/resend`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ to: recipient }),
      });
      const b = (await res.json().catch(() => ({}))) as {
        to?: string;
        toCustomer?: boolean;
        included?: number;
        missing?: number;
        revoked?: number;
        error?: string;
        detail?: string;
        field?: string;
      };
      if (res.ok) {
        const left: string[] = [];
        if (b.missing) left.push(`${b.missing} not stored`);
        if (b.revoked) left.push(`${b.revoked} revoked`);
        const notes = [
          left.length ? `Left out: ${left.join(", ")}.` : "",
          b.toCustomer === false ? "Not the customer's address, so the purchase still shows its own delivery status." : "",
        ].filter(Boolean);
        toast.success(`Sent ${b.included} key${b.included === 1 ? "" : "s"} to ${b.to ?? recipient}`, {
          description: notes.length ? notes.join(" ") : undefined,
        });
        onSent();
        onOpenChange(false);
        return;
      }
      switch (b.error) {
        case "no_recipient":
          setFailure({ title: "Enter an email address to send to." });
          break;
        case "bad_request":
          setFailure({ title: "That email address doesn't look right." });
          break;
        case "no_recoverable_keys":
          setFailure({
            title: "No key in this purchase can be sent",
            body: `${b.missing ?? 0} not stored, ${b.revoked ?? 0} revoked.`,
          });
          break;
        case "mail_not_configured":
          setFailure({
            title: "Email sending isn't set up yet",
            body: (
              <>
                An owner can set it in{" "}
                <Link href="/admin/settings" className="font-medium text-foreground underline underline-offset-2">
                  Settings
                </Link>
                .
              </>
            ),
          });
          break;
        case "mail_failed":
          setFailure({ title: "The mail server refused it", body: b.detail || "No reason given." });
          onSent(); // the purchase now records the error
          break;
        case "not_found":
          setFailure({ title: "This purchase no longer exists." });
          break;
        default:
          setFailure({
            title: res.status === 401 ? "Your session has expired" : "Couldn't send the email",
            body: res.status === 401 ? "Sign in again, then retry." : "Try again in a moment.",
          });
      }
    } catch {
      setFailure({ title: "Couldn't reach the server", body: "Check your connection and try again." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !busy && onOpenChange(v)}>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={send} className="grid gap-4">
          <DialogHeader>
            <DialogTitle>{order.emailedUtc ? "Resend the keys email" : "Email the keys"}</DialogTitle>
            <DialogDescription>
              The purchase email for <span className="font-mono text-foreground">{order.serial}</span> with the serial and
              every key that can be sent.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-1.5">
            <Label htmlFor="resend-to" className="text-xs">
              Send to
            </Label>
            <Input
              id="resend-to"
              type="email"
              value={to}
              onChange={(e) => {
                setTo(e.target.value);
                setFailure(null);
              }}
              placeholder="name@company.com"
              autoFocus
              autoComplete="off"
            />
            <p className="text-xs text-muted-foreground">
              {order.customerEmail ? "The customer's email. Change it to send somewhere else." : "This purchase has no customer email."}
            </p>
          </div>

          <ul className="divide-y rounded-md border text-[13px]">
            <SummaryRow icon={CircleCheck} tone="text-success" label="Included" value={c.usable} />
            {c.missing > 0 && (
              <SummaryRow
                icon={CircleSlash}
                tone="text-muted-foreground"
                label="Not stored — can't be included"
                value={c.missing}
              />
            )}
            {c.revoked > 0 && <SummaryRow icon={Ban} tone="text-destructive" label="Revoked — left out" value={c.revoked} />}
          </ul>

          {nothing && (
            <Alert tone="warning" title="Nothing to send">
              Every key here was issued before keys were stored{c.revoked ? " or is revoked" : ""}. Reissue unused keys from the
              keys table first.
            </Alert>
          )}
          {failure && (
            <Alert tone="danger" title={failure.title}>
              {failure.body}
            </Alert>
          )}

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => onOpenChange(false)} disabled={busy}>
              Cancel
            </Button>
            <Button type="submit" disabled={busy || nothing}>
              {busy ? <Loader2 className="animate-spin" /> : <Send />}
              {busy ? "Sending…" : `Send ${c.usable} key${c.usable === 1 ? "" : "s"}`}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function SummaryRow({
  icon: Icon,
  tone,
  label,
  value,
}: {
  icon: React.ElementType;
  tone: string;
  label: string;
  value: number;
}) {
  return (
    <li className="flex items-center gap-2 px-3 py-2">
      <Icon className={`h-4 w-4 shrink-0 ${tone}`} aria-hidden />
      <span className="flex-1 text-muted-foreground">{label}</span>
      <span className="font-medium tabular-nums">{value}</span>
    </li>
  );
}

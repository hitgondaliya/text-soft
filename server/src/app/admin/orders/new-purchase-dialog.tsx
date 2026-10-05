"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import { ArrowRight, CheckCircle2, Download, Info, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert } from "@/components/ui/alert";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { CopyButton } from "@/components/copy-button";
import { PhoneField, type PhoneValue } from "@/components/phone-field";
import { DEFAULT_COUNTRY } from "@/lib/countries";
import { cn } from "@/lib/utils";
import { downloadText, keysClipboardText, keysFileText } from "./keys-text";

type Created = {
  serial: string;
  created: Array<{ seq: number; key: string }>;
  emailed?: boolean;
  emailTo?: string | null;
  emailError?: string;
};

export function NewPurchaseDialog({
  open,
  onOpenChange,
  onCreated,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  onCreated: () => void;
}) {
  const [customerName, setCustomerName] = useState("");
  const [customerEmail, setCustomerEmail] = useState("");
  const [phone, setPhone] = useState<PhoneValue>({ country: DEFAULT_COUNTRY, number: "" });
  const [phoneBad, setPhoneBad] = useState(false);
  const [company, setCompany] = useState("");
  const [edition, setEdition] = useState("full");
  const [mode, setMode] = useState<"days" | "date" | "perpetual">("days");
  const [days, setDays] = useState(365);
  const [expiryDate, setExpiryDate] = useState("");
  const [quantity, setQuantity] = useState(1);
  const [note, setNote] = useState("");
  const [sendEmail, setSendEmail] = useState(true);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState<Created | null>(null);
  // What the success screen needs for the .txt file.
  const [doneInfo, setDoneInfo] = useState<{ customerName: string; company: string; edition: string; expiryUtc: string | null } | null>(
    null,
  );

  // Start clean each time the dialog opens. (Resetting on close would swap the
  // success screen for an empty form while the dialog fades out.)
  useEffect(() => {
    if (open) reset();
  }, [open]);

  function reset() {
    setDone(null);
    setDoneInfo(null);
    setCustomerName("");
    setCustomerEmail("");
    setPhone({ country: DEFAULT_COUNTRY, number: "" });
    setPhoneBad(false);
    setCompany("");
    setQuantity(1);
    setNote("");
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (busy) return;
    if (!customerName.trim()) {
      toast.error("Customer name is required — the serial is filed under it.");
      return;
    }
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > 500) {
      toast.error("Number of keys must be between 1 and 500.");
      return;
    }
    if (mode === "days" && (!Number.isInteger(days) || days < 1 || days > 3650)) {
      toast.error("Days must be between 1 and 3650.");
      return;
    }
    setBusy(true);
    setPhoneBad(false);
    const body: Record<string, unknown> = {
      customerName,
      customerEmail: customerEmail || undefined,
      customerPhone: phone.number.trim() || undefined,
      customerPhoneCountry: phone.country,
      company: company || undefined,
      edition,
      quantity,
      note: note || undefined,
      sendEmail: sendEmail && !!customerEmail,
    };
    let expiryUtc: string | null = null;
    if (mode === "days") {
      body.durationDays = days;
      const d = new Date();
      d.setUTCDate(d.getUTCDate() + days);
      expiryUtc = d.toISOString();
    } else if (mode === "date" && expiryDate) {
      expiryUtc = new Date(expiryDate + "T23:59:59Z").toISOString();
      body.expiryUtc = expiryUtc;
    } else if (mode === "perpetual") body.perpetual = true;

    try {
      const res = await fetch("/api/admin/orders", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(body),
      });
      if (res.ok) {
        const d: Created = await res.json();
        setDone(d);
        setDoneInfo({ customerName, company, edition, expiryUtc });
        onCreated();
        toast.success(`${d.serial} created — ${d.created.length} key${d.created.length > 1 ? "s" : ""} issued`);
      } else {
        const err = await res.json().catch(() => ({}));
        if (err?.field === "customerPhone") {
          setPhoneBad(true);
          toast.error("That mobile number is not valid for the selected country.");
        } else if (res.status === 401) {
          toast.error("Your session has expired. Sign in again.");
        } else {
          toast.error("Could not create the purchase. Check the fields.");
        }
      }
    } catch {
      toast.error("Couldn't reach the server. Nothing was created.");
    } finally {
      setBusy(false);
    }
  }

  const n = done?.created.length ?? 0;

  return (
    <Dialog
      open={open}
      onOpenChange={(v) => {
        if (busy) return;
        onOpenChange(v);
      }}
    >
      <DialogContent className="gap-3 sm:max-w-2xl">
        {done ? (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <CheckCircle2 className="h-5 w-5 shrink-0 text-success" aria-hidden />
                <span>
                  <span className="font-mono">{done.serial}</span> created
                </span>
              </DialogTitle>
              <DialogDescription>
                {n} key{n > 1 ? "s" : ""} issued. Give the customer the serial <strong className="text-foreground">and</strong> one
                key per PC — the installer asks for both.
              </DialogDescription>
            </DialogHeader>

            {done.emailTo && (
              <Alert tone={done.emailed ? "success" : "warning"}>
                {done.emailed
                  ? `Emailed the serial and keys to ${done.emailTo}.`
                  : `Could not email ${done.emailTo}: ${done.emailError ?? "unknown error"}. Send the keys below by hand, or use Resend email on the purchase page.`}
              </Alert>
            )}

            <div className="overflow-hidden rounded-md border">
              <div className="flex items-center gap-3 border-b bg-muted/40 py-1.5 pl-3 pr-1.5">
                <span className="w-12 shrink-0 text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Serial</span>
                <code className="min-w-0 flex-1 truncate font-mono text-[13px] font-semibold">{done.serial}</code>
                <CopyButton value={done.serial} ariaLabel="Copy serial" toastText="Serial copied" size="icon-xs" />
              </div>
              <ul className="max-h-[min(18rem,40dvh)] divide-y overflow-y-auto">
                {done.created.map((c) => (
                  <li key={c.seq} className="flex items-center gap-3 py-1 pl-3 pr-1.5 hover:bg-muted/30">
                    <span className="w-12 shrink-0 text-xs tabular-nums text-muted-foreground">Key {c.seq}</span>
                    <code className="min-w-0 flex-1 select-all truncate font-mono text-[13px] tracking-wide">{c.key}</code>
                    <CopyButton value={c.key} ariaLabel={`Copy key ${c.seq}`} toastText={`Key ${c.seq} copied`} size="icon-xs" />
                  </li>
                ))}
              </ul>
            </div>

            <p className="flex items-start gap-1.5 text-xs text-muted-foreground">
              <Info className="mt-px h-3.5 w-3.5 shrink-0" aria-hidden />
              The keys are saved. You can copy them again at any time from the purchase page.
            </p>

            <DialogFooter className="mt-1">
              <Button
                variant="outline"
                onClick={() => {
                  downloadText(
                    `${done.serial}-keys.txt`,
                    keysFileText({ serial: done.serial, ...(doneInfo ?? {}) }, done.created),
                  );
                }}
              >
                <Download /> Download .txt
              </Button>
              <CopyButton
                value={keysClipboardText(done.serial, done.created)}
                label="Copy all"
                variant="outline"
                size="default"
                toastText={`Copied the serial and ${n} key${n > 1 ? "s" : ""}`}
              />
              <Button asChild>
                <Link href={`/admin/orders/${encodeURIComponent(done.serial)}`}>
                  Open purchase <ArrowRight />
                </Link>
              </Button>
            </DialogFooter>
          </>
        ) : (
          <form onSubmit={submit} className="grid gap-4">
            <DialogHeader>
              <DialogTitle>New purchase</DialogTitle>
              <DialogDescription>One serial for this sale; every key is issued under it. Each key works on one PC.</DialogDescription>
            </DialogHeader>

            <FieldGroup title="Customer">
              <Field label="Customer name" htmlFor="np-name" required>
                <Input
                  id="np-name"
                  value={customerName}
                  onChange={(e) => setCustomerName(e.target.value)}
                  autoFocus
                  placeholder="Nishant Patel"
                  maxLength={128}
                />
              </Field>
              <Field label="Company" htmlFor="np-company">
                <Input
                  id="np-company"
                  value={company}
                  onChange={(e) => setCompany(e.target.value)}
                  placeholder="Patel Printers"
                  maxLength={128}
                />
              </Field>
              <Field label="Email" htmlFor="np-email">
                <Input
                  id="np-email"
                  type="email"
                  value={customerEmail}
                  onChange={(e) => setCustomerEmail(e.target.value)}
                  placeholder="name@company.com"
                />
              </Field>
              <Field label="Mobile" hint={phoneBad ? "Not a valid number for that country." : undefined} error={phoneBad}>
                <PhoneField
                  value={phone}
                  invalid={phoneBad}
                  onChange={(v) => {
                    setPhone(v);
                    setPhoneBad(false);
                  }}
                />
              </Field>
            </FieldGroup>

            <FieldGroup title="Licence" className="sm:grid-cols-4">
              <Field label="Edition">
                <Select value={edition} onValueChange={setEdition}>
                  <SelectTrigger aria-label="Edition">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="full">Full</SelectItem>
                    <SelectItem value="demo">Demo</SelectItem>
                  </SelectContent>
                </Select>
              </Field>
              <Field label="Keys" htmlFor="np-qty" hint="One per PC, up to 500">
                <Input
                  id="np-qty"
                  type="number"
                  inputMode="numeric"
                  min={1}
                  max={500}
                  value={Number.isNaN(quantity) ? "" : quantity}
                  onChange={(e) => setQuantity(e.target.valueAsNumber)}
                />
              </Field>
              <Field label="Validity">
                <Select value={mode} onValueChange={(v) => setMode(v as typeof mode)}>
                  <SelectTrigger aria-label="Validity">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="days">Number of days</SelectItem>
                    <SelectItem value="date">Until a date</SelectItem>
                    <SelectItem value="perpetual">Perpetual</SelectItem>
                  </SelectContent>
                </Select>
              </Field>
              {mode === "days" && (
                <Field label="Days" htmlFor="np-days">
                  <Input
                    id="np-days"
                    type="number"
                    inputMode="numeric"
                    min={1}
                    max={3650}
                    value={Number.isNaN(days) ? "" : days}
                    onChange={(e) => setDays(e.target.valueAsNumber)}
                  />
                </Field>
              )}
              {mode === "date" && (
                <Field label="Expiry date" htmlFor="np-date">
                  <Input id="np-date" type="date" value={expiryDate} onChange={(e) => setExpiryDate(e.target.value)} required />
                </Field>
              )}
              {mode === "perpetual" && (
                <Field label="Expiry">
                  <div className="flex h-9 items-center text-[13px] text-muted-foreground">Never expires</div>
                </Field>
              )}
              <Field label="Note" htmlFor="np-note" className="col-span-full">
                <Input
                  id="np-note"
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                  placeholder="Invoice number, reseller, anything useful later"
                  maxLength={500}
                />
              </Field>
            </FieldGroup>

            <label
              className={cn(
                "flex cursor-pointer items-start gap-2.5 rounded-md border px-3 py-2.5 text-[13px] transition-colors hover:bg-muted/40",
                !customerEmail && "cursor-default opacity-70 hover:bg-transparent",
              )}
            >
              <input
                type="checkbox"
                className="mt-0.5 h-4 w-4 accent-primary"
                checked={sendEmail && !!customerEmail}
                disabled={!customerEmail}
                onChange={(e) => setSendEmail(e.target.checked)}
              />
              <span className="min-w-0">
                <span className="font-medium">Email the customer</span>
                <span className="block text-xs text-muted-foreground">
                  {customerEmail
                    ? `The serial and every key go to ${customerEmail} as soon as the purchase is created.`
                    : "Enter an email above to send the serial and keys automatically."}
                </span>
              </span>
            </label>

            <DialogFooter>
              <Button type="button" variant="ghost" onClick={() => onOpenChange(false)} disabled={busy}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="animate-spin" />}
                {busy
                  ? "Creating…"
                  : Number.isInteger(quantity) && quantity > 0
                    ? `Create & issue ${quantity} key${quantity === 1 ? "" : "s"}`
                    : "Create & issue keys"}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}

function FieldGroup({ title, className, children }: { title: string; className?: string; children: React.ReactNode }) {
  return (
    <fieldset className="min-w-0">
      <legend className="mb-2 text-[11px] font-medium uppercase tracking-wide text-muted-foreground">{title}</legend>
      <div className={cn("grid gap-3 sm:grid-cols-2", className)}>{children}</div>
    </fieldset>
  );
}

function Field({
  label,
  htmlFor,
  hint,
  required,
  error,
  className,
  children,
}: {
  label: string;
  htmlFor?: string;
  hint?: string;
  required?: boolean;
  error?: boolean;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <div className={cn("min-w-0 space-y-1.5", className)}>
      <Label htmlFor={htmlFor} className="text-xs">
        {label}
        {required && <span className="ml-0.5 text-primary-strong">*</span>}
      </Label>
      {children}
      {hint && <p className={cn("text-[11px] leading-4", error ? "text-destructive" : "text-muted-foreground")}>{hint}</p>}
    </div>
  );
}

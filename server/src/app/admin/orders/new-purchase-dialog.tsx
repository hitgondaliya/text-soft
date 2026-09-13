"use client";

import { useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import { Copy, Download, ArrowRight, Loader2, CheckCircle2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

type Created = { serial: string; created: Array<{ seq: number; key: string }> };

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
  const [customerPhone, setCustomerPhone] = useState("");
  const [company, setCompany] = useState("");
  const [edition, setEdition] = useState("full");
  const [mode, setMode] = useState<"days" | "date" | "perpetual">("days");
  const [days, setDays] = useState(365);
  const [expiryDate, setExpiryDate] = useState("");
  const [deviceLimit, setDeviceLimit] = useState(1);
  const [quantity, setQuantity] = useState(1);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState<Created | null>(null);

  function reset() {
    setDone(null);
    setCustomerName("");
    setCustomerEmail("");
    setCustomerPhone("");
    setCompany("");
    setQuantity(1);
    setNote("");
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!customerName.trim()) {
      toast.error("Customer name is required — the serial is filed under it.");
      return;
    }
    setBusy(true);
    const body: Record<string, unknown> = {
      customerName,
      customerEmail: customerEmail || undefined,
      customerPhone: customerPhone || undefined,
      company: company || undefined,
      edition,
      deviceLimit,
      quantity,
      note: note || undefined,
    };
    if (mode === "days") body.durationDays = days;
    else if (mode === "date" && expiryDate) body.expiryUtc = new Date(expiryDate + "T23:59:59Z").toISOString();
    else if (mode === "perpetual") body.perpetual = true;

    const res = await fetch("/api/admin/orders", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body),
    });
    setBusy(false);
    if (res.ok) {
      const d: Created = await res.json();
      setDone(d);
      onCreated();
      toast.success(`${d.serial} created — ${d.created.length} key${d.created.length > 1 ? "s" : ""} issued`);
    } else {
      toast.error("Could not create the purchase. Check the fields.");
    }
  }

  const lines = done ? done.created.map((c) => `${done.serial}-${String(c.seq).padStart(3, "0")}\t${c.key}`) : [];
  const text = lines.join("\n");

  function download() {
    const blob = new Blob([`serial\tkey\n${text}\n`], { type: "text/tab-separated-values" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = `${done!.serial}.tsv`;
    a.click();
    URL.revokeObjectURL(a.href);
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(v) => {
        onOpenChange(v);
        if (!v) reset();
      }}
    >
      <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-2xl">
        {done ? (
          <>
            <DialogHeader>
              <div className="mb-2 inline-flex h-10 w-10 items-center justify-center rounded-full bg-success/10 text-success">
                <CheckCircle2 className="h-5 w-5" />
              </div>
              <DialogTitle>
                Serial <span className="font-mono text-primary">{done.serial}</span>
              </DialogTitle>
              <DialogDescription>
                {done.created.length} key{done.created.length > 1 ? "s" : ""} issued. Copy them now — the raw keys are
                shown once and the server keeps only their hashes. The serial stays on record forever.
              </DialogDescription>
            </DialogHeader>
            <Textarea readOnly value={text} rows={Math.min(done.created.length + 1, 12)} className="font-mono text-xs" />
            <DialogFooter className="gap-2 sm:gap-2">
              <Button
                variant="outline"
                onClick={() => {
                  navigator.clipboard.writeText(text);
                  toast.success("Copied to clipboard");
                }}
              >
                <Copy /> Copy all
              </Button>
              <Button variant="outline" onClick={download}>
                <Download /> Download .tsv
              </Button>
              <Button asChild>
                <Link href={`/admin/orders/${done.serial}`}>
                  Open purchase <ArrowRight />
                </Link>
              </Button>
            </DialogFooter>
          </>
        ) : (
          <form onSubmit={submit}>
            <DialogHeader>
              <DialogTitle>New purchase</DialogTitle>
              <DialogDescription>
                One serial number is created for this sale and every key is issued under it.
              </DialogDescription>
            </DialogHeader>

            <div className="mt-6 grid gap-5 sm:grid-cols-2">
              <Field label="Customer name" required>
                <Input value={customerName} onChange={(e) => setCustomerName(e.target.value)} autoFocus placeholder="Nishant Patel" />
              </Field>
              <Field label="Company">
                <Input value={company} onChange={(e) => setCompany(e.target.value)} placeholder="Patel Printers" />
              </Field>
              <Field label="Email">
                <Input type="email" value={customerEmail} onChange={(e) => setCustomerEmail(e.target.value)} placeholder="name@company.com" />
              </Field>
              <Field label="Phone">
                <Input value={customerPhone} onChange={(e) => setCustomerPhone(e.target.value)} placeholder="+91 98250 00000" />
              </Field>

              <div className="col-span-full my-1 h-px bg-border" />

              <Field label="Edition">
                <Select value={edition} onValueChange={setEdition}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="full">Full</SelectItem>
                    <SelectItem value="demo">Demo</SelectItem>
                  </SelectContent>
                </Select>
              </Field>
              <Field label="How many keys" hint="One per licence sold, up to 500">
                <Input type="number" min={1} max={500} value={quantity} onChange={(e) => setQuantity(+e.target.value)} />
              </Field>
              <Field label="Validity">
                <Select value={mode} onValueChange={(v) => setMode(v as typeof mode)}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="days">For a number of days</SelectItem>
                    <SelectItem value="date">Until a date</SelectItem>
                    <SelectItem value="perpetual">Perpetual</SelectItem>
                  </SelectContent>
                </Select>
              </Field>
              {mode === "days" && (
                <Field label="Days">
                  <Input type="number" min={1} value={days} onChange={(e) => setDays(+e.target.value)} />
                </Field>
              )}
              {mode === "date" && (
                <Field label="Expiry date">
                  <Input type="date" value={expiryDate} onChange={(e) => setExpiryDate(e.target.value)} required />
                </Field>
              )}
              {mode === "perpetual" && (
                <Field label="Expiry">
                  <div className="flex h-9 items-center text-sm text-muted-foreground">Never expires</div>
                </Field>
              )}
              <Field label="Devices per key" hint="How many PCs one key may activate">
                <Input type="number" min={1} max={100} value={deviceLimit} onChange={(e) => setDeviceLimit(+e.target.value)} />
              </Field>
              <Field label="Note" className="sm:col-span-2">
                <Input value={note} onChange={(e) => setNote(e.target.value)} placeholder="Invoice number, reseller, anything useful later" />
              </Field>
            </div>

            <DialogFooter className="mt-6">
              <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="animate-spin" />}
                {busy ? "Creating…" : `Create & issue ${quantity} key${quantity > 1 ? "s" : ""}`}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}

function Field({
  label,
  hint,
  required,
  className,
  children,
}: {
  label: string;
  hint?: string;
  required?: boolean;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <div className={`space-y-2 ${className ?? ""}`}>
      <Label>
        {label}
        {required && <span className="ml-0.5 text-primary">*</span>}
      </Label>
      {children}
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}

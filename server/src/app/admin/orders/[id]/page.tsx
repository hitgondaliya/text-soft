"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import {
  ArrowLeft,
  KeyRound,
  MonitorSmartphone,
  Ban,
  Users,
  CalendarPlus,
  Infinity as InfinityIcon,
  RotateCcw,
  Building2,
  Mail,
  Phone,
  StickyNote,
  Loader2,
} from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { EditionBadge, StatusBadge } from "@/components/status-badge";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { fmtDate, fmtDateTime, fmtRelative } from "@/lib/utils";

interface KeyRow {
  id: string;
  seq: number;
  prefix: string;
  status: string;
  expired: boolean;
  deviceLimit: number;
  devicesUsed: number;
  lastSeenUtc: string | null;
}

interface Order {
  id: string;
  serial: string;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  company: string;
  edition: string;
  deviceLimit: number;
  quantity: number;
  expiryUtc: string | null;
  expired: boolean;
  note: string;
  createdBy: string;
  createdUtc: string;
  keys: KeyRow[];
}

export default function OrderDetail({ params }: { params: { id: string } }) {
  const [o, setO] = useState<Order | null>(null);
  const [busy, setBusy] = useState(false);
  const [confirmRevoke, setConfirmRevoke] = useState(false);

  const load = useCallback(() => {
    fetch(`/api/admin/orders/${params.id}`)
      .then((r) => (r.ok ? r.json() : null))
      .then(setO)
      .catch(() => {});
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  async function act(body: Record<string, unknown>, success: string) {
    setBusy(true);
    const res = await fetch(`/api/admin/orders/${params.id}`, {
      method: "PATCH",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body),
    });
    setBusy(false);
    if (res.ok) toast.success(success);
    else toast.error("That didn't work. Try again.");
    load();
  }

  if (!o) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
          {[0, 1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-24" />
          ))}
        </div>
        <Skeleton className="h-64" />
      </div>
    );
  }

  const revoked = o.keys.filter((k) => k.status === "revoked").length;
  const inUse = o.keys.filter((k) => k.devicesUsed > 0).length;
  const allRevoked = o.keys.length > 0 && revoked === o.keys.length;

  return (
    <div>
      <Button variant="ghost" size="sm" className="-ml-2 mb-2 text-muted-foreground" asChild>
        <Link href="/admin/orders">
          <ArrowLeft /> Purchases
        </Link>
      </Button>

      <PageHeader
        title={
          <span className="flex flex-wrap items-center gap-3">
            <span className="font-mono">{o.serial}</span>
            <EditionBadge edition={o.edition} />
            {allRevoked ? (
              <Badge variant="destructive">Revoked</Badge>
            ) : o.expired ? (
              <Badge variant="warning">Expired</Badge>
            ) : (
              <Badge variant="success">Active</Badge>
            )}
          </span>
        }
        description={
          <>
            Sold {fmtDateTime(o.createdUtc)} by {o.createdBy}
          </>
        }
      />

      <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
        <Stat icon={KeyRound} label="Keys issued" value={o.keys.length} />
        <Stat icon={MonitorSmartphone} label="Keys in use" value={inUse} tone="text-primary bg-primary/10" />
        <Stat icon={Ban} label="Revoked" value={revoked} tone="text-destructive bg-destructive/10" />
        <Stat icon={Users} label="Devices per key" value={o.deviceLimit} />
      </div>

      <div className="mt-6 grid gap-6 lg:grid-cols-[1fr_1.4fr]">
        <Card>
          <CardHeader>
            <CardTitle>Customer</CardTitle>
            <CardDescription>Who this serial is filed under.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 text-sm">
            <div>
              <div className="text-lg font-semibold">{o.customerName}</div>
              {o.company && (
                <div className="mt-0.5 flex items-center gap-2 text-muted-foreground">
                  <Building2 className="h-3.5 w-3.5" /> {o.company}
                </div>
              )}
            </div>
            <div className="space-y-2 text-muted-foreground">
              <div className="flex items-center gap-2">
                <Mail className="h-3.5 w-3.5" />
                {o.customerEmail ? (
                  <a href={`mailto:${o.customerEmail}`} className="text-foreground hover:underline">
                    {o.customerEmail}
                  </a>
                ) : (
                  "—"
                )}
              </div>
              <div className="flex items-center gap-2">
                <Phone className="h-3.5 w-3.5" /> {o.customerPhone || "—"}
              </div>
              <div className="flex items-start gap-2">
                <StickyNote className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {o.note || "—"}
              </div>
            </div>
            <div className="rounded-lg bg-muted/60 p-3">
              <div className="text-xs font-medium uppercase tracking-wide text-muted-foreground">Valid until</div>
              <div className="mt-1 flex items-center gap-2 font-medium">
                {o.expiryUtc ? fmtDate(o.expiryUtc) : "Perpetual — never expires"}
                {o.expiryUtc && !o.expired && (
                  <span className="text-xs font-normal text-muted-foreground">({fmtRelative(o.expiryUtc)})</span>
                )}
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Whole purchase</CardTitle>
            <CardDescription>
              These act on all {o.keys.length} keys under this serial at once.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            <Button
              variant="outline"
              disabled={busy}
              onClick={() => act({ action: "extendAll", days: 365 }, "Extended every key by one year")}
            >
              <CalendarPlus /> Extend 1 year
            </Button>
            <Button
              variant="outline"
              disabled={busy}
              onClick={() => act({ action: "extendAll", days: 30 }, "Extended every key by 30 days")}
            >
              <CalendarPlus /> Extend 30 days
            </Button>
            <Button
              variant="outline"
              disabled={busy}
              onClick={() => act({ action: "setExpiryAll", expiryUtc: null }, "This purchase never expires now")}
            >
              <InfinityIcon /> Make perpetual
            </Button>
            {allRevoked ? (
              <Button disabled={busy} onClick={() => act({ action: "unrevokeAll" }, "All keys are active again")}>
                <RotateCcw /> Un-revoke all
              </Button>
            ) : (
              <Button variant="destructive" disabled={busy} onClick={() => setConfirmRevoke(true)}>
                <Ban /> Revoke all
              </Button>
            )}
            {busy && <Loader2 className="ml-2 h-4 w-4 animate-spin self-center text-muted-foreground" />}
          </CardContent>
        </Card>
      </div>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>Keys</CardTitle>
          <CardDescription>
            {o.keys.length} issued under {o.serial}. Open one to see its devices or act on it alone.
          </CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-12">#</TableHead>
                <TableHead>Key</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Devices</TableHead>
                <TableHead className="hidden text-right sm:table-cell">Last seen</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {o.keys.map((k) => (
                <TableRow key={k.id}>
                  <TableCell className="text-muted-foreground tabular-nums">{k.seq}</TableCell>
                  <TableCell className="whitespace-nowrap font-mono">
                    <Link href={`/admin/keys/${k.id}`} className="text-primary hover:underline">
                      {k.prefix}…
                    </Link>
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={k.status} expired={k.expired} />
                  </TableCell>
                  <TableCell className="text-right tabular-nums">
                    {k.devicesUsed}/{k.deviceLimit}
                  </TableCell>
                  <TableCell className="hidden text-right text-muted-foreground sm:table-cell">
                    {k.lastSeenUtc ? fmtRelative(k.lastSeenUtc) : "never used"}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Dialog open={confirmRevoke} onOpenChange={setConfirmRevoke}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Revoke all {o.keys.length} keys?</DialogTitle>
            <DialogDescription>
              Every device using a key from {o.serial} stops working at its next check-in. You can un-revoke
              later.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setConfirmRevoke(false)}>
              Keep them
            </Button>
            <Button
              variant="destructive"
              onClick={() => {
                setConfirmRevoke(false);
                act({ action: "revokeAll" }, `Revoked all keys of ${o.serial}`);
              }}
            >
              <Ban /> Revoke all
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Stat({
  icon: Icon,
  label,
  value,
  tone = "text-foreground bg-muted",
}: {
  icon: React.ElementType;
  label: string;
  value: number;
  tone?: string;
}) {
  return (
    <Card>
      <CardContent className="flex items-center gap-4 p-5">
        <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${tone}`}>
          <Icon className="h-4 w-4" />
        </div>
        <div>
          <div className="text-2xl font-bold tracking-tight">{value}</div>
          <div className="text-xs font-medium text-muted-foreground">{label}</div>
        </div>
      </CardContent>
    </Card>
  );
}

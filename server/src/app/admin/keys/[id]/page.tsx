"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { toast } from "sonner";
import { ArrowLeft, CalendarPlus, Ban, RotateCcw, MonitorSmartphone, Unplug, Loader2 } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { EditionBadge, StatusBadge } from "@/components/status-badge";
import { fmtDate, fmtDateTime, fmtRelative } from "@/lib/utils";

interface Activation {
  id: string;
  deviceFp: string;
  deviceName: string;
  ip: string;
  appVersion: string;
  status: string;
  activatedUtc: string;
  lastSeenUtc: string;
}
interface Detail {
  id: string;
  prefix: string;
  serial?: string;
  seq?: number;
  edition: string;
  status: string;
  expired: boolean;
  expiryUtc: string | null;
  deviceLimit: number;
  customerName: string;
  customerEmail: string;
  note: string;
  createdBy: string;
  createdUtc: string;
  activations: Activation[];
}

export default function KeyDetail() {
  const { id } = useParams<{ id: string }>();
  const [d, setD] = useState<Detail | null>(null);
  const [extendDays, setExtendDays] = useState(30);
  const [limit, setLimit] = useState(1);
  const [busy, setBusy] = useState(false);

  const load = useCallback(() => {
    fetch(`/api/admin/keys/${id}`)
      .then((r) => r.json())
      .then((x) => {
        setD(x);
        if (x?.deviceLimit) setLimit(x.deviceLimit);
      })
      .catch(() => {});
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  async function patch(body: Record<string, unknown>, success: string) {
    setBusy(true);
    const res = await fetch(`/api/admin/keys/${id}`, {
      method: "PATCH",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body),
    });
    setBusy(false);
    if (res.ok) toast.success(success);
    else toast.error("That didn't work. Try again.");
    load();
  }

  if (!d) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-56" />
        <div className="grid gap-6 lg:grid-cols-2">
          <Skeleton className="h-64" />
          <Skeleton className="h-64" />
        </div>
      </div>
    );
  }

  const active = d.activations.filter((a) => a.status === "active").length;

  return (
    <div>
      <Button variant="ghost" size="sm" className="-ml-2 mb-2 text-muted-foreground" asChild>
        <Link href={d.serial ? `/admin/orders/${d.serial}` : "/admin/keys"}>
          <ArrowLeft /> {d.serial ?? "License keys"}
        </Link>
      </Button>

      <PageHeader
        title={
          <span className="flex flex-wrap items-center gap-3">
            <span className="font-mono">{d.prefix}…</span>
            <EditionBadge edition={d.edition} />
            <StatusBadge status={d.status} expired={d.expired} />
          </span>
        }
        description={
          d.serial ? (
            <>
              Key #{d.seq} of purchase{" "}
              <Link href={`/admin/orders/${d.serial}`} className="font-mono text-primary hover:underline">
                {d.serial}
              </Link>
            </>
          ) : (
            "Issued before purchases existed"
          )
        }
      />

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Details</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="divide-y text-sm">
              <Row label="Customer" value={d.customerName || "—"} />
              <Row label="Email" value={d.customerEmail || "—"} />
              <Row label="Valid until" value={d.expiryUtc ? `${fmtDate(d.expiryUtc)} (${fmtRelative(d.expiryUtc)})` : "Perpetual"} />
              <Row label="Device limit" value={String(d.deviceLimit)} />
              <Row label="Note" value={d.note || "—"} />
              <Row label="Issued" value={`${fmtDateTime(d.createdUtc)} by ${d.createdBy}`} />
            </dl>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>This key only</CardTitle>
            <CardDescription>
              To change every key of the sale at once, use the purchase page instead.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="flex items-end gap-2">
              <div className="flex-1 space-y-2">
                <Label htmlFor="extend">Extend by days</Label>
                <Input id="extend" type="number" min={1} value={extendDays} onChange={(e) => setExtendDays(+e.target.value)} />
              </div>
              <Button
                variant="outline"
                disabled={busy}
                onClick={() => patch({ action: "extend", days: extendDays }, `Extended by ${extendDays} days`)}
              >
                <CalendarPlus /> Extend
              </Button>
            </div>
            <div className="flex items-end gap-2">
              <div className="flex-1 space-y-2">
                <Label htmlFor="limit">Device limit</Label>
                <Input id="limit" type="number" min={1} max={100} value={limit} onChange={(e) => setLimit(+e.target.value)} />
              </div>
              <Button
                variant="outline"
                disabled={busy}
                onClick={() => patch({ action: "setDeviceLimit", deviceLimit: limit }, `Device limit set to ${limit}`)}
              >
                <MonitorSmartphone /> Set
              </Button>
            </div>
            <div className="border-t pt-5">
              {d.status === "revoked" ? (
                <Button disabled={busy} onClick={() => patch({ action: "unrevoke" }, "Key is active again")}>
                  <RotateCcw /> Un-revoke key
                </Button>
              ) : (
                <Button
                  variant="destructive"
                  disabled={busy}
                  onClick={() => patch({ action: "revoke" }, "Key revoked")}
                >
                  <Ban /> Revoke key
                </Button>
              )}
              {busy && <Loader2 className="ml-3 inline h-4 w-4 animate-spin text-muted-foreground" />}
            </div>
          </CardContent>
        </Card>
      </div>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>
            Devices{" "}
            <span className="ml-1 font-normal text-muted-foreground">
              {active}/{d.deviceLimit}
            </span>
          </CardTitle>
          <CardDescription>Each PC that activated with this key. Free a slot to let another one in.</CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          {d.activations.length === 0 ? (
            <div className="px-6 pb-8 pt-2 text-center text-sm text-muted-foreground">
              Not activated on any device yet.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Device</TableHead>
                  <TableHead className="hidden md:table-cell">Fingerprint</TableHead>
                  <TableHead className="hidden lg:table-cell">IP</TableHead>
                  <TableHead className="hidden sm:table-cell">Version</TableHead>
                  <TableHead>Last seen</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {d.activations.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell className="font-medium">{a.deviceName || "Unnamed device"}</TableCell>
                    <TableCell className="hidden font-mono text-xs text-muted-foreground md:table-cell">
                      {a.deviceFp.slice(0, 16)}…
                    </TableCell>
                    <TableCell className="hidden text-muted-foreground lg:table-cell">{a.ip}</TableCell>
                    <TableCell className="hidden text-muted-foreground sm:table-cell">{a.appVersion || "—"}</TableCell>
                    <TableCell className="text-muted-foreground">{fmtRelative(a.lastSeenUtc)}</TableCell>
                    <TableCell>
                      {a.status === "active" ? (
                        <Badge variant="success">Active</Badge>
                      ) : (
                        <Badge variant="secondary">Freed</Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      {a.status === "active" && (
                        <Button
                          variant="ghost"
                          size="sm"
                          disabled={busy}
                          onClick={() =>
                            patch({ action: "deactivateDevice", deviceFp: a.deviceFp }, "Device slot freed")
                          }
                        >
                          <Unplug /> Free slot
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-4 py-2.5">
      <dt className="shrink-0 text-muted-foreground">{label}</dt>
      <dd className="text-right font-medium">{value}</dd>
    </div>
  );
}

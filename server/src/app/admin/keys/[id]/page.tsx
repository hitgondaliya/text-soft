"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { toast } from "sonner";
import {
  ArrowLeft,
  CalendarPlus,
  Ban,
  RotateCcw,
  MonitorSmartphone,
  ArrowRightLeft,
  Loader2,
  ShieldAlert,
  UserCheck,
  Mail,
  Phone,
  Clock,
  History,
} from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { EditionBadge, StatusBadge } from "@/components/status-badge";
import { PhoneDisplay } from "@/components/flag";
import { AttemptsTable, type AttemptRow } from "@/components/attempts-table";
import { fmtDate, fmtDateTime, fmtRelative } from "@/lib/utils";

interface EndUser {
  name: string;
  email: string;
  phoneCountry: string;
  phoneDisplay: string;
  registeredUtc: string;
}
interface Activation {
  id: string;
  deviceFp: string;
  deviceName: string;
  ip: string;
  appVersion: string;
  status: string;
  activatedUtc: string;
  lastSeenUtc: string;
  user: EndUser | null;
  releasedUtc: string | null;
  releasedBy: string;
  releaseReason: string;
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
  customerName: string;
  customerEmail: string;
  note: string;
  createdBy: string;
  createdUtc: string;
  device: Activation | null;
  history: Activation[];
  attempts: AttemptRow[];
}

export default function KeyDetail() {
  const { id } = useParams<{ id: string }>();
  const [d, setD] = useState<Detail | null>(null);
  const [extendDays, setExtendDays] = useState(30);
  const [busy, setBusy] = useState(false);
  const [moveOpen, setMoveOpen] = useState(false);
  const [moveReason, setMoveReason] = useState("");

  const load = useCallback(() => {
    fetch(`/api/admin/keys/${id}`)
      .then((r) => r.json())
      .then(setD)
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

  const dev = d.device;

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
            {d.attempts.length > 0 && (
              <Badge variant="destructive">
                <ShieldAlert className="h-3 w-3" /> {d.attempts.length} blocked
              </Badge>
            )}
          </span>
        }
        description={
          d.serial ? (
            <>
              Key #{d.seq} of purchase{" "}
              <Link href={`/admin/orders/${d.serial}`} className="font-mono text-primary hover:underline">
                {d.serial}
              </Link>
              {" · "}
              {d.customerName}
            </>
          ) : (
            "Issued before purchases existed"
          )
        }
      />

      <div className="grid gap-6 lg:grid-cols-2">
        {/* ---- The PC that holds the key, and the person on it ---- */}
        <Card className={dev ? "border-primary/30" : ""}>
          <CardHeader className="flex flex-row flex-wrap items-start justify-between gap-3 space-y-0">
            <div className="min-w-[10rem]">
              <CardTitle className="flex items-center gap-2">
                <MonitorSmartphone className="h-4 w-4 text-primary" /> Device
              </CardTitle>
              <CardDescription>The one PC this key is bound to.</CardDescription>
            </div>
            {dev && (
              <Button variant="outline" size="sm" disabled={busy} onClick={() => setMoveOpen(true)}>
                <ArrowRightLeft /> Move to another device
              </Button>
            )}
          </CardHeader>
          <CardContent className="text-sm">
            {!dev ? (
              <div className="rounded-lg border border-dashed p-6 text-center text-muted-foreground">
                Not activated on any PC yet. The first PC to install with this serial and key takes it.
              </div>
            ) : (
              <div className="space-y-4">
                <div className="flex items-start justify-between gap-4 rounded-lg bg-muted/60 p-3">
                  <div>
                    <div className="font-semibold">{dev.deviceName || "Unnamed PC"}</div>
                    <div className="mt-0.5 font-mono text-[11px] text-muted-foreground">{dev.deviceFp.slice(0, 24)}…</div>
                  </div>
                  <div className="text-right text-xs text-muted-foreground">
                    <div>Activated {fmtDate(dev.activatedUtc)}</div>
                    <div>Seen {fmtRelative(dev.lastSeenUtc)}</div>
                    <div>
                      {dev.ip} · v{dev.appVersion || "?"}
                    </div>
                  </div>
                </div>

                {dev.user ? (
                  <div>
                    <div className="mb-2 flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
                      <UserCheck className="h-3.5 w-3.5 text-success" /> Registered user
                    </div>
                    <div className="text-base font-semibold">{dev.user.name}</div>
                    <div className="mt-1 space-y-1 text-muted-foreground">
                      <div className="flex items-center gap-2">
                        <Mail className="h-3.5 w-3.5" />
                        <a href={`mailto:${dev.user.email}`} className="text-foreground hover:underline">
                          {dev.user.email}
                        </a>
                      </div>
                      <div className="flex items-center gap-2">
                        <Phone className="h-3.5 w-3.5" />
                        <PhoneDisplay country={dev.user.phoneCountry} text={dev.user.phoneDisplay} className="text-foreground" />
                      </div>
                      <div className="flex items-center gap-2">
                        <Clock className="h-3.5 w-3.5" /> Registered {fmtDateTime(dev.user.registeredUtc)}
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="flex items-center gap-2 rounded-lg border border-warning/40 bg-warning/10 p-3 text-warning">
                    <Clock className="h-4 w-4 shrink-0" />
                    Activated but the user has not filled in name, email and mobile yet. The app stays on the
                    registration screen until they do.
                  </div>
                )}
              </div>
            )}
          </CardContent>
        </Card>

        {/* ---- Actions on this key alone ---- */}
        <Card>
          <CardHeader>
            <CardTitle>This key only</CardTitle>
            <CardDescription>
              To change every key of the sale at once, use the purchase page instead.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            <dl className="divide-y text-sm">
              <Row label="Valid until" value={d.expiryUtc ? `${fmtDate(d.expiryUtc)} (${fmtRelative(d.expiryUtc)})` : "Perpetual"} />
              <Row label="Customer" value={d.customerName || "—"} />
              <Row label="Note" value={d.note || "—"} />
              <Row label="Issued" value={`${fmtDateTime(d.createdUtc)} by ${d.createdBy}`} />
            </dl>
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

      {/* ---- Attempts from other PCs ---- */}
      <Card className="mt-6">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <ShieldAlert className={`h-4 w-4 ${d.attempts.length ? "text-destructive" : "text-success"}`} />
            Blocked attempts
            {d.attempts.length > 0 && <span className="font-normal text-muted-foreground">{d.attempts.length}</span>}
          </CardTitle>
          <CardDescription>
            Every time this key was tried from a PC other than the one it is bound to, or with the wrong serial.
          </CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          <AttemptsTable rows={d.attempts} showKey={false} />
        </CardContent>
      </Card>

      {/* ---- Previous devices ---- */}
      {d.history.length > 0 && (
        <Card className="mt-6">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <History className="h-4 w-4" /> Previous devices
            </CardTitle>
            <CardDescription>PCs that held this key before it was moved.</CardDescription>
          </CardHeader>
          <CardContent className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>PC</TableHead>
                  <TableHead className="hidden sm:table-cell">User</TableHead>
                  <TableHead>Held</TableHead>
                  <TableHead>Released</TableHead>
                  <TableHead className="hidden md:table-cell">Why</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {d.history.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell>
                      <div className="font-medium">{a.deviceName || "Unnamed PC"}</div>
                      <div className="font-mono text-[11px] text-muted-foreground">{a.deviceFp.slice(0, 12)}…</div>
                    </TableCell>
                    <TableCell className="hidden sm:table-cell">{a.user?.name || <span className="text-muted-foreground">—</span>}</TableCell>
                    <TableCell className="whitespace-nowrap text-muted-foreground">
                      {fmtDate(a.activatedUtc)} → {a.releasedUtc ? fmtDate(a.releasedUtc) : "?"}
                    </TableCell>
                    <TableCell>
                      <Badge variant={a.status === "released" ? "warning" : "secondary"}>
                        {a.status === "released" ? `by ${a.releasedBy}` : "replaced"}
                      </Badge>
                    </TableCell>
                    <TableCell className="hidden text-muted-foreground md:table-cell">{a.releaseReason}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      <Dialog open={moveOpen} onOpenChange={setMoveOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Move this key to another device?</DialogTitle>
            <DialogDescription>
              <strong>{dev?.deviceName || "The current PC"}</strong> loses the licence at its next check-in (within 24
              hours, or immediately if it is online). The customer then enters the same serial and key on the new PC,
              which takes the key. The old PC cannot take it back.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="reason">Reason (kept on record)</Label>
            <Input
              id="reason"
              value={moveReason}
              onChange={(e) => setMoveReason(e.target.value)}
              placeholder="Old laptop stolen, customer called 13 Sep"
              autoFocus
            />
          </div>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setMoveOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => {
                setMoveOpen(false);
                patch({ action: "releaseDevice", reason: moveReason }, "Key released. The next PC to activate takes it.");
                setMoveReason("");
              }}
            >
              <ArrowRightLeft /> Release this device
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
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

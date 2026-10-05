"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { toast } from "sonner";
import {
  ArrowRightLeft,
  Ban,
  CalendarClock,
  CalendarPlus,
  Check,
  History,
  KeyRound,
  Loader2,
  MonitorOff,
  MonitorSmartphone,
  RefreshCw,
  RotateCcw,
  ShieldAlert,
  TriangleAlert,
  UserCheck,
  UserRound,
} from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { SectionCard } from "@/components/section-card";
import { EmptyState } from "@/components/empty-state";
import { KvList } from "@/components/kv-list";
import { CopyButton } from "@/components/copy-button";
import { SecretKey, revealKeyById } from "@/components/secret-key";
import { EditionBadge, StatusBadge } from "@/components/status-badge";
import { PhoneDisplay } from "@/components/flag";
import { AttemptsTable, type AttemptRow } from "@/components/attempts-table";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
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
import { cn, fmtDate, fmtDateTime, fmtRelative } from "@/lib/utils";

interface EndUser {
  name: string;
  email: string;
  phoneCountry: string;
  phoneE164: string;
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
  recoverable: boolean;
  reissuedUtc: string | null;
  reissuedBy: string;
  serial: string | null;
  seq: number | null;
  edition: string;
  status: string;
  expired: boolean;
  expiryUtc: string | null;
  customerName: string;
  customerEmail: string;
  note: string;
  createdBy: string;
  createdUtc: string;
  lastSeenUtc: string | null;
  device: Activation | null;
  history: Activation[];
  attempts: AttemptRow[];
}

type ReissueState =
  | { step: "confirm" }
  | { step: "busy" }
  | { step: "done"; key: string; prefix: string }
  | { step: "blocked"; reason: "in_use" | "revoked" | "conflict" | "failed"; deviceName?: string };

const EXTEND_PRESETS = [30, 90, 365];

/** What the customer needs to type: the serial and the key, ready to paste into WhatsApp or an email. */
function serialAndKey(serial: string | null, key: string): string {
  return serial ? `Serial: ${serial}\nKey: ${key}` : `Key: ${key}`;
}

export default function KeyDetail() {
  const { id } = useParams<{ id: string }>();
  const [d, setD] = useState<Detail | null>(null);
  const [loadError, setLoadError] = useState<"not_found" | "failed" | null>(null);
  const [loading, setLoading] = useState(true);
  const [extendDays, setExtendDays] = useState(30);
  const [busy, setBusy] = useState(false);
  const [moveOpen, setMoveOpen] = useState(false);
  const [moveReason, setMoveReason] = useState("");
  const [reissue, setReissue] = useState<ReissueState | null>(null);
  // Separate from the state so the dialog keeps its content while it animates closed.
  const [reissueOpen, setReissueOpen] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const r = await fetch(`/api/admin/keys/${id}`, { cache: "no-store" });
      if (r.status === 404) {
        setLoadError("not_found");
        return;
      }
      if (!r.ok) throw new Error(String(r.status));
      setD((await r.json()) as Detail);
      setLoadError(null);
    } catch {
      setLoadError("failed");
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  // One reveal per key string per visit: the eye, the copy buttons and "Copy serial & key"
  // all share it, so the audit log gets one "key.reveal", not one per click.
  const revealCache = useRef<{ prefix: string; p: Promise<string> } | null>(null);
  const prefix = d?.prefix ?? "";
  const reveal = useCallback((): Promise<string> => {
    const cached = revealCache.current;
    if (cached && cached.prefix === prefix) return cached.p;
    const p = revealKeyById(id).catch((err: unknown) => {
      if (revealCache.current?.p === p) revealCache.current = null;
      throw err;
    });
    revealCache.current = { prefix, p };
    return p;
  }, [id, prefix]);

  async function patch(body: Record<string, unknown>, success: string, undo?: Record<string, unknown>) {
    setBusy(true);
    try {
      const res = await fetch(`/api/admin/keys/${id}`, {
        method: "PATCH",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(body),
      });
      if (res.ok) {
        toast.success(
          success,
          undo ? { duration: 10000, action: { label: "Undo", onClick: () => void patch(undo, "Undone") } } : undefined,
        );
      } else {
        const err = (await res.json().catch(() => ({}))) as { error?: string };
        toast.error(err.error === "not_bound" ? "This key is not on any PC now." : "That didn't work. Try again.");
      }
    } catch {
      toast.error("That didn't work. Check your connection.");
    } finally {
      setBusy(false);
      void load();
    }
  }

  async function doReissue() {
    setReissue({ step: "busy" });
    try {
      const res = await fetch(`/api/admin/keys/${id}/reissue`, { method: "POST" });
      const body = (await res.json().catch(() => ({}))) as {
        key?: string;
        prefix?: string;
        error?: string;
        deviceName?: string;
      };
      if (res.ok && body.key && body.prefix) {
        // Seed the reveal cache so showing or copying the new key costs no second reveal.
        revealCache.current = { prefix: body.prefix, p: Promise.resolve(body.key) };
        setReissue({ step: "done", key: body.key, prefix: body.prefix });
        void load();
        return;
      }
      if (body.error === "in_use") setReissue({ step: "blocked", reason: "in_use", deviceName: body.deviceName ?? "" });
      else if (body.error === "revoked") setReissue({ step: "blocked", reason: "revoked" });
      else if (body.error === "conflict") setReissue({ step: "blocked", reason: "conflict" });
      else setReissue({ step: "blocked", reason: "failed" });
      void load();
    } catch {
      setReissue({ step: "blocked", reason: "failed" });
    }
  }

  function openReissue() {
    if (!d) return;
    // Known up front: no point asking for confirmation the server will refuse.
    if (d.device) setReissue({ step: "blocked", reason: "in_use", deviceName: d.device.deviceName });
    else if (d.status === "revoked") setReissue({ step: "blocked", reason: "revoked" });
    else setReissue({ step: "confirm" });
    setReissueOpen(true);
  }

  if (!d) {
    if (loadError) {
      return (
        <div>
          <PageHeader title="Licence key" back={{ href: "/admin/keys", label: "Licence keys" }} />
          <div className="rounded-lg border bg-card">
            <EmptyState
              icon={loadError === "not_found" ? KeyRound : TriangleAlert}
              tone={loadError === "not_found" ? "neutral" : "danger"}
              title={loadError === "not_found" ? "Key not found" : "Couldn't load this key"}
              description={
                loadError === "not_found"
                  ? "It may have been removed, or the link is wrong."
                  : "Check your connection or sign in again."
              }
              action={
                loadError === "failed" ? (
                  <Button variant="outline" size="sm" onClick={() => void load()}>
                    <RefreshCw /> Retry
                  </Button>
                ) : (
                  <Button variant="outline" size="sm" asChild>
                    <Link href="/admin/keys">All keys</Link>
                  </Button>
                )
              }
            />
          </div>
        </div>
      );
    }
    return <DetailSkeleton />;
  }

  const dev = d.device;
  const revoked = d.status === "revoked";
  const label = d.serial ? `${d.serial} #${d.seq}` : d.prefix;

  return (
    <div className="space-y-4">
      <PageHeader
        className="mb-0"
        back={d.serial ? { href: `/admin/orders/${d.serial}`, label: `Purchase ${d.serial}` } : { href: "/admin/keys", label: "Licence keys" }}
        title={
          <span className="flex flex-wrap items-center gap-x-2.5 gap-y-1">
            {d.serial ? (
              <span className="font-mono text-lg font-semibold sm:text-xl">
                {d.serial}
                <span className="ml-1.5 text-muted-foreground">#{d.seq}</span>
              </span>
            ) : (
              <span className="font-mono">{d.prefix}…</span>
            )}
            <span className="flex items-center gap-1">
              <EditionBadge edition={d.edition} />
              <StatusBadge status={d.status} expired={d.expired} />
              {d.attempts.length > 0 && (
                <Badge variant="destructive">
                  <ShieldAlert /> {d.attempts.length} blocked
                </Badge>
              )}
            </span>
          </span>
        }
        description={
          d.serial ? (
            <>
              Key {d.seq} of purchase{" "}
              <Link href={`/admin/orders/${d.serial}`} className="font-mono text-foreground hover:text-primary-strong hover:underline">
                {d.serial}
              </Link>
              {d.customerName && <> · {d.customerName}</>}
            </>
          ) : (
            "Issued before purchases existed"
          )
        }
        actions={
          <Button
            variant="ghost"
            size="icon"
            onClick={() => void load()}
            disabled={loading}
            aria-label="Refresh"
            title="Refresh"
            className="hidden text-muted-foreground hover:text-foreground sm:inline-flex"
          >
            <RefreshCw className={cn(loading && "animate-spin")} />
          </Button>
        }
      />

      {loadError && (
        <Alert
          tone="danger"
          title={loadError === "not_found" ? "This key no longer exists" : "Couldn't refresh this key"}
          action={
            <Button variant="outline" size="xs" onClick={() => void load()}>
              <RefreshCw /> Retry
            </Button>
          }
        >
          Showing what loaded last.
        </Alert>
      )}

      {/* ---- The key itself: show, copy, or reissue an old one ---- */}
      <section className="overflow-hidden rounded-lg border bg-card">
        <div className="flex flex-col gap-3 p-4 md:flex-row md:items-center md:justify-between">
          <div className="flex min-w-0 items-center gap-3">
            <div
              className={cn(
                "flex h-10 w-10 shrink-0 items-center justify-center rounded-lg",
                d.recoverable ? "bg-primary/10 text-primary" : "bg-muted text-muted-foreground",
              )}
            >
              <KeyRound className="h-5 w-5" aria-hidden />
            </div>
            <div className="min-w-0">
              <div className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">Licence key</div>
              <SecretKey
                prefix={d.prefix}
                recoverable={d.recoverable}
                reveal={reveal}
                className="[&>span:first-child]:text-base sm:[&>span:first-child]:text-lg"
              />
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {d.recoverable ? (
              <>
                <CopyButton value={reveal} label="Copy key" variant="outline" size="sm" toastText="Key copied" />
                <CopyButton
                  value={async () => serialAndKey(d.serial, await reveal())}
                  label={d.serial ? "Copy serial & key" : "Copy key text"}
                  variant="default"
                  size="sm"
                  toastText={d.serial ? "Serial and key copied" : "Key copied"}
                />
              </>
            ) : (
              !revoked && (
                <Button variant="soft" size="sm" onClick={openReissue}>
                  <RefreshCw /> Reissue key
                </Button>
              )
            )}
          </div>
        </div>

        {!d.recoverable && (
          <div className="border-t px-4 py-3">
            <Alert tone={dev ? "info" : "warning"} title="This key can't be shown again">
              It was issued before the panel started storing keys, so only its first characters were kept.{" "}
              {revoked
                ? "It is revoked; un-revoke it first if you want to reissue it."
                : dev
                  ? `It is in use on ${dev.deviceName || "a PC"}, so the customer already has it. To give them a new key, move it off that PC first, then reissue.`
                  : "If the customer lost it, reissue it: a new key string replaces it under the same serial and terms, and you can copy it here any time."}
            </Alert>
          </div>
        )}

        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 border-t bg-muted/30 px-4 py-2 text-xs text-muted-foreground">
          {d.serial && (
            <span className="inline-flex items-center gap-1">
              Serial <span className="font-mono text-foreground">{d.serial}</span>
              <CopyButton value={d.serial} size="icon-xs" ariaLabel="Copy serial" toastText="Serial copied" className="-my-1" />
            </span>
          )}
          <span>
            Issued {fmtDate(d.createdUtc)} by {d.createdBy || "—"}
          </span>
          {d.reissuedUtc && (
            <span>
              Reissued {fmtDateTime(d.reissuedUtc)}
              {d.reissuedBy ? ` by ${d.reissuedBy}` : ""}
            </span>
          )}
          <span title={d.lastSeenUtc ? fmtDateTime(d.lastSeenUtc) : undefined}>
            Last check-in {d.lastSeenUtc ? fmtRelative(d.lastSeenUtc) : "never"}
          </span>
        </div>
      </section>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {/* ---- The PC that holds the key ---- */}
        <SectionCard
          title="Device"
          description="The one PC this key is bound to."
          icon={MonitorSmartphone}
          iconClassName={dev ? "text-primary" : undefined}
          actions={
            dev && (
              <Button variant="outline" size="xs" disabled={busy} onClick={() => setMoveOpen(true)}>
                <ArrowRightLeft /> Move
              </Button>
            )
          }
        >
          {dev ? (
            <KvList
              labelWidth="6.5rem"
              items={[
                { label: "PC name", value: <span className="font-medium">{dev.deviceName || "Unnamed PC"}</span> },
                {
                  label: "Fingerprint",
                  value: (
                    <span className="block max-w-[14rem] truncate" title={dev.deviceFp}>
                      {dev.deviceFp}
                    </span>
                  ),
                  mono: true,
                  copy: dev.deviceFp,
                },
                { label: "Activated", value: fmtDateTime(dev.activatedUtc) },
                {
                  label: "Last seen",
                  value: <span title={fmtDateTime(dev.lastSeenUtc)}>{fmtRelative(dev.lastSeenUtc)}</span>,
                },
                { label: "IP address", value: dev.ip, mono: true },
                { label: "App version", value: dev.appVersion, mono: true },
              ]}
            />
          ) : (
            <EmptyState
              compact
              icon={MonitorOff}
              title="Not activated yet"
              description="The first PC to install with this serial and key takes it."
              className="py-4"
            />
          )}
        </SectionCard>

        {/* ---- The person who registered on that PC ---- */}
        <SectionCard
          title="Registered user"
          description="Who filled in the registration form on that PC."
          icon={dev?.user ? UserCheck : UserRound}
          iconClassName={dev?.user ? "text-success" : undefined}
        >
          {dev?.user ? (
            <KvList
              labelWidth="6.5rem"
              items={[
                { label: "Name", value: <span className="font-medium">{dev.user.name}</span> },
                {
                  label: "Email",
                  value: (
                    <a href={`mailto:${dev.user.email}`} className="break-all hover:text-primary-strong hover:underline">
                      {dev.user.email}
                    </a>
                  ),
                  copy: dev.user.email,
                },
                {
                  label: "Mobile",
                  value: <PhoneDisplay country={dev.user.phoneCountry} text={dev.user.phoneDisplay} />,
                  copy: dev.user.phoneE164 || undefined,
                },
                { label: "Registered", value: fmtDateTime(dev.user.registeredUtc) },
              ]}
            />
          ) : dev ? (
            <Alert tone="warning" title="Not registered yet">
              The PC is activated, but the user has not filled in name, email and mobile. The app stays on the
              registration screen until they do.
            </Alert>
          ) : (
            <EmptyState
              compact
              icon={UserRound}
              title="No user yet"
              description="The user registers right after the first activation."
              className="py-4"
            />
          )}
        </SectionCard>

        {/* ---- Terms of this key, and what can be changed on it ---- */}
        <SectionCard
          title="Licence"
          description="Changes here touch this key only."
          icon={CalendarClock}
          className="md:col-span-2 xl:col-span-1"
          footer={
            <div className="flex w-full flex-wrap items-center justify-between gap-2">
              <span>
                {d.serial ? (
                  <>
                    Change every key of the sale on the{" "}
                    <Link href={`/admin/orders/${d.serial}`} className="text-foreground hover:text-primary-strong hover:underline">
                      purchase page
                    </Link>
                    .
                  </>
                ) : (
                  "This key has no purchase."
                )}
              </span>
              {revoked ? (
                <Button size="xs" disabled={busy} onClick={() => void patch({ action: "unrevoke" }, "Key is active again")}>
                  <RotateCcw /> Un-revoke
                </Button>
              ) : (
                <Button
                  variant="outline"
                  size="xs"
                  disabled={busy}
                  className="border-destructive/30 text-destructive hover:bg-destructive/10 hover:text-destructive"
                  onClick={() => void patch({ action: "revoke" }, "Key revoked", { action: "unrevoke" })}
                >
                  <Ban /> Revoke key
                </Button>
              )}
            </div>
          }
        >
          <KvList
            labelWidth="6.5rem"
            items={[
              {
                label: "Valid until",
                value: d.expiryUtc ? (
                  <span>
                    {fmtDate(d.expiryUtc)}{" "}
                    <span className={cn("text-xs", d.expired ? "text-warning" : "text-muted-foreground")}>
                      ({fmtRelative(d.expiryUtc)})
                    </span>
                  </span>
                ) : (
                  "Perpetual"
                ),
              },
              { label: "Edition", value: d.edition === "full" ? "Full" : "Demo" },
              {
                label: "Customer",
                value: d.customerName ? (
                  <span>
                    {d.customerName}
                    {d.customerEmail && <span className="block text-xs text-muted-foreground">{d.customerEmail}</span>}
                  </span>
                ) : null,
              },
              { label: "Note", value: d.note, hidden: !d.note },
            ]}
          />
          <div className="mt-3 border-t pt-3">
            <Label htmlFor="extend" className="text-xs font-medium text-muted-foreground">
              Extend validity
            </Label>
            <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
              <div className="relative w-[5.5rem]">
                <Input
                  id="extend"
                  type="number"
                  min={1}
                  max={3650}
                  value={extendDays}
                  onChange={(e) => setExtendDays(Math.max(1, Math.min(3650, Math.trunc(+e.target.value) || 1)))}
                  className="h-8 pr-10 tabular-nums"
                />
                <span className="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 text-xs text-muted-foreground">
                  days
                </span>
              </div>
              <div className="flex items-center" role="group" aria-label="Quick picks">
                {EXTEND_PRESETS.map((n) => (
                  <Button
                    key={n}
                    type="button"
                    variant={extendDays === n ? "secondary" : "ghost"}
                    size="xs"
                    onClick={() => setExtendDays(n)}
                    aria-pressed={extendDays === n}
                    className={cn("px-1.5 tabular-nums", extendDays === n ? "text-foreground" : "text-muted-foreground")}
                  >
                    {n === 365 ? "1y" : `${n}d`}
                  </Button>
                ))}
              </div>
              <Button
                variant="outline"
                size="sm"
                disabled={busy}
                onClick={() => void patch({ action: "extend", days: extendDays }, `Extended by ${extendDays} days`)}
                className="ml-auto"
              >
                {busy ? <Loader2 className="animate-spin" /> : <CalendarPlus />} Extend
              </Button>
            </div>
            <p className="mt-1.5 text-[11px] text-muted-foreground">
              Counts from the expiry date, or from today if it has already passed.
            </p>
          </div>
        </SectionCard>
      </div>

      {/* ---- Attempts from other PCs ---- */}
      <SectionCard
        title={
          <span className="flex items-center gap-2">
            Blocked attempts
            {d.attempts.length > 0 && <Badge variant="destructive">{d.attempts.length}</Badge>}
          </span>
        }
        description="Every time this key was tried on a PC other than its own, or with the wrong serial."
        icon={ShieldAlert}
        iconClassName={d.attempts.length ? "text-destructive" : "text-success"}
        flush
      >
        <AttemptsTable rows={d.attempts} showKey={false} />
      </SectionCard>

      {/* ---- Previous devices ---- */}
      {d.history.length > 0 && (
        <SectionCard title="Previous devices" description="PCs that held this key before it was moved." icon={History} flush>
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
                    <div className="flex max-w-[240px] items-baseline gap-1.5" title={`Fingerprint ${a.deviceFp}`}>
                      <span className="truncate font-medium">{a.deviceName || "Unnamed PC"}</span>
                      <span className="hidden shrink-0 font-mono text-[11px] text-muted-foreground sm:inline">
                        {a.deviceFp.slice(0, 8)}
                      </span>
                    </div>
                  </TableCell>
                  <TableCell className="hidden sm:table-cell">
                    {a.user?.name || <span className="text-muted-foreground">—</span>}
                  </TableCell>
                  <TableCell className="whitespace-nowrap text-muted-foreground">
                    {fmtDate(a.activatedUtc)} → {a.releasedUtc ? fmtDate(a.releasedUtc) : "?"}
                  </TableCell>
                  <TableCell className="whitespace-nowrap">
                    <Badge variant={a.status === "released" ? "warning" : "secondary"}>
                      {a.status === "released" ? `by ${a.releasedBy || "admin"}` : "replaced"}
                    </Badge>
                  </TableCell>
                  <TableCell className="hidden text-muted-foreground md:table-cell">
                    <div className="max-w-[360px] truncate" title={a.releaseReason || undefined}>
                      {a.releaseReason || "—"}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </SectionCard>
      )}

      {/* ---- Move to another device ---- */}
      <Dialog open={moveOpen} onOpenChange={setMoveOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Move this key to another device?</DialogTitle>
            <DialogDescription>
              <strong className="text-foreground">{dev?.deviceName || "The current PC"}</strong> loses the licence at its
              next check-in (within 24 hours, or at once if it is online). The customer then enters the same serial and key
              on the new PC, which takes the key. The old PC cannot take it back.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1.5">
            <Label htmlFor="reason" className="text-xs">
              Reason (kept on record)
            </Label>
            <Input
              id="reason"
              value={moveReason}
              onChange={(e) => setMoveReason(e.target.value)}
              placeholder="Old laptop stolen, customer called 13 Sep"
              maxLength={200}
              autoFocus
            />
          </div>
          <DialogFooter>
            <Button variant="ghost" size="sm" onClick={() => setMoveOpen(false)}>
              Cancel
            </Button>
            <Button
              size="sm"
              onClick={() => {
                setMoveOpen(false);
                void patch({ action: "releaseDevice", reason: moveReason }, "Key released. The next PC to activate takes it.");
                setMoveReason("");
              }}
            >
              <ArrowRightLeft /> Release this device
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ---- Reissue an old key ---- */}
      <Dialog
        open={reissueOpen}
        onOpenChange={(open) => {
          if (!open && reissue?.step !== "busy") setReissueOpen(false);
        }}
      >
        <DialogContent className="max-w-md">
          {reissue?.step === "done" ? (
            <ReissuedKey serial={d.serial} label={label} newKey={reissue.key} onClose={() => setReissueOpen(false)} />
          ) : reissue?.step === "blocked" ? (
            <>
              <DialogHeader>
                <DialogTitle>Can't reissue this key</DialogTitle>
                <DialogDescription>{label}</DialogDescription>
              </DialogHeader>
              {reissue.reason === "in_use" ? (
                <Alert tone="warning" title={`In use on ${reissue.deviceName || "a PC"}`}>
                  Release the device first: a new key string would leave that PC without a valid key. Use{" "}
                  <strong className="text-foreground">Move</strong> on the Device card, then reissue.
                </Alert>
              ) : reissue.reason === "revoked" ? (
                <Alert tone="warning" title="The key is revoked">
                  Un-revoke it first if you want to give the customer a new key for this slot.
                </Alert>
              ) : reissue.reason === "conflict" ? (
                <Alert tone="warning" title="The key changed a moment ago">
                  Someone else changed it at the same time. The page has been refreshed; check it and try again.
                </Alert>
              ) : (
                <Alert tone="danger" title="That didn't work">
                  The server did not reissue the key. Check your connection and try again.
                </Alert>
              )}
              <DialogFooter>
                <Button variant="ghost" size="sm" onClick={() => setReissueOpen(false)}>
                  Close
                </Button>
                {reissue.reason === "in_use" && dev && (
                  <Button
                    size="sm"
                    onClick={() => {
                      setReissueOpen(false);
                      setMoveOpen(true);
                    }}
                  >
                    <ArrowRightLeft /> Move to another device
                  </Button>
                )}
              </DialogFooter>
            </>
          ) : (
            <>
              <DialogHeader>
                <DialogTitle>Reissue {label}?</DialogTitle>
                <DialogDescription>
                  A new key string replaces <span className="font-mono text-foreground">{d.prefix}…</span>. The old key stops
                  working at once. The serial, edition and expiry stay the same, and the new key can be shown and copied
                  here any time.
                </DialogDescription>
              </DialogHeader>
              <DialogFooter>
                <Button variant="ghost" size="sm" disabled={reissue?.step === "busy"} onClick={() => setReissueOpen(false)}>
                  Cancel
                </Button>
                <Button size="sm" disabled={reissue?.step === "busy"} onClick={() => void doReissue()}>
                  {reissue?.step === "busy" ? <Loader2 className="animate-spin" /> : <RefreshCw />} Reissue key
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

/** The new key after a reissue: big, selectable, with copy buttons. */
function ReissuedKey({
  serial,
  label,
  newKey,
  onClose,
}: {
  serial: string | null;
  label: string;
  newKey: string;
  onClose: () => void;
}) {
  return (
    <>
      <DialogHeader>
        <DialogTitle className="flex items-center gap-2">
          <span className="flex h-6 w-6 items-center justify-center rounded-full bg-success/10 text-success">
            <Check className="h-3.5 w-3.5" />
          </span>
          New key issued
        </DialogTitle>
        <DialogDescription>
          {label}. The old key no longer works. Send this one to the customer.
        </DialogDescription>
      </DialogHeader>
      <div className="rounded-md border bg-muted/40 p-3">
        {serial && (
          <div className="mb-2 flex items-center justify-between gap-2 text-xs text-muted-foreground">
            <span>Serial</span>
            <span className="font-mono text-foreground">{serial}</span>
          </div>
        )}
        <div className="flex items-center justify-between gap-2">
          <span className="text-xs text-muted-foreground">Key</span>
          <span className="select-all break-all text-right font-mono text-base font-semibold tracking-wide">{newKey}</span>
        </div>
      </div>
      <DialogFooter>
        <Button variant="ghost" size="sm" onClick={onClose}>
          Done
        </Button>
        <CopyButton value={newKey} label="Copy key" variant="outline" size="sm" toastText="Key copied" />
        <CopyButton
          value={serialAndKey(serial, newKey)}
          label={serial ? "Copy serial & key" : "Copy key text"}
          variant="default"
          size="sm"
          toastText={serial ? "Serial and key copied" : "Key copied"}
        />
      </DialogFooter>
    </>
  );
}

function DetailSkeleton() {
  return (
    <div className="space-y-4">
      <div className="space-y-2">
        <Skeleton className="h-3 w-28" />
        <Skeleton className="h-7 w-64" />
        <Skeleton className="h-4 w-80" />
      </div>
      <Skeleton className="h-28 w-full rounded-lg" />
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <Skeleton className="h-56 rounded-lg" />
        <Skeleton className="h-56 rounded-lg" />
        <Skeleton className="h-56 rounded-lg" />
      </div>
      <Skeleton className="h-32 w-full rounded-lg" />
    </div>
  );
}

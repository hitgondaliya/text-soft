"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import {
  ArrowLeft,
  Ban,
  CalendarClock,
  CalendarPlus,
  ChevronRight,
  CircleCheck,
  Copy,
  Download,
  Infinity as InfinityIcon,
  KeyRound,
  List,
  Loader2,
  MonitorSmartphone,
  MoreHorizontal,
  RefreshCw,
  RotateCcw,
  Send,
  ShieldAlert,
  TriangleAlert,
  UserRound,
} from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { StatStrip, type StatItem } from "@/components/stat-strip";
import { SectionCard } from "@/components/section-card";
import { KvList } from "@/components/kv-list";
import { EmptyState } from "@/components/empty-state";
import { CopyButton, copyText } from "@/components/copy-button";
import { SecretKey, revealKeyById } from "@/components/secret-key";
import { EditionBadge, StatusBadge } from "@/components/status-badge";
import { PhoneDisplay } from "@/components/flag";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { cn, fmtDate, fmtDateTime, fmtRelative } from "@/lib/utils";
import {
  downloadText,
  keysClipboardText,
  keysFileText,
  leftOutSummary,
  revealOrderKeys,
  usableKeys,
  type KeyLine,
} from "../keys-text";
import { keyCounts, type KeyRow, type Order } from "./order-types";
import { ReissueDialog } from "./reissue-dialog";
import { ResendDialog } from "./resend-dialog";

const NOTHING_TO_COPY = "No key here can be shown: they were issued before keys were stored, or are revoked.";

export default function OrderDetail({ params }: { params: { id: string } }) {
  const [o, setO] = useState<Order | null>(null);
  const [state, setState] = useState<"loading" | "ready" | "missing" | "failed">("loading");
  const [busy, setBusy] = useState(false);
  const [confirmRevoke, setConfirmRevoke] = useState(false);
  const [resendOpen, setResendOpen] = useState(false);
  const [reissue, setReissue] = useState<KeyRow | null>(null);
  const [fetching, setFetching] = useState<"copy" | "download" | null>(null);
  // Kept apart from the open flag so the text stays put while the dialog fades out.
  const [manualText, setManualText] = useState("");
  const [manualOpen, setManualOpen] = useState(false);
  const loaded = useRef(false);

  const load = useCallback(async () => {
    try {
      const r = await fetch(`/api/admin/orders/${encodeURIComponent(params.id)}`, { cache: "no-store" });
      if (r.status === 404) {
        setState("missing");
        return;
      }
      if (!r.ok) throw new Error(String(r.status));
      setO((await r.json()) as Order);
      setState("ready");
      loaded.current = true;
    } catch {
      // First load: the page shows its own error box. A refresh: keep the data, say so.
      if (loaded.current) toast.error("Couldn't refresh this purchase.");
      else setState("failed");
    }
  }, [params.id]);

  useEffect(() => {
    void load();
  }, [load]);

  async function act(body: Record<string, unknown>, success: string) {
    setBusy(true);
    try {
      const res = await fetch(`/api/admin/orders/${encodeURIComponent(params.id)}`, {
        method: "PATCH",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(body),
      });
      if (res.ok) toast.success(success);
      else toast.error(res.status === 401 ? "Your session has expired. Sign in again." : "That didn't work. Try again.");
    } catch {
      toast.error("Couldn't reach the server.");
    } finally {
      setBusy(false);
      void load();
    }
  }

  /**
   * Every key of the purchase in full (null when not stored). Revoked comes from the
   * reveal response itself, so a key revoked in another tab since this page loaded
   * is still left out; the loaded rows are only a fallback for an older server.
   */
  async function fetchLines(order: Order): Promise<KeyLine[]> {
    const r = await revealOrderKeys(order.serial);
    const loaded = new Map(order.keys.map((k) => [k.id, k.status]));
    return r.keys.map((k) => ({ seq: k.seq, key: k.key, revoked: (k.status ?? loaded.get(k.id)) === "revoked" }));
  }

  async function copyAll(order: Order) {
    setFetching("copy");
    try {
      const lines = await fetchLines(order);
      const n = usableKeys(lines).length;
      const left = leftOutSummary(lines);
      if (n === 0) {
        toast.error("No key in this purchase can be copied.", { description: left || undefined });
        return;
      }
      const text = keysClipboardText(order.serial, lines);
      if (await copyText(text)) {
        toast.success(`Copied the serial and ${n} key${n === 1 ? "" : "s"}`, { description: left || undefined });
      } else {
        setManualText(text);
        setManualOpen(true);
      }
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not fetch the keys.");
    } finally {
      setFetching(null);
    }
  }

  async function download(order: Order) {
    setFetching("download");
    try {
      const lines = await fetchLines(order);
      const text = keysFileText(
        {
          serial: order.serial,
          customerName: order.customerName,
          company: order.company,
          edition: order.edition,
          expiryUtc: order.expiryUtc,
        },
        lines,
      );
      downloadText(`${order.serial}-keys.txt`, text);
      toast.success(`Downloaded ${order.serial}-keys.txt`, { description: leftOutSummary(lines) || undefined });
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not fetch the keys.");
    } finally {
      setFetching(null);
    }
  }

  if (state === "missing") {
    return (
      <div className="space-y-4">
        <PageHeader back={{ href: "/admin/orders", label: "Purchases" }} title="Purchase not found" className="mb-0" />
        <section className="rounded-lg border bg-card">
          <EmptyState
            icon={KeyRound}
            title={`No purchase called “${decodeURIComponent(params.id)}”`}
            description="It may have been typed wrong. Search the purchases list instead."
            action={
              <Button variant="outline" size="sm" asChild>
                <Link href="/admin/orders">
                  <ArrowLeft /> All purchases
                </Link>
              </Button>
            }
          />
        </section>
      </div>
    );
  }

  if (!o) {
    return state === "failed" ? (
      <div className="space-y-4">
        <PageHeader back={{ href: "/admin/orders", label: "Purchases" }} title="Purchase" className="mb-0" />
        <Alert
          tone="danger"
          title="Couldn't load this purchase"
          action={
            <Button variant="outline" size="xs" onClick={() => void load()}>
              <RefreshCw /> Retry
            </Button>
          }
        >
          Check your connection or sign in again.
        </Alert>
      </div>
    ) : (
      <DetailSkeleton />
    );
  }

  const c = keyCounts(o.keys);
  const allRevoked = c.total > 0 && c.revoked === c.total;
  const nothingToCopy = c.usable === 0;

  const stats: StatItem[] = [
    {
      label: "Keys issued",
      value: c.total,
      icon: KeyRound,
      tone: "neutral",
      hint: c.missing > 0 ? `${c.missing} not stored` : `${c.usable} copyable`,
    },
    {
      label: "On a PC",
      value: c.bound,
      icon: MonitorSmartphone,
      tone: "primary",
      hint: `${c.registered} registered`,
      title: "Keys activated on a PC right now",
    },
    { label: "Revoked", value: c.revoked, icon: Ban, tone: c.revoked ? "danger" : "neutral" },
    {
      label: "Blocked attempts",
      value: o.attemptsTotal,
      icon: ShieldAlert,
      tone: o.attemptsTotal > 0 ? "danger" : "success",
      href: o.attemptsTotal > 0 ? `/admin/security?q=${encodeURIComponent(o.serial)}` : undefined,
      hint: o.attemptsTotal > 0 ? "Review on Security" : "None",
      title: "Refused activations with this purchase's keys",
    },
  ];

  return (
    <div className="space-y-4">
      <PageHeader
        className="mb-0"
        back={{ href: "/admin/orders", label: "Purchases" }}
        title={
          <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <span className="font-mono">{o.serial}</span>
            <CopyButton value={o.serial} ariaLabel="Copy serial" toastText="Serial copied" size="icon-xs" className="text-muted-foreground" />
            <EditionBadge edition={o.edition} />
            <StatusBadge status={allRevoked ? "revoked" : "active"} expired={o.expired} />
          </span>
        }
        description={
          <>
            <span className="font-medium text-foreground">{o.customerName}</span>
            {o.company && <> · {o.company}</>} · sold {fmtDate(o.createdUtc)} by {o.createdBy}
          </>
        }
        actions={
          <>
            <Button variant="outline" size="sm" onClick={() => setResendOpen(true)}>
              <Send /> {o.emailedUtc ? "Resend email" : "Email keys"}
            </Button>
            <span title={nothingToCopy ? NOTHING_TO_COPY : undefined} className="inline-flex">
              <Button
                variant="outline"
                size="sm"
                onClick={() => void download(o)}
                disabled={nothingToCopy || fetching !== null}
                aria-label="Download keys as .txt"
                title={nothingToCopy ? undefined : "Download keys as .txt"}
              >
                {fetching === "download" ? <Loader2 className="animate-spin" /> : <Download />}
                <span className="hidden sm:inline">Download .txt</span>
              </Button>
            </span>
            <span title={nothingToCopy ? NOTHING_TO_COPY : undefined} className="inline-flex">
              <Button size="sm" onClick={() => void copyAll(o)} disabled={nothingToCopy || fetching !== null}>
                {fetching === "copy" ? <Loader2 className="animate-spin" /> : <Copy />}
                Copy all keys
              </Button>
            </span>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" size="icon" aria-label="More actions" title="More actions" disabled={busy}>
                  {busy ? <Loader2 className="animate-spin" /> : <MoreHorizontal />}
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-52">
                <DropdownMenuItem asChild>
                  <Link href={`/admin/keys?q=${encodeURIComponent(o.serial)}`}>
                    <List /> Open in licence keys
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuItem asChild>
                  <Link href={`/admin/security?q=${encodeURIComponent(o.serial)}`}>
                    <ShieldAlert /> Blocked attempts
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                {c.revoked > 0 && (
                  <DropdownMenuItem onSelect={() => void act({ action: "unrevokeAll" }, "Every key is active again")}>
                    <RotateCcw /> Un-revoke all keys
                  </DropdownMenuItem>
                )}
                {!allRevoked && (
                  <DropdownMenuItem
                    onSelect={() => setConfirmRevoke(true)}
                    className="text-destructive focus:bg-destructive/10 focus:text-destructive"
                  >
                    <Ban /> Revoke all keys
                  </DropdownMenuItem>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          </>
        }
      />

      <StatStrip items={stats} size="sm" />

      <div className="grid gap-4 lg:grid-cols-2">
        <SectionCard title="Customer" icon={UserRound}>
          <KvList
            labelWidth="6.5rem"
            items={[
              { label: "Name", value: <span className="font-medium">{o.customerName}</span> },
              { label: "Company", value: o.company },
              {
                label: "Email",
                value: o.customerEmail ? (
                  <a href={`mailto:${o.customerEmail}`} className="break-all hover:underline">
                    {o.customerEmail}
                  </a>
                ) : null,
                copy: o.customerEmail || undefined,
              },
              {
                label: "Mobile",
                value: o.customerPhoneDisplay ? (
                  <PhoneDisplay country={o.customerPhoneCountry} text={o.customerPhoneDisplay} />
                ) : null,
                copy: o.customerPhoneDisplay || undefined,
              },
              { label: "Note", value: o.note ? <span className="whitespace-pre-line">{o.note}</span> : null },
            ]}
          />
        </SectionCard>

        <SectionCard
          title="Validity & delivery"
          icon={CalendarClock}
          footer={
            <div className="flex w-full flex-wrap items-center gap-1.5">
              <span className="mr-1">All {c.total} keys:</span>
              <Button
                variant="outline"
                size="xs"
                disabled={busy}
                onClick={() => void act({ action: "extendAll", days: 30 }, "Extended every key by 30 days")}
              >
                <CalendarPlus /> +30 days
              </Button>
              <Button
                variant="outline"
                size="xs"
                disabled={busy}
                onClick={() => void act({ action: "extendAll", days: 365 }, "Extended every key by one year")}
              >
                <CalendarPlus /> +1 year
              </Button>
              <Button
                variant="outline"
                size="xs"
                disabled={busy || !o.expiryUtc}
                onClick={() => void act({ action: "setExpiryAll", expiryUtc: null }, "This purchase never expires now")}
              >
                <InfinityIcon /> Make perpetual
              </Button>
              {busy && <Loader2 className="h-3.5 w-3.5 animate-spin" aria-label="Working" />}
            </div>
          }
        >
          <KvList
            labelWidth="6.5rem"
            items={[
              {
                label: "Valid until",
                value: o.expiryUtc ? (
                  <span className="inline-flex flex-wrap items-center gap-1.5">
                    <span className="font-medium">{fmtDate(o.expiryUtc)}</span>
                    <span className="text-xs text-muted-foreground">{fmtRelative(o.expiryUtc)}</span>
                    {o.expired && <Badge variant="warning">Expired</Badge>}
                  </span>
                ) : (
                  <span className="font-medium">Perpetual — never expires</span>
                ),
              },
              { label: "Edition", value: <EditionBadge edition={o.edition} /> },
              { label: "Sold", value: fmtDateTime(o.createdUtc) },
              { label: "Sold by", value: o.createdBy },
              {
                label: "Keys email",
                value: (
                  <span className="inline-flex flex-wrap items-center gap-x-2 gap-y-0.5">
                    {o.emailedUtc ? (
                      <span className="inline-flex items-center gap-1 text-success">
                        <CircleCheck className="h-3.5 w-3.5" aria-hidden /> Sent {fmtDateTime(o.emailedUtc)}
                      </span>
                    ) : o.emailError ? (
                      <span className="inline-flex items-start gap-1 text-warning">
                        <TriangleAlert className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden /> Failed: {o.emailError}
                      </span>
                    ) : (
                      <span className="text-muted-foreground">
                        Not sent{o.customerEmail ? "" : " — no customer email"}
                      </span>
                    )}
                    <button
                      type="button"
                      onClick={() => setResendOpen(true)}
                      className="rounded text-xs font-medium text-primary-strong hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                    >
                      {o.emailedUtc ? "Resend" : "Send now"}
                    </button>
                    {o.lastSentTo && o.lastSentUtc && o.lastSentTo.toLowerCase() !== o.customerEmail.toLowerCase() && (
                      <span className="basis-full text-xs text-muted-foreground">
                        Last copy went to <span className="font-medium text-foreground">{o.lastSentTo}</span>,{" "}
                        {fmtDateTime(o.lastSentUtc)}
                      </span>
                    )}
                  </span>
                ),
              },
            ]}
          />
        </SectionCard>
      </div>

      <SectionCard
        title="Keys"
        icon={KeyRound}
        description={`${c.total} under ${o.serial} · one key works on one PC · open a key to move it to another PC`}
        flush
      >
        {c.missing > 0 && (
          <div className="border-b px-4 py-3">
            <Alert tone="info" title={`${c.missing} of ${c.total - c.revoked} keys can't be shown again`}>
              They were issued before the panel started storing keys. PCs already using them keep working.{" "}
              {c.reissuable > 0 ? (
                <>
                  If a customer lost one that was never activated, use <strong className="text-foreground">Reissue</strong> on
                  its row: that slot gets a new key and the old one stops working.
                </>
              ) : (
                <>Each of them is on a PC now; to replace one, open the key and move it off its PC first.</>
              )}
            </Alert>
          </div>
        )}
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-10">#</TableHead>
              <TableHead>Key</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="hidden sm:table-cell">Registered user</TableHead>
              <TableHead className="hidden lg:table-cell">PC</TableHead>
              <TableHead className="hidden 2xl:table-cell">Mobile</TableHead>
              <TableHead className="hidden text-right md:table-cell">Blocked</TableHead>
              <TableHead className="hidden text-right md:table-cell">Last seen</TableHead>
              <TableHead className="w-10">
                <span className="sr-only">Open</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {o.keys.map((k) => (
              <TableRow key={k.id} className={cn(k.status === "revoked" && "text-muted-foreground")}>
                <TableCell className="tabular-nums text-muted-foreground">{k.seq}</TableCell>
                <TableCell className="whitespace-nowrap">
                  <SecretKey
                    prefix={k.prefix}
                    recoverable={k.recoverable}
                    reveal={() => revealKeyById(k.id)}
                    onReissue={!k.recoverable && k.status !== "revoked" && !k.bound ? () => setReissue(k) : undefined}
                  />
                </TableCell>
                <TableCell>
                  <StatusBadge status={k.status} expired={k.expired} />
                </TableCell>
                <TableCell className="hidden max-w-[240px] sm:table-cell">
                  {k.registered ? (
                    <div className="min-w-0">
                      <div className="truncate font-medium text-foreground">{k.userName}</div>
                      <div className="truncate text-xs text-muted-foreground">{k.userEmail}</div>
                    </div>
                  ) : k.bound ? (
                    <Badge variant="warning">Not registered yet</Badge>
                  ) : (
                    <span className="text-muted-foreground">Not activated</span>
                  )}
                </TableCell>
                <TableCell className="hidden max-w-[180px] truncate lg:table-cell">
                  {k.deviceName ? (
                    <span title={k.deviceName}>{k.deviceName}</span>
                  ) : (
                    <span className="text-muted-foreground">{k.bound ? "Unnamed PC" : "—"}</span>
                  )}
                </TableCell>
                <TableCell className="hidden 2xl:table-cell">
                  <PhoneDisplay country={k.userPhoneCountry} text={k.userPhone} />
                </TableCell>
                <TableCell className="hidden text-right md:table-cell">
                  {k.attempts > 0 ? (
                    <Link
                      href={`/admin/keys/${k.id}`}
                      className="inline-flex"
                      title={`${k.attempts} refused activation${k.attempts === 1 ? "" : "s"} with this key`}
                    >
                      <Badge variant="destructive">
                        <ShieldAlert /> {k.attempts}
                      </Badge>
                    </Link>
                  ) : (
                    <span className="tabular-nums text-muted-foreground">0</span>
                  )}
                </TableCell>
                <TableCell
                  className="hidden whitespace-nowrap text-right text-muted-foreground md:table-cell"
                  title={k.lastSeenUtc ? fmtDateTime(k.lastSeenUtc) : undefined}
                >
                  {k.lastSeenUtc ? fmtRelative(k.lastSeenUtc) : "never"}
                </TableCell>
                <TableCell className="w-10 text-right">
                  <Button
                    variant="ghost"
                    size="icon-xs"
                    asChild
                    className="text-muted-foreground hover:text-foreground"
                  >
                    <Link href={`/admin/keys/${k.id}`} aria-label={`Open key #${k.seq}`} title={`Open key #${k.seq}`}>
                      <ChevronRight />
                    </Link>
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </SectionCard>

      <ResendDialog order={o} open={resendOpen} onOpenChange={setResendOpen} onSent={() => void load()} />

      <ReissueDialog serial={o.serial} target={reissue} onClose={() => setReissue(null)} onReissued={() => void load()} />

      <Dialog open={confirmRevoke} onOpenChange={setConfirmRevoke}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Revoke all {c.total} keys?</DialogTitle>
            <DialogDescription>
              Every PC using a key from <span className="font-mono text-foreground">{o.serial}</span> stops working at its next
              check-in. You can un-revoke later.
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
                void act({ action: "revokeAll" }, `Revoked all keys of ${o.serial}`);
              }}
            >
              <Ban /> Revoke all
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={manualOpen} onOpenChange={setManualOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Copy the keys by hand</DialogTitle>
            <DialogDescription>The browser blocked the clipboard. Select the text below and copy it.</DialogDescription>
          </DialogHeader>
          <Textarea
            readOnly
            value={manualText}
            rows={Math.min(14, manualText.split("\n").length + 1)}
            className="font-mono text-xs"
            onFocus={(e) => e.currentTarget.select()}
            autoFocus
          />
          <DialogFooter>
            <Button onClick={() => setManualOpen(false)}>Done</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function DetailSkeleton() {
  return (
    <div className="space-y-4" aria-busy="true" aria-label="Loading purchase">
      <div className="space-y-2">
        <Skeleton className="h-3 w-20" />
        <div className="flex items-center justify-between gap-3">
          <Skeleton className="h-7 w-64" />
          <Skeleton className="hidden h-8 w-72 sm:block" />
        </div>
        <Skeleton className="h-4 w-80 max-w-full" />
      </div>
      <Skeleton className="h-[74px] w-full rounded-lg" />
      <div className="grid gap-4 lg:grid-cols-2">
        <Skeleton className="h-56 rounded-lg" />
        <Skeleton className="h-56 rounded-lg" />
      </div>
      <Skeleton className="h-72 rounded-lg" />
    </div>
  );
}

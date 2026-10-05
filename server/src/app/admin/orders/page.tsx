"use client";

import { Suspense, useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { ChevronRight, Plus, RefreshCw, ShoppingBag } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { DataToolbar } from "@/components/data-toolbar";
import { EmptyState } from "@/components/empty-state";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { EditionBadge } from "@/components/status-badge";
import { cn, fmtDate, fmtDateTime, fmtRelative } from "@/lib/utils";
import { NewPurchaseDialog } from "./new-purchase-dialog";

interface OrderRow {
  id: string;
  serial: string;
  customerName: string;
  customerEmail: string;
  customerPhoneDisplay: string;
  company: string;
  edition: string;
  quantity: number;
  issued: number;
  revoked: number;
  activated: number;
  expiryUtc: string | null;
  expired: boolean;
  emailedUtc: string | null;
  createdUtc: string;
}

type Filter = "all" | "active" | "expiring" | "expired" | "revoked";

const SOON_MS = 30 * 24 * 60 * 60 * 1000;

function stateOf(o: OrderRow): "active" | "expired" | "revoked" {
  if (o.issued > 0 && o.revoked >= o.issued) return "revoked";
  if (o.expired) return "expired";
  return "active";
}

function expiringSoon(o: OrderRow): boolean {
  if (!o.expiryUtc || stateOf(o) !== "active") return false;
  return new Date(o.expiryUtc).getTime() - Date.now() <= SOON_MS;
}

function matches(o: OrderRow, f: Filter): boolean {
  if (f === "all") return true;
  if (f === "expiring") return expiringSoon(o);
  return stateOf(o) === f;
}

export default function OrdersPage() {
  return (
    <Suspense fallback={null}>
      <Orders />
    </Suspense>
  );
}

function Orders() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const [rows, setRows] = useState<OrderRow[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [loading, setLoading] = useState(true);
  const [q, setQ] = useState(params.get("q") ?? "");
  const [filter, setFilter] = useState<Filter>("all");
  const [open, setOpen] = useState(params.get("new") === "1");
  const seq = useRef(0);

  const load = useCallback(async (query: string) => {
    const mine = ++seq.current;
    setLoading(true);
    setFailed(false);
    try {
      const p = new URLSearchParams();
      if (query.trim()) p.set("q", query.trim());
      const r = await fetch(`/api/admin/orders?${p}`, { cache: "no-store" });
      if (!r.ok) throw new Error(String(r.status));
      const d = (await r.json()) as { orders?: OrderRow[] };
      if (mine === seq.current) setRows(d.orders ?? []);
    } catch {
      if (mine === seq.current) setFailed(true);
    } finally {
      if (mine === seq.current) setLoading(false);
    }
  }, []);

  // Debounced search: one request per pause in typing, not per keystroke.
  useEffect(() => {
    const t = setTimeout(() => void load(q), q ? 250 : 0);
    return () => clearTimeout(t);
  }, [q, load]);

  function setDialog(v: boolean) {
    setOpen(v);
    // Drop ?new=1 so a refresh doesn't pop the dialog open again.
    if (!v && params.get("new")) router.replace(pathname);
  }

  const counts = useMemo(() => {
    const c: Record<Filter, number> = { all: 0, active: 0, expiring: 0, expired: 0, revoked: 0 };
    for (const o of rows ?? []) {
      c.all++;
      c[stateOf(o)]++;
      if (expiringSoon(o)) c.expiring++;
    }
    return c;
  }, [rows]);

  const shown = useMemo(() => (rows ?? []).filter((o) => matches(o, filter)), [rows, filter]);

  const open_ = (serial: string) => router.push(`/admin/orders/${encodeURIComponent(serial)}`);

  return (
    <div className="space-y-3">
      <PageHeader
        title="Purchases"
        description="One serial per sale. Every key of the sale is issued under it."
        className="mb-1"
        actions={
          <>
            <Button
              variant="ghost"
              size="icon"
              onClick={() => void load(q)}
              disabled={loading}
              aria-label="Refresh"
              title="Refresh"
              className="text-muted-foreground hover:text-foreground"
            >
              <RefreshCw className={cn(loading && rows !== null && "animate-spin")} />
            </Button>
            <Button size="sm" onClick={() => setDialog(true)}>
              <Plus /> New purchase
            </Button>
          </>
        }
      />

      <NewPurchaseDialog open={open} onOpenChange={setDialog} onCreated={() => void load(q)} />

      <DataToolbar
        search={q}
        onSearchChange={setQ}
        placeholder="Search serial, customer, email, company, mobile…"
        filters={
          <Tabs value={filter} onValueChange={(v) => setFilter(v as Filter)} className="min-w-0 max-w-full">
            <TabsList className="max-w-full overflow-x-auto">
              <FilterTab value="all" label="All" n={rows ? counts.all : null} />
              <FilterTab value="active" label="Active" n={rows ? counts.active : null} />
              <FilterTab
                value="expiring"
                label="Expiring 30d"
                n={rows ? counts.expiring : null}
                title="Active purchases that expire within 30 days"
              />
              <FilterTab value="expired" label="Expired" n={rows ? counts.expired : null} />
              <FilterTab value="revoked" label="Revoked" n={rows ? counts.revoked : null} />
            </TabsList>
          </Tabs>
        }
      />

      <section className="overflow-hidden rounded-lg border bg-card text-card-foreground">
        {rows === null ? (
          failed ? (
            <EmptyState
              tone="danger"
              title="Couldn't load purchases"
              description="Check your connection or sign in again."
              action={
                <Button variant="outline" size="sm" onClick={() => void load(q)}>
                  <RefreshCw /> Retry
                </Button>
              }
            />
          ) : (
            <ListSkeleton />
          )
        ) : shown.length === 0 ? (
          q || filter !== "all" ? (
            <EmptyState
              icon={ShoppingBag}
              title="Nothing matches"
              description={
                q ? "Try a serial number, customer name, email, company or mobile number." : "No purchase is in this state."
              }
              action={
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setQ("");
                    setFilter("all");
                  }}
                >
                  Clear filters
                </Button>
              }
            />
          ) : (
            <EmptyState
              icon={ShoppingBag}
              tone="primary"
              title="No purchases yet"
              description="Record your first sale and its keys are issued under one serial number."
              action={
                <Button size="sm" onClick={() => setDialog(true)}>
                  <Plus /> New purchase
                </Button>
              }
            />
          )
        ) : (
          <>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Serial</TableHead>
                  <TableHead>Customer</TableHead>
                  <TableHead className="hidden sm:table-cell">Edition</TableHead>
                  <TableHead>Keys used</TableHead>
                  <TableHead className="hidden md:table-cell">Expiry</TableHead>
                  <TableHead className="hidden lg:table-cell">Sold</TableHead>
                  <TableHead className="w-8">
                    <span className="sr-only">Open</span>
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {shown.map((o) => {
                  const state = stateOf(o);
                  const live = Math.max(0, o.issued - o.revoked);
                  const pct = o.issued ? Math.min(100, Math.round((o.activated / o.issued) * 100)) : 0;
                  return (
                    <TableRow
                      key={o.id}
                      className="group cursor-pointer"
                      onClick={(e) => {
                        if ((e.target as HTMLElement).closest("a,button")) return;
                        open_(o.serial);
                      }}
                    >
                      <TableCell className="whitespace-nowrap">
                        <Link
                          href={`/admin/orders/${encodeURIComponent(o.serial)}`}
                          className="rounded font-mono text-[13px] font-medium text-primary-strong hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                        >
                          {o.serial}
                        </Link>
                      </TableCell>
                      <TableCell className="max-w-[260px] xl:max-w-[360px]">
                        <div className="truncate font-medium">{o.customerName}</div>
                        {(o.company || o.customerEmail || o.customerPhoneDisplay) && (
                          <div className="truncate text-xs text-muted-foreground">
                            {[o.company, o.customerEmail || o.customerPhoneDisplay].filter(Boolean).join(" · ")}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="hidden sm:table-cell">
                        <EditionBadge edition={o.edition} />
                      </TableCell>
                      <TableCell className="whitespace-nowrap" title={`${o.activated} of ${o.issued} keys activated on a PC`}>
                        <div className="flex items-center gap-2">
                          <span className="tabular-nums">
                            <span className="font-medium">{o.activated}</span>
                            <span className="text-muted-foreground">/{o.issued}</span>
                          </span>
                          <span className="hidden h-1 w-12 overflow-hidden rounded-full bg-muted md:block" aria-hidden>
                            <span className="block h-full rounded-full bg-primary/70" style={{ width: `${pct}%` }} />
                          </span>
                          {o.revoked > 0 && state !== "revoked" && (
                            <span className="text-xs text-destructive" title={`${live} still usable`}>
                              {o.revoked} revoked
                            </span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="hidden whitespace-nowrap md:table-cell">
                        <div className="flex items-center gap-1.5">
                          {o.expiryUtc ? (
                            <span
                              className={cn(state === "active" ? "text-foreground" : "text-muted-foreground")}
                              title={fmtRelative(o.expiryUtc)}
                            >
                              {fmtDate(o.expiryUtc)}
                            </span>
                          ) : (
                            <span className="text-muted-foreground">Perpetual</span>
                          )}
                          {state === "revoked" ? (
                            <Badge variant="destructive">Revoked</Badge>
                          ) : state === "expired" ? (
                            <Badge variant="warning">Expired</Badge>
                          ) : expiringSoon(o) ? (
                            <Badge variant="warning">{fmtRelative(o.expiryUtc)}</Badge>
                          ) : null}
                        </div>
                      </TableCell>
                      <TableCell
                        className="hidden whitespace-nowrap text-muted-foreground lg:table-cell"
                        title={fmtDateTime(o.createdUtc)}
                      >
                        {fmtDate(o.createdUtc)}
                      </TableCell>
                      <TableCell className="w-8 text-right">
                        <ChevronRight
                          className="ml-auto h-4 w-4 text-muted-foreground/50 transition-colors group-hover:text-foreground"
                          aria-hidden
                        />
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
            <div className="flex items-center justify-between gap-2 border-t px-4 py-2 text-xs text-muted-foreground">
              <span>
                {shown.length === rows.length
                  ? `${rows.length} purchase${rows.length === 1 ? "" : "s"}`
                  : `${shown.length} of ${rows.length} purchases`}
              </span>
              {rows.length >= 500 && <span>Showing the newest 500 — search to narrow it down.</span>}
            </div>
          </>
        )}
      </section>
    </div>
  );
}

function FilterTab({ value, label, n, title }: { value: Filter; label: string; n: number | null; title?: string }) {
  return (
    <TabsTrigger value={value} title={title}>
      {label}
      {n !== null && <span className="hidden tabular-nums text-muted-foreground sm:inline">{n}</span>}
    </TabsTrigger>
  );
}

function ListSkeleton() {
  return (
    <div>
      <div className="h-9 border-b bg-muted/40" />
      {Array.from({ length: 6 }, (_, i) => (
        <div key={i} className="flex items-center gap-4 border-b px-4 py-2.5 last:border-0">
          <Skeleton className="h-4 w-28" />
          <div className="flex-1 space-y-1.5">
            <Skeleton className="h-3.5 w-40" />
            <Skeleton className="h-3 w-56" />
          </div>
          <Skeleton className="hidden h-4 w-12 sm:block" />
          <Skeleton className="h-4 w-14" />
          <Skeleton className="hidden h-4 w-20 md:block" />
          <Skeleton className="hidden h-4 w-20 lg:block" />
        </div>
      ))}
    </div>
  );
}

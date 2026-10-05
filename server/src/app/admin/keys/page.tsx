"use client";

import { Suspense, useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { ChevronRight, KeyRound, Plus, RefreshCw, SearchX, ShieldAlert } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { DataToolbar } from "@/components/data-toolbar";
import { EmptyState } from "@/components/empty-state";
import { SecretKey, revealKeyById } from "@/components/secret-key";
import { EditionBadge, StatusBadge } from "@/components/status-badge";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { cn, fmtDate, fmtDateTime, fmtRelative } from "@/lib/utils";

interface KeyRow {
  id: string;
  prefix: string;
  recoverable: boolean;
  serial: string;
  seq: number;
  edition: string;
  status: string;
  expired: boolean;
  expiryUtc: string | null;
  bound: boolean;
  deviceName: string;
  registered: boolean;
  userName: string;
  userPhone: string;
  attempts: number;
  customerName: string;
  customerEmail: string;
  createdUtc: string;
  lastSeenUtc: string | null;
}

// ?status= values. "expiring" is filtered here (the API has no such filter);
// every other value is passed to GET /api/admin/keys as before.
const FILTERS = [
  { value: "all", label: "All", hint: "Every key ever issued" },
  { value: "active", label: "Active", hint: "Not revoked and not expired" },
  { value: "expiring", label: "Expiring", hint: "Active keys that expire within 7 days" },
  { value: "bound", label: "On a PC", hint: "Activated on a PC right now" },
  { value: "unused", label: "Unused", hint: "Active keys no PC has activated yet" },
  { value: "attempts", label: "Blocked", hint: "Keys someone tried on another PC or with a wrong serial" },
  { value: "expired", label: "Expired", hint: "Past their expiry date" },
  { value: "revoked", label: "Revoked", hint: "Revoked by an admin" },
] as const;
type Filter = (typeof FILTERS)[number]["value"];

const WEEK_MS = 7 * 86_400_000;
const LIST_CAP = 500; // the API returns the newest 500

function asFilter(v: string | null): Filter {
  return (FILTERS.find((f) => f.value === v)?.value ?? "all") as Filter;
}

function expiresSoon(k: KeyRow, now: number): boolean {
  if (!k.expiryUtc || k.expired) return false;
  const t = new Date(k.expiryUtc).getTime();
  return t > now && t - now <= WEEK_MS;
}

export default function KeysPage() {
  return (
    <Suspense fallback={null}>
      <Keys />
    </Suspense>
  );
}

function Keys() {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const status = asFilter(params.get("status"));
  const urlQ = params.get("q") ?? "";

  const [rows, setRows] = useState<KeyRow[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [tick, setTick] = useState(0);

  // The URL is the source of truth for the filter and the search, so a link such as
  // /admin/keys?status=revoked (dashboard, sidebar) always shows what it says.
  const setParams = useCallback(
    (patch: Record<string, string | null>) => {
      const p = new URLSearchParams(params.toString());
      for (const [k, v] of Object.entries(patch)) {
        if (v === null || v === "" || (k === "status" && v === "all")) p.delete(k);
        else p.set(k, v);
      }
      const s = p.toString();
      router.replace(s ? `${pathname}?${s}` : pathname, { scroll: false });
    },
    [params, pathname, router],
  );

  // Search box: typed text is local, written to ?q= after a short pause.
  const [q, setQ] = useState(urlQ);
  const written = useRef(urlQ);
  useEffect(() => {
    if (urlQ !== written.current) {
      written.current = urlQ;
      setQ(urlQ);
    }
  }, [urlQ]);
  useEffect(() => {
    if (q === written.current) return;
    const t = setTimeout(() => {
      written.current = q;
      setParams({ q: q.trim() ? q : null });
    }, 300);
    return () => clearTimeout(t);
  }, [q, setParams]);

  useEffect(() => {
    const ac = new AbortController();
    const p = new URLSearchParams();
    const serverStatus = status === "expiring" ? "active" : status;
    if (serverStatus !== "all") p.set("status", serverStatus);
    if (urlQ.trim()) p.set("q", urlQ.trim());
    setLoading(true);
    fetch(`/api/admin/keys?${p}`, { cache: "no-store", signal: ac.signal })
      .then(async (r) => {
        if (!r.ok) throw new Error(String(r.status));
        return ((await r.json()) as { keys?: KeyRow[] }).keys ?? [];
      })
      .then((keys) => {
        const now = Date.now();
        // "Active" means usable today, the same as the dashboard's Active keys figure.
        const list =
          status === "active"
            ? keys.filter((k) => !k.expired)
            : status === "expiring"
              ? keys.filter((k) => expiresSoon(k, now))
              : keys;
        setRows(list);
        setFailed(false);
      })
      .catch(() => {
        if (ac.signal.aborted) return;
        setFailed(true);
        setRows((prev) => prev ?? []);
      })
      .finally(() => {
        if (!ac.signal.aborted) setLoading(false);
      });
    return () => ac.abort();
  }, [status, urlQ, tick]);

  const reload = () => setTick((n) => n + 1);
  const filtered = status !== "all" || urlQ.trim() !== "";

  function openRow(e: React.MouseEvent, id: string) {
    const target = e.target as HTMLElement;
    if (target.closest("a,button,input,[role=button],[tabindex]")) return;
    if (window.getSelection()?.toString()) return; // selecting a revealed key, not opening it
    const href = `/admin/keys/${id}`;
    if (e.metaKey || e.ctrlKey) window.open(href, "_blank", "noopener");
    else router.push(href);
  }

  const now = Date.now();

  return (
    <div>
      <PageHeader
        title="Licence keys"
        description="Every key issued, the PC it is on and who uses it. Show or copy any key stored since keys were saved."
        actions={
          <Button size="sm" asChild>
            <Link href="/admin/orders?new=1">
              <Plus /> New purchase
            </Link>
          </Button>
        }
      />

      {failed && (
        <Alert
          tone="danger"
          title="Couldn't load the keys"
          className="mb-3"
          action={
            <Button variant="outline" size="xs" onClick={reload}>
              <RefreshCw /> Retry
            </Button>
          }
        >
          {rows && rows.length ? "Showing the last list that loaded." : "Check your connection or sign in again."}
        </Alert>
      )}

      <section className="min-w-0 rounded-lg border bg-card text-card-foreground">
        <div className="border-b px-3 py-2.5 sm:px-4">
          <DataToolbar
            search={q}
            onSearchChange={setQ}
            placeholder="Search key, serial, customer, user, email, mobile…"
            filters={
              <div className="min-w-0 max-w-full overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
                <Tabs value={status} onValueChange={(v) => setParams({ status: v })}>
                  <TabsList aria-label="Filter keys">
                    {FILTERS.map((f) => (
                      <TabsTrigger key={f.value} value={f.value} title={f.hint}>
                        {f.label}
                      </TabsTrigger>
                    ))}
                  </TabsList>
                </Tabs>
              </div>
            }
            actions={
              <>
                <span className="whitespace-nowrap text-xs tabular-nums text-muted-foreground" aria-live="polite">
                  {rows === null ? "" : `${rows.length.toLocaleString("en-IN")} ${rows.length === 1 ? "key" : "keys"}`}
                </span>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  onClick={reload}
                  disabled={loading}
                  aria-label="Refresh"
                  title="Refresh"
                  className="text-muted-foreground hover:text-foreground"
                >
                  <RefreshCw className={cn(loading && "animate-spin")} />
                </Button>
              </>
            }
          />
        </div>

        {rows === null ? (
          <TableSkeleton />
        ) : rows.length === 0 ? (
          filtered ? (
            <EmptyState
              icon={SearchX}
              title="No keys match"
              description="Try another search or filter."
              action={
                <Button variant="outline" size="sm" onClick={() => setParams({ status: null, q: null })}>
                  Clear filters
                </Button>
              }
            />
          ) : (
            <EmptyState
              icon={KeyRound}
              tone="primary"
              title="No keys yet"
              description="Keys are issued from a purchase. Create one and its keys appear here with their serial."
              action={
                <Button size="sm" asChild>
                  <Link href="/admin/orders?new=1">
                    <Plus /> New purchase
                  </Link>
                </Button>
              }
            />
          )
        ) : (
          <Table className={cn(loading && "opacity-60 transition-opacity")}>
            <TableHeader>
              <TableRow>
                <TableHead>Key</TableHead>
                <TableHead>Purchase</TableHead>
                {/* Columns come in by width so nothing scrolls sideways with the sidebar open:
                    md/lg device, xl expiry, 1360px+ (a 1366 laptop) customer, 2xl edition and last seen. */}
                <TableHead className="hidden min-[1360px]:table-cell">Customer</TableHead>
                <TableHead className="hidden 2xl:table-cell">Edition</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="hidden md:table-cell">Device · user</TableHead>
                <TableHead className="hidden xl:table-cell">Expiry</TableHead>
                <TableHead className="hidden text-right 2xl:table-cell">Last seen</TableHead>
                <TableHead className="w-10 px-2 last:pr-3">
                  <span className="sr-only">Open</span>
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((k) => {
                const soon = expiresSoon(k, now);
                return (
                  <TableRow key={k.id} className="cursor-pointer" onClick={(e) => openRow(e, k.id)}>
                    <TableCell className="whitespace-nowrap">
                      <SecretKey prefix={k.prefix} recoverable={k.recoverable} reveal={() => revealKeyById(k.id)} />
                    </TableCell>
                    <TableCell className="whitespace-nowrap font-mono text-xs">
                      {k.serial ? (
                        <Link
                          href={`/admin/orders/${k.serial}`}
                          className="hover:text-primary-strong hover:underline"
                          title={k.customerName ? `Purchase by ${k.customerName}` : "Open purchase"}
                        >
                          {k.serial}
                          <span className="ml-1 text-muted-foreground">#{k.seq}</span>
                        </Link>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </TableCell>
                    <TableCell className="hidden min-[1360px]:table-cell">
                      <div className="max-w-[130px] truncate 2xl:max-w-[240px]" title={k.customerEmail || undefined}>
                        {k.customerName || <span className="text-muted-foreground">—</span>}
                      </div>
                    </TableCell>
                    <TableCell className="hidden 2xl:table-cell">
                      <EditionBadge edition={k.edition} />
                    </TableCell>
                    <TableCell className="whitespace-nowrap">
                      <div className="flex items-center gap-1">
                        <StatusBadge status={k.status} expired={k.expired} />
                        {k.edition !== "full" && (
                          <span className="2xl:hidden">
                            <EditionBadge edition={k.edition} />
                          </span>
                        )}
                        {k.attempts > 0 && (
                          <Badge
                            variant="destructive"
                            title={`${k.attempts} blocked ${k.attempts === 1 ? "attempt" : "attempts"}`}
                          >
                            <ShieldAlert /> {k.attempts}
                          </Badge>
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="hidden md:table-cell">
                      {k.bound ? (
                        <div
                          className="flex min-w-0 max-w-[100px] items-center gap-1.5 xl:max-w-[200px] 2xl:max-w-[300px]"
                          title={
                            k.registered
                              ? [k.deviceName, k.userName, k.userPhone].filter(Boolean).join(" · ")
                              : `${k.deviceName || "Unnamed PC"} · activated, but the user has not registered yet`
                          }
                        >
                          <span className="truncate">{k.deviceName || "Unnamed PC"}</span>
                          {k.registered ? (
                            <span className="hidden truncate text-muted-foreground xl:inline">· {k.userName}</span>
                          ) : (
                            <>
                              <Badge variant="warning" className="hidden shrink-0 xl:inline-flex">
                                Unregistered
                              </Badge>
                              <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-warning xl:hidden" aria-label="Not registered" />
                            </>
                          )}
                        </div>
                      ) : (
                        <span className="text-muted-foreground">Not activated</span>
                      )}
                    </TableCell>
                    <TableCell
                      className="hidden whitespace-nowrap xl:table-cell"
                      title={k.expiryUtc ? fmtRelative(k.expiryUtc) : undefined}
                    >
                      {k.expiryUtc ? (
                        <span className={cn(soon ? "font-medium text-warning" : "text-muted-foreground")}>
                          {fmtDate(k.expiryUtc)}
                        </span>
                      ) : (
                        <span className="text-muted-foreground">Perpetual</span>
                      )}
                    </TableCell>
                    <TableCell
                      className="hidden whitespace-nowrap text-right text-muted-foreground 2xl:table-cell"
                      title={k.lastSeenUtc ? fmtDateTime(k.lastSeenUtc) : undefined}
                    >
                      {k.lastSeenUtc ? fmtRelative(k.lastSeenUtc) : "never"}
                    </TableCell>
                    <TableCell className="w-10 px-2 text-right last:pr-3">
                      <Button
                        variant="ghost"
                        size="icon-xs"
                        asChild
                        className="text-muted-foreground hover:text-foreground"
                      >
                        <Link
                          href={`/admin/keys/${k.id}`}
                          aria-label={`Open key ${k.serial ? `${k.serial} #${k.seq}` : k.prefix}`}
                          title="Open key"
                        >
                          <ChevronRight />
                        </Link>
                      </Button>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        )}

        {rows !== null && rows.length >= LIST_CAP && (
          <div className="border-t px-4 py-2.5 text-xs text-muted-foreground">
            Showing the newest {LIST_CAP} keys. Search or filter to find older ones.
          </div>
        )}
      </section>
    </div>
  );
}

function TableSkeleton() {
  return (
    <div className="divide-y">
      {Array.from({ length: 6 }, (_, i) => (
        <div key={i} className="flex items-center gap-4 px-4 py-2.5">
          <Skeleton className="h-4 w-40" />
          <Skeleton className="h-4 w-28" />
          <Skeleton className="hidden h-4 w-16 sm:block" />
          <Skeleton className="hidden h-4 flex-1 md:block" />
        </div>
      ))}
    </div>
  );
}

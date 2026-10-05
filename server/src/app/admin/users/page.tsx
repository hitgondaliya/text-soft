"use client";

import { Suspense, useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { ChevronRight, Download, RefreshCw, SearchX, Users } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { DataToolbar } from "@/components/data-toolbar";
import { EmptyState } from "@/components/empty-state";
import { EditionBadge } from "@/components/status-badge";
import { PhoneDisplay } from "@/components/flag";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { cn, fmtDate, fmtDateTime, fmtRelative } from "@/lib/utils";

interface Row {
  id: string;
  keyId: string;
  serial: string;
  seq: number | null;
  keyPrefix: string;
  edition: string;
  keyStatus: string;
  expired: boolean;
  expiryUtc: string | null;
  customerName: string;
  deviceName: string;
  deviceFp: string;
  ip: string;
  appVersion: string;
  status: string;
  activatedUtc: string;
  lastSeenUtc: string;
  user: { name: string; email: string; phoneCountry: string; phoneE164: string; phoneDisplay: string; registeredUtc: string } | null;
}

// ?registered= yes | no. Filtered here so each tab can show its count.
const FILTERS = [
  { value: "all", label: "All PCs", hint: "Every PC that holds a key right now" },
  { value: "yes", label: "Registered", hint: "The user filled in name, email and mobile" },
  { value: "no", label: "Not registered", hint: "Activated, but the registration form is still open" },
] as const;
type Filter = (typeof FILTERS)[number]["value"];

const LIST_CAP = 500;

function asFilter(v: string | null): Filter {
  return (FILTERS.find((f) => f.value === v)?.value ?? "all") as Filter;
}

export default function UsersPage() {
  return (
    <Suspense fallback={null}>
      <UsersInner />
    </Suspense>
  );
}

function UsersInner() {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const registered = asFilter(params.get("registered"));
  const urlQ = params.get("q") ?? "";

  const [all, setAll] = useState<Row[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [tick, setTick] = useState(0);

  const setParams = useCallback(
    (patch: Record<string, string | null>) => {
      const p = new URLSearchParams(params.toString());
      for (const [k, v] of Object.entries(patch)) {
        if (v === null || v === "" || (k === "registered" && v === "all")) p.delete(k);
        else p.set(k, v);
      }
      const s = p.toString();
      router.replace(s ? `${pathname}?${s}` : pathname, { scroll: false });
    },
    [params, pathname, router],
  );

  // Search box: local while typing, written to ?q= after a short pause.
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
    const p = new URLSearchParams({ status: "active" });
    if (urlQ.trim()) p.set("q", urlQ.trim());
    setLoading(true);
    fetch(`/api/admin/activations?${p}`, { cache: "no-store", signal: ac.signal })
      .then(async (r) => {
        if (!r.ok) throw new Error(String(r.status));
        return ((await r.json()) as { activations?: Row[] }).activations ?? [];
      })
      .then((list) => {
        setAll(list);
        setFailed(false);
      })
      .catch(() => {
        if (ac.signal.aborted) return;
        setFailed(true);
        setAll((prev) => prev ?? []);
      })
      .finally(() => {
        if (!ac.signal.aborted) setLoading(false);
      });
    return () => ac.abort();
  }, [urlQ, tick]);

  const counts = useMemo(() => {
    const yes = all?.filter((r) => r.user).length ?? 0;
    return { all: all?.length ?? 0, yes, no: (all?.length ?? 0) - yes };
  }, [all]);

  const rows = useMemo(() => {
    if (!all) return null;
    if (registered === "yes") return all.filter((r) => r.user);
    if (registered === "no") return all.filter((r) => !r.user);
    return all;
  }, [all, registered]);

  const reload = () => setTick((n) => n + 1);
  const filtered = registered !== "all" || urlQ.trim() !== "";

  function exportCsv() {
    if (!rows) return;
    const head = ["name", "email", "mobile", "serial", "key", "edition", "customer", "pc", "activated", "last_seen", "expiry"];
    const lines = rows.map((r) =>
      [
        r.user?.name ?? "",
        r.user?.email ?? "",
        r.user?.phoneE164 ?? "",
        `${r.serial} #${r.seq ?? ""}`,
        r.keyPrefix,
        r.edition,
        r.customerName,
        r.deviceName,
        r.activatedUtc.slice(0, 10),
        r.lastSeenUtc.slice(0, 10),
        r.expiryUtc ? r.expiryUtc.slice(0, 10) : "perpetual",
      ]
        .map((v) => `"${String(v).replace(/"/g, '""')}"`)
        .join(","),
    );
    // BOM so Excel opens names in Gujarati/Hindi as UTF-8.
    const blob = new Blob(["﻿" + [head.join(","), ...lines].join("\n") + "\n"], { type: "text/csv;charset=utf-8" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = `textsoft-users${registered === "all" ? "" : registered === "yes" ? "-registered" : "-unregistered"}.csv`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  }

  function openRow(e: React.MouseEvent, keyId: string) {
    const target = e.target as HTMLElement;
    if (target.closest("a,button,input,[role=button]")) return;
    if (window.getSelection()?.toString()) return;
    const href = `/admin/keys/${keyId}`;
    if (e.metaKey || e.ctrlKey) window.open(href, "_blank", "noopener");
    else router.push(href);
  }

  return (
    <div>
      <PageHeader
        title="End users"
        description="Everyone who activated TextSoft: the name, email and mobile they gave, and the PC it runs on."
        actions={
          <Button variant="outline" size="sm" onClick={exportCsv} disabled={!rows || rows.length === 0}>
            <Download /> Export CSV
          </Button>
        }
      />

      {failed && (
        <Alert
          tone="danger"
          title="Couldn't load the users"
          className="mb-3"
          action={
            <Button variant="outline" size="xs" onClick={reload}>
              <RefreshCw /> Retry
            </Button>
          }
        >
          {all && all.length ? "Showing the last list that loaded." : "Check your connection or sign in again."}
        </Alert>
      )}

      <section className="min-w-0 rounded-lg border bg-card text-card-foreground">
        <div className="border-b px-3 py-2.5 sm:px-4">
          <DataToolbar
            search={q}
            onSearchChange={setQ}
            placeholder="Search name, email, mobile, PC, serial…"
            filters={
              <div className="min-w-0 max-w-full overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
                <Tabs value={registered} onValueChange={(v) => setParams({ registered: v })}>
                  <TabsList aria-label="Filter users">
                    {FILTERS.map((f) => (
                      <TabsTrigger key={f.value} value={f.value} title={f.hint}>
                        {f.label}
                        {all !== null && (
                          <span className="tabular-nums text-muted-foreground">{counts[f.value].toLocaleString("en-IN")}</span>
                        )}
                      </TabsTrigger>
                    ))}
                  </TabsList>
                </Tabs>
              </div>
            }
            actions={
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
            }
          />
        </div>

        {rows === null ? (
          <TableSkeleton />
        ) : rows.length === 0 ? (
          filtered ? (
            <EmptyState
              icon={SearchX}
              title="Nobody matches"
              description="Try another search or filter."
              action={
                <Button variant="outline" size="sm" onClick={() => setParams({ registered: null, q: null })}>
                  Clear filters
                </Button>
              }
            />
          ) : (
            <EmptyState
              icon={Users}
              tone="primary"
              title="No users yet"
              description="When a customer activates and fills in the registration form, they show up here."
            />
          )
        ) : (
          <Table className={cn(loading && "opacity-60 transition-opacity")}>
            <TableHeader>
              <TableRow>
                <TableHead>User</TableHead>
                <TableHead>Mobile</TableHead>
                <TableHead className="hidden lg:table-cell">Email</TableHead>
                <TableHead>Key</TableHead>
                <TableHead className="hidden md:table-cell">PC</TableHead>
                <TableHead className="hidden 2xl:table-cell">Customer</TableHead>
                <TableHead className="hidden xl:table-cell">Activated</TableHead>
                <TableHead className="hidden text-right sm:table-cell">Last seen</TableHead>
                <TableHead className="w-10 px-2 last:pr-3">
                  <span className="sr-only">Open</span>
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((r) => (
                <TableRow key={r.id} className="cursor-pointer" onClick={(e) => openRow(e, r.keyId)}>
                  <TableCell>
                    {r.user ? (
                      <div className="max-w-[180px] truncate font-medium" title={r.user.name}>
                        {r.user.name}
                      </div>
                    ) : (
                      <Badge variant="warning" title="Activated, but the registration form is still open">
                        Not registered
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell className="whitespace-nowrap">
                    <PhoneDisplay country={r.user?.phoneCountry} text={r.user?.phoneDisplay} />
                  </TableCell>
                  <TableCell className="hidden lg:table-cell">
                    {r.user ? (
                      <a
                        href={`mailto:${r.user.email}`}
                        className="block max-w-[220px] truncate hover:text-primary-strong hover:underline"
                        title={r.user.email}
                      >
                        {r.user.email}
                      </a>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="whitespace-nowrap">
                    <div className="flex items-center gap-1.5">
                      <Link
                        href={`/admin/keys/${r.keyId}`}
                        className="font-mono text-xs hover:text-primary-strong hover:underline"
                        title={`Key ${r.keyPrefix}…`}
                      >
                        {r.serial || `${r.keyPrefix}…`}
                        {r.seq ? <span className="ml-1 text-muted-foreground">#{r.seq}</span> : null}
                      </Link>
                      {r.edition && r.edition !== "full" && <EditionBadge edition={r.edition} />}
                      {r.keyStatus === "revoked" ? (
                        <Badge variant="destructive">Revoked</Badge>
                      ) : (
                        r.expired && <Badge variant="warning">Expired</Badge>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="hidden md:table-cell">
                    <div
                      className="max-w-[160px] truncate"
                      title={[r.deviceName || "Unnamed PC", r.ip, r.appVersion && `v${r.appVersion}`].filter(Boolean).join(" · ")}
                    >
                      {r.deviceName || <span className="text-muted-foreground">Unnamed PC</span>}
                    </div>
                  </TableCell>
                  <TableCell className="hidden 2xl:table-cell">
                    <div className="max-w-[180px] truncate">{r.customerName || <span className="text-muted-foreground">—</span>}</div>
                  </TableCell>
                  <TableCell className="hidden whitespace-nowrap text-muted-foreground xl:table-cell" title={fmtDateTime(r.activatedUtc)}>
                    {fmtDate(r.activatedUtc)}
                  </TableCell>
                  <TableCell
                    className="hidden whitespace-nowrap text-right text-muted-foreground sm:table-cell"
                    title={fmtDateTime(r.lastSeenUtc)}
                  >
                    {fmtRelative(r.lastSeenUtc)}
                  </TableCell>
                  <TableCell className="w-10 px-2 text-right last:pr-3">
                    <Button variant="ghost" size="icon-xs" asChild className="text-muted-foreground hover:text-foreground">
                      <Link
                        href={`/admin/keys/${r.keyId}`}
                        aria-label={`Open key ${r.serial ? `${r.serial} #${r.seq ?? ""}` : r.keyPrefix}`}
                        title="Open key"
                      >
                        <ChevronRight />
                      </Link>
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}

        {all !== null && all.length >= LIST_CAP && (
          <div className="border-t px-4 py-2.5 text-xs text-muted-foreground">
            Showing the newest {LIST_CAP} activations. Search to find older ones.
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
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-4 w-28" />
          <Skeleton className="h-4 w-28" />
          <Skeleton className="hidden h-4 flex-1 md:block" />
        </div>
      ))}
    </div>
  );
}

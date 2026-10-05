"use client";

import { Suspense, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Ban, CalendarX, CircleHelp, Hash, MonitorX, RefreshCw, ShieldAlert, ShieldCheck, Undo2 } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { DataToolbar } from "@/components/data-toolbar";
import { StatStrip, type StatItem, type StatTone } from "@/components/stat-strip";
import { AttemptsTable, REASON_OPTIONS, reasonLabel, type AttemptRow } from "@/components/attempts-table";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { cn } from "@/lib/utils";

// ?days= 1 | 7 | 30 (default) | all     ?reason= one of REASON_OPTIONS     ?q= search
const PERIODS = [
  { value: "1", label: "24h", long: "the last 24 hours" },
  { value: "7", label: "7d", long: "the last 7 days" },
  { value: "30", label: "30d", long: "the last 30 days" },
  { value: "all", label: "All", long: "all time" },
] as const;
const DEFAULT_DAYS = "30";
const LIST_CAP = 500;

const REASON_LOOK: Record<string, { icon: React.ElementType; tone: StatTone }> = {
  already_bound: { icon: MonitorX, tone: "danger" },
  released: { icon: Undo2, tone: "warning" },
  serial_mismatch: { icon: Hash, tone: "warning" },
  invalid: { icon: CircleHelp, tone: "neutral" },
  revoked: { icon: Ban, tone: "danger" },
  expired: { icon: CalendarX, tone: "warning" },
};

export default function SecurityPage() {
  return (
    <Suspense fallback={null}>
      <SecurityInner />
    </Suspense>
  );
}

function SecurityInner() {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const daysParam = params.get("days");
  const days = PERIODS.some((p) => p.value === daysParam) ? (daysParam as string) : DEFAULT_DAYS;
  const reasonParam = params.get("reason");
  const reason = REASON_OPTIONS.some((o) => o.value === reasonParam) ? (reasonParam as string) : "all";
  const urlQ = params.get("q") ?? "";

  const [all, setAll] = useState<AttemptRow[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [tick, setTick] = useState(0);

  const setParams = useCallback(
    (patch: Record<string, string | null>) => {
      const p = new URLSearchParams(params.toString());
      for (const [k, v] of Object.entries(patch)) {
        if (v === null || v === "" || (k === "reason" && v === "all") || (k === "days" && v === DEFAULT_DAYS)) p.delete(k);
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

  // The reason is filtered here, so the strip can count every reason for the period.
  useEffect(() => {
    const ac = new AbortController();
    const p = new URLSearchParams();
    if (days !== "all") p.set("days", days);
    if (urlQ.trim()) p.set("q", urlQ.trim());
    setLoading(true);
    fetch(`/api/admin/attempts?${p}`, { cache: "no-store", signal: ac.signal })
      .then(async (r) => {
        if (!r.ok) throw new Error(String(r.status));
        return ((await r.json()) as { attempts?: AttemptRow[] }).attempts ?? [];
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
  }, [days, urlQ, tick]);

  const byReason = useMemo(() => {
    const m: Record<string, number> = {};
    for (const r of all ?? []) m[r.reason] = (m[r.reason] ?? 0) + 1;
    return m;
  }, [all]);

  const rows = useMemo(() => (all && reason !== "all" ? all.filter((r) => r.reason === reason) : all), [all, reason]);

  const period = PERIODS.find((p) => p.value === days)!;
  const hrefFor = (r: string | null) => {
    const p = new URLSearchParams(params.toString());
    if (r) p.set("reason", r);
    else p.delete("reason");
    const s = p.toString();
    return s ? `${pathname}?${s}` : pathname;
  };

  const v = (n: number): number | string | null => (all ? n : failed ? "—" : null);
  const stats: StatItem[] = [
    {
      label: "All refused",
      value: v(all?.length ?? 0),
      icon: (all?.length ?? 0) > 0 ? ShieldAlert : ShieldCheck,
      tone: (all?.length ?? 0) > 0 ? "primary" : "success",
      href: hrefFor(null),
      title: `Every refused activation in ${period.long}`,
    },
    ...REASON_OPTIONS.map((o) => ({
      label: o.label,
      value: v(byReason[o.value] ?? 0),
      icon: REASON_LOOK[o.value]?.icon,
      tone: (byReason[o.value] ?? 0) > 0 ? (REASON_LOOK[o.value]?.tone ?? "neutral") : ("neutral" as StatTone),
      href: hrefFor(o.value),
      title: `${o.explain}: show only these`,
    })),
  ];

  return (
    <div className="space-y-4">
      <PageHeader
        className="mb-0"
        title="Security"
        description="Every activation the server refused. A key tried on a second PC shows up here with that PC's name and IP."
      />

      {failed && (
        <Alert
          tone="danger"
          title="Couldn't load the attempts"
          action={
            <Button variant="outline" size="xs" onClick={() => setTick((n) => n + 1)}>
              <RefreshCw /> Retry
            </Button>
          }
        >
          {all && all.length ? "Showing the last list that loaded." : "Check your connection or sign in again."}
        </Alert>
      )}

      <StatStrip items={stats} />

      <section className="min-w-0 rounded-lg border bg-card text-card-foreground">
        <div className="border-b px-3 py-2.5 sm:px-4">
          <DataToolbar
            search={q}
            onSearchChange={setQ}
            placeholder="Search serial, key, PC name, IP…"
            filters={
              <>
                <Select value={reason} onValueChange={(val) => setParams({ reason: val })}>
                  <SelectTrigger className="h-9 w-full sm:w-44" aria-label="Reason">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All reasons</SelectItem>
                    {REASON_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <Tabs value={days} onValueChange={(val) => setParams({ days: val })}>
                  <TabsList aria-label="Period">
                    {PERIODS.map((p) => (
                      <TabsTrigger key={p.value} value={p.value} title={`Refusals in ${p.long}`}>
                        {p.label}
                      </TabsTrigger>
                    ))}
                  </TabsList>
                </Tabs>
              </>
            }
            actions={
              <>
                <span className="whitespace-nowrap text-xs tabular-nums text-muted-foreground" aria-live="polite">
                  {rows === null
                    ? ""
                    : `${rows.length.toLocaleString("en-IN")} ${reason === "all" ? "" : `${reasonLabel(reason).toLowerCase()} `}${
                        rows.length === 1 ? "attempt" : "attempts"
                      }`}
                </span>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  onClick={() => setTick((n) => n + 1)}
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
          <div className="divide-y">
            {Array.from({ length: 6 }, (_, i) => (
              <div key={i} className="flex items-center gap-4 px-4 py-2.5">
                <Skeleton className="h-4 w-24" />
                <Skeleton className="h-4 w-28" />
                <Skeleton className="h-4 w-20" />
                <Skeleton className="hidden h-4 flex-1 md:block" />
              </div>
            ))}
          </div>
        ) : (
          <div className={cn(loading && "opacity-60 transition-opacity")}>
            <AttemptsTable
              rows={rows}
              emptyText={
                reason !== "all" || urlQ.trim()
                  ? `Nothing matches in ${period.long}.`
                  : `Nothing refused in ${period.long}. Every activation was a legitimate one.`
              }
            />
          </div>
        )}

        <div className="flex flex-wrap items-center justify-between gap-2 border-t px-4 py-2.5 text-xs text-muted-foreground">
          <span>
            A refused attempt never changes the licence. To let a customer move to a new PC, open the key and use{" "}
            <strong className="font-medium text-foreground">Move</strong> on its Device card.
          </span>
          {all !== null && all.length >= LIST_CAP && <span>Counting the newest {LIST_CAP} attempts.</span>}
        </div>
      </section>
    </div>
  );
}

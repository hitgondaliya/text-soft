"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import {
  ShoppingBag,
  KeyRound,
  CheckCircle2,
  Clock,
  Ban,
  MonitorSmartphone,
  UserCheck,
  ShieldAlert,
  ArrowRight,
  Plus,
  RefreshCw,
  Search,
} from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { StatStrip, type StatItem } from "@/components/stat-strip";
import { SectionCard } from "@/components/section-card";
import { EmptyState } from "@/components/empty-state";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { EditionBadge } from "@/components/status-badge";
import { AttemptsTable, type AttemptRow } from "@/components/attempts-table";
import { cn, fmtDateTime, fmtRelative } from "@/lib/utils";

interface Stats {
  orders: number;
  total: number;
  active: number;
  revoked: number;
  expiringSoon: number;
  activations: number;
  registered: number;
  blocked7d: number;
  recent: Array<{ id: string; prefix: string; serial: string; seq: number | null; edition: string; userName: string; lastSeenUtc: string }>;
  recentAttempts: AttemptRow[];
}

function TableSkeleton({ rows = 4 }: { rows?: number }) {
  return (
    <div className="space-y-2.5 p-4">
      {Array.from({ length: rows }, (_, i) => (
        <Skeleton key={i} className={cn("h-4", i % 3 === 0 ? "w-full" : i % 3 === 1 ? "w-5/6" : "w-2/3")} />
      ))}
    </div>
  );
}

export default function Dashboard() {
  const [s, setS] = useState<Stats | null>(null);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setFailed(false);
    try {
      const r = await fetch("/api/admin/stats", { cache: "no-store" });
      if (!r.ok) throw new Error(String(r.status));
      setS((await r.json()) as Stats);
    } catch {
      setFailed(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  // null = skeleton while loading; "—" once a load has failed with nothing to show.
  const v = (n: number | undefined): number | string | null => (s ? (n ?? 0) : failed ? "—" : null);

  const stats: StatItem[] = [
    { label: "Purchases", value: v(s?.orders), icon: ShoppingBag, tone: "primary", href: "/admin/orders", title: "Purchases recorded" },
    { label: "Keys issued", value: v(s?.total), icon: KeyRound, tone: "neutral", href: "/admin/keys", title: "Every key ever issued" },
    { label: "Active keys", value: v(s?.active), icon: CheckCircle2, tone: "success", href: "/admin/keys?status=active", title: "Not revoked and not expired" },
    { label: "Expiring 7d", value: v(s?.expiringSoon), icon: Clock, tone: "warning", href: "/admin/keys?status=expiring", title: "Active keys that expire within 7 days" },
    { label: "Revoked", value: v(s?.revoked), icon: Ban, tone: "danger", href: "/admin/keys?status=revoked", title: "Revoked keys" },
    { label: "PCs active", value: v(s?.activations), icon: MonitorSmartphone, tone: "primary", href: "/admin/users", title: "PCs currently activated" },
    { label: "Registered", value: v(s?.registered), icon: UserCheck, tone: "success", href: "/admin/users?registered=yes", title: "Activated PCs whose user has registered" },
    { label: "Blocked 7d", value: v(s?.blocked7d), icon: ShieldAlert, tone: "danger", href: "/admin/security?days=7", title: "Refused activation attempts in the last 7 days" },
  ];

  const blocked = s?.recentAttempts.length ?? 0;

  return (
    <div className="space-y-4">
      <PageHeader
        title="Dashboard"
        description="Licences, devices and blocked attempts at a glance."
        actions={
          <>
            <Button
              variant="ghost"
              size="icon"
              onClick={() => void load()}
              disabled={loading}
              aria-label="Refresh"
              title="Refresh"
              className="text-muted-foreground hover:text-foreground"
            >
              <RefreshCw className={cn(loading && "animate-spin")} />
            </Button>
            <Button variant="outline" size="sm" asChild className="hidden sm:inline-flex">
              <Link href="/admin/keys">
                <Search /> Find a key
              </Link>
            </Button>
            <Button size="sm" asChild>
              <Link href="/admin/orders?new=1">
                <Plus /> New purchase
              </Link>
            </Button>
          </>
        }
        className="mb-0"
      />

      {failed && (
        <Alert
          tone="danger"
          title="Couldn't load the dashboard"
          action={
            <Button variant="outline" size="xs" onClick={() => void load()}>
              <RefreshCw /> Retry
            </Button>
          }
        >
          {s ? "Showing the last figures that loaded." : "Check your connection or sign in again."}
        </Alert>
      )}

      <StatStrip items={stats} />

      {/* Side by side from xl (a 1366px laptop): the attempts card uses the compact table
          (no IP / Via), so neither table scrolls. The full table is on Security. */}
      <div className="grid gap-4 xl:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <SectionCard
          title="Recently active keys"
          description="Devices that checked in most recently."
          icon={MonitorSmartphone}
          actions={
            <Button variant="ghost" size="xs" asChild className="text-muted-foreground hover:text-foreground">
              <Link href="/admin/keys">
                All keys <ArrowRight />
              </Link>
            </Button>
          }
          flush
        >
          {!s ? (
            failed ? (
              <EmptyState compact title="Nothing to show" description="The figures did not load." />
            ) : (
              <TableSkeleton />
            )
          ) : s.recent.length === 0 ? (
            <EmptyState
              compact
              icon={MonitorSmartphone}
              title="No device has activated yet"
              description="Once a customer installs with their serial and key, the PC shows up here."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Key</TableHead>
                  <TableHead className="hidden sm:table-cell">User</TableHead>
                  <TableHead className="hidden sm:table-cell xl:hidden">Edition</TableHead>
                  <TableHead className="text-right">Last seen</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {s.recent.map((k) => (
                  <TableRow key={k.id}>
                    <TableCell className="whitespace-nowrap font-mono text-xs">
                      <Link href={`/admin/keys/${k.id}`} className="text-primary-strong hover:underline">
                        {k.serial || `${k.prefix}…`}
                      </Link>
                      {k.seq ? <span className="ml-1 text-muted-foreground">#{k.seq}</span> : null}
                      {/* phones: the user goes under the key instead of its own column */}
                      <div className="max-w-[190px] truncate font-sans text-[11px] text-muted-foreground sm:hidden">
                        {k.userName || "Not registered"}
                      </div>
                    </TableCell>
                    <TableCell className="hidden sm:table-cell">
                      {/* truncate on an inner block: table cells ignore max-width */}
                      <div className="max-w-[200px] truncate xl:max-w-[150px] 2xl:max-w-[220px]" title={k.userName || undefined}>
                        {k.userName || <span className="text-muted-foreground">Not registered</span>}
                      </div>
                    </TableCell>
                    <TableCell className="hidden sm:table-cell xl:hidden">
                      <EditionBadge edition={k.edition} />
                    </TableCell>
                    <TableCell className="whitespace-nowrap text-right text-muted-foreground" title={fmtDateTime(k.lastSeenUtc)}>
                      {fmtRelative(k.lastSeenUtc)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </SectionCard>

        <SectionCard
          title="Latest blocked attempts"
          description="Keys tried on a second PC, wrong serials, expired keys."
          icon={ShieldAlert}
          iconClassName={!s ? undefined : blocked ? "text-destructive" : "text-success"}
          actions={
            <Button variant="ghost" size="xs" asChild className="text-muted-foreground hover:text-foreground">
              <Link href="/admin/security">
                Security <ArrowRight />
              </Link>
            </Button>
          }
          flush
        >
          {!s ? (
            failed ? (
              <EmptyState compact title="Nothing to show" description="The figures did not load." />
            ) : (
              <TableSkeleton rows={3} />
            )
          ) : (
            <AttemptsTable compact rows={s.recentAttempts} emptyText="Nothing refused so far." />
          )}
        </SectionCard>
      </div>
    </div>
  );
}

"use client";

import Link from "next/link";
import { ShieldCheck } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { EmptyState } from "@/components/empty-state";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { cn, fmtDateTime, fmtRelative } from "@/lib/utils";

/**
 * AttemptsTable — every refused activation, one compact line per attempt.
 * Used on the dashboard, the key page, the purchase page and Security.
 *
 *   <AttemptsTable rows={attempts} />                    // full: when, key, reason, PC, IP, key is on, via
 *   <AttemptsTable rows={attempts} showKey={false} />    // on a key's own page
 *   <AttemptsTable rows={attempts} compact />            // half-width cards: drops IP and Via, no absolute time
 *
 * Props:
 *   rows        AttemptRow[]
 *   showKey?    boolean   show the Key column (default true)
 *   compact?    boolean   five columns at most, for a card beside another card (default false);
 *                         "Key is on" hides from xl to 2xl, where the dashboard card is narrowest
 *   emptyText?  string    shown with a green shield when there are no rows
 *   className?  string    on the table
 */

export interface AttemptRow {
  id: string;
  keyId: string | null;
  serial: string;
  seq: number | null;
  keyPrefix: string;
  known: boolean;
  deviceName: string;
  deviceFp: string;
  ip: string;
  appVersion: string;
  reason: string;
  source: string;
  boundDeviceName: string;
  createdUtc: string;
}

type ReasonVariant = "destructive" | "warning" | "secondary";

const REASONS: Record<string, { label: string; variant: ReasonVariant; explain: string }> = {
  already_bound: { label: "Second device", variant: "destructive", explain: "The key is already on another PC" },
  released: { label: "Old device", variant: "warning", explain: "A PC the key was moved away from" },
  serial_mismatch: { label: "Wrong serial", variant: "warning", explain: "The key does not belong to that serial" },
  invalid: { label: "Unknown key", variant: "secondary", explain: "No such key exists" },
  revoked: { label: "Revoked", variant: "destructive", explain: "The key is revoked" },
  expired: { label: "Expired", variant: "warning", explain: "The key has expired" },
};

export function reasonLabel(reason: string): string {
  return REASONS[reason]?.label ?? reason;
}

export function ReasonBadge({ reason }: { reason: string }) {
  const r = REASONS[reason] ?? { label: reason, variant: "secondary" as const, explain: "" };
  return (
    <Badge variant={r.variant} title={r.explain || undefined}>
      {r.label}
    </Badge>
  );
}

/** Reason filter options, in display order: { value, label, explain }. */
export const REASON_OPTIONS = Object.entries(REASONS).map(([value, r]) => ({ value, label: r.label, explain: r.explain }));

/** "05 Oct, 18:08" — the absolute time beside the relative one, without the year when it is this year. */
function shortTime(iso: string): string {
  const d = new Date(iso);
  const sameYear = d.getFullYear() === new Date().getFullYear();
  return d.toLocaleString("en-GB", {
    day: "2-digit",
    month: "short",
    ...(sameYear ? {} : { year: "numeric" }),
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function AttemptsTable({
  rows,
  showKey = true,
  compact = false,
  emptyText = "No blocked attempts. Nobody has tried this key on another PC.",
  className,
}: {
  rows: AttemptRow[];
  showKey?: boolean;
  compact?: boolean;
  emptyText?: string;
  className?: string;
}) {
  if (rows.length === 0) {
    return <EmptyState compact icon={ShieldCheck} tone="success" title="All clear" description={emptyText} />;
  }
  return (
    <Table className={className}>
      <TableHeader>
        <TableRow>
          <TableHead>When</TableHead>
          {showKey && <TableHead>Key</TableHead>}
          <TableHead>Reason</TableHead>
          <TableHead>Tried from</TableHead>
          {!compact && <TableHead className="hidden md:table-cell">IP</TableHead>}
          <TableHead className={cn("hidden", compact ? "sm:table-cell xl:hidden 2xl:table-cell" : "lg:table-cell")}>Key is on</TableHead>
          {!compact && <TableHead className="hidden text-right xl:table-cell">Via</TableHead>}
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((t) => (
          <TableRow key={t.id}>
            <TableCell className="whitespace-nowrap" title={fmtDateTime(t.createdUtc)}>
              {fmtRelative(t.createdUtc)}
              {!compact && (
                <span className="ml-1.5 hidden text-xs text-muted-foreground 2xl:inline">{shortTime(t.createdUtc)}</span>
              )}
            </TableCell>
            {showKey && (
              <TableCell className="whitespace-nowrap font-mono text-xs">
                {t.keyId ? (
                  <Link href={`/admin/keys/${t.keyId}`} className="hover:text-primary-strong hover:underline">
                    {t.serial || `${t.keyPrefix}…`}
                    {t.seq ? <span className="ml-1 text-muted-foreground">#{t.seq}</span> : null}
                  </Link>
                ) : (
                  <span className="text-muted-foreground" title="Typed key, not one we issued">
                    {t.keyPrefix || "?"}…
                  </span>
                )}
              </TableCell>
            )}
            <TableCell className="whitespace-nowrap">
              <ReasonBadge reason={t.reason} />
            </TableCell>
            <TableCell>
              <div
                className={cn("flex items-baseline gap-1.5", compact ? "max-w-[140px]" : "max-w-[240px]")}
                title={t.deviceFp ? `Fingerprint ${t.deviceFp}` : undefined}
              >
                <span className={cn("truncate", t.deviceName ? "font-medium" : "text-muted-foreground")}>
                  {t.deviceName || "Unnamed PC"}
                </span>
                {!compact && t.deviceFp && (
                  <span className="hidden shrink-0 font-mono text-[11px] text-muted-foreground sm:inline">
                    {t.deviceFp.slice(0, 8)}
                  </span>
                )}
              </div>
            </TableCell>
            {!compact && (
              <TableCell className="hidden whitespace-nowrap font-mono text-xs text-muted-foreground md:table-cell">
                {t.ip || "—"}
              </TableCell>
            )}
            <TableCell className={cn("hidden", compact ? "sm:table-cell xl:hidden 2xl:table-cell" : "lg:table-cell")}>
              <div className={cn("truncate", compact ? "max-w-[140px]" : "max-w-[180px]")}>
                {t.boundDeviceName || <span className="text-muted-foreground">—</span>}
              </div>
            </TableCell>
            {!compact && (
              <TableCell className="hidden whitespace-nowrap text-right text-xs text-muted-foreground xl:table-cell">
                {t.source === "installer" ? "Installer" : "App"}
                {t.appVersion && <span className="ml-1 font-mono text-[11px]">{t.appVersion}</span>}
              </TableCell>
            )}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

"use client";

import Link from "next/link";
import { ShieldAlert } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { fmtDateTime, fmtRelative } from "@/lib/utils";

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

const REASONS: Record<string, { label: string; variant: "destructive" | "warning" | "secondary"; explain: string }> = {
  already_bound: { label: "Second device", variant: "destructive", explain: "Key is already on another PC" },
  released: { label: "Old device", variant: "warning", explain: "PC the key was moved away from" },
  serial_mismatch: { label: "Wrong serial", variant: "warning", explain: "Key does not belong to that serial" },
  invalid: { label: "Unknown key", variant: "secondary", explain: "No such key" },
  revoked: { label: "Revoked", variant: "destructive", explain: "Key is revoked" },
  expired: { label: "Expired", variant: "warning", explain: "Key has expired" },
};

export function ReasonBadge({ reason }: { reason: string }) {
  const r = REASONS[reason] ?? { label: reason, variant: "secondary" as const, explain: "" };
  return (
    <Badge variant={r.variant} title={r.explain}>
      {r.label}
    </Badge>
  );
}

export const REASON_OPTIONS = Object.entries(REASONS).map(([value, r]) => ({ value, label: r.label }));

/** The blocked-attempts table used on the key page, the purchase page and Security. */
export function AttemptsTable({
  rows,
  showKey = true,
  emptyText = "No blocked attempts. Nobody has tried this key on another PC.",
}: {
  rows: AttemptRow[];
  showKey?: boolean;
  emptyText?: string;
}) {
  if (rows.length === 0) {
    return (
      <div className="flex flex-col items-center px-6 py-10 text-center">
        <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-full bg-success/10 text-success">
          <ShieldAlert className="h-4 w-4" />
        </div>
        <p className="max-w-sm text-sm text-muted-foreground">{emptyText}</p>
      </div>
    );
  }
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>When</TableHead>
          {showKey && <TableHead>Key</TableHead>}
          <TableHead>Reason</TableHead>
          <TableHead>Tried from</TableHead>
          <TableHead className="hidden md:table-cell">IP</TableHead>
          <TableHead className="hidden lg:table-cell">Key is on</TableHead>
          <TableHead className="hidden text-right sm:table-cell">Via</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((t) => (
          <TableRow key={t.id}>
            <TableCell className="whitespace-nowrap">
              <div>{fmtRelative(t.createdUtc)}</div>
              <div className="text-xs text-muted-foreground">{fmtDateTime(t.createdUtc)}</div>
            </TableCell>
            {showKey && (
              <TableCell className="whitespace-nowrap font-mono text-xs">
                {t.keyId ? (
                  <Link href={`/admin/keys/${t.keyId}`} className="text-primary hover:underline">
                    {t.serial} <span className="text-muted-foreground">#{t.seq}</span>
                  </Link>
                ) : (
                  <span className="text-muted-foreground">{t.keyPrefix || "?"}…</span>
                )}
              </TableCell>
            )}
            <TableCell>
              <ReasonBadge reason={t.reason} />
            </TableCell>
            <TableCell className="font-medium">
              {t.deviceName || <span className="text-muted-foreground">Unnamed PC</span>}
              {t.deviceFp && (
                <div className="font-mono text-[11px] text-muted-foreground">{t.deviceFp.slice(0, 12)}…</div>
              )}
            </TableCell>
            <TableCell className="hidden font-mono text-xs text-muted-foreground md:table-cell">{t.ip}</TableCell>
            <TableCell className="hidden lg:table-cell">
              {t.boundDeviceName || <span className="text-muted-foreground">—</span>}
            </TableCell>
            <TableCell className="hidden text-right text-xs text-muted-foreground sm:table-cell">
              {t.source === "installer" ? "Installer" : "App"}
              {t.appVersion && <div className="text-[11px]">{t.appVersion}</div>}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

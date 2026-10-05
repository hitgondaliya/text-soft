"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { History, RefreshCw } from "lucide-react";
import { SectionCard } from "@/components/section-card";
import { DataToolbar } from "@/components/data-toolbar";
import { EmptyState } from "@/components/empty-state";
import { Alert } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { cn, fmtDateTime, fmtRelative } from "@/lib/utils";

/**
 * AuditLog — the panel's activity log for owners (GET /api/admin/audit): who showed or
 * copied which keys, reissues, re-sends, purchases, team and email-settings changes.
 * Keys themselves are never logged, only the serial and key number.
 */

interface Entry {
  id: string;
  adminEmail: string;
  action: string;
  target: string;
  detail: Record<string, unknown>;
  createdUtc: string;
}

type Group = "all" | "keys" | "sales" | "team" | "settings";

const GROUPS: Array<{ value: Group; label: string; hint: string }> = [
  { value: "all", label: "All", hint: "Everything, newest first" },
  { value: "keys", label: "Key access", hint: "Keys shown, copied, re-sent or reissued" },
  { value: "sales", label: "Sales", hint: "Purchases and key changes" },
  { value: "team", label: "Team", hint: "Panel users" },
  { value: "settings", label: "Settings", hint: "Email settings and tests" },
];

type Tone = "warning" | "destructive" | "secondary" | "outline" | "success";

const ACTIONS: Record<string, { label: string; tone: Tone }> = {
  "key.reveal": { label: "Showed a key", tone: "warning" },
  "order.reveal": { label: "Copied all keys", tone: "warning" },
  "key.reveal_refused": { label: "Show refused (limit)", tone: "destructive" },
  "order.reveal_refused": { label: "Copy refused (limit)", tone: "destructive" },
  "key.reissue": { label: "Reissued a key", tone: "warning" },
  "order.resend": { label: "Emailed keys", tone: "warning" },
  "order.resend_failed": { label: "Email failed", tone: "destructive" },
  "order.create": { label: "New purchase", tone: "success" },
  "order.revokeAll": { label: "Revoked purchase", tone: "destructive" },
  "order.unrevokeAll": { label: "Restored purchase", tone: "outline" },
  "order.extendAll": { label: "Extended purchase", tone: "outline" },
  "order.setExpiryAll": { label: "Set purchase expiry", tone: "outline" },
  "order.updateCustomer": { label: "Edited customer", tone: "outline" },
  "key.revoke": { label: "Revoked a key", tone: "destructive" },
  "key.unrevoke": { label: "Restored a key", tone: "outline" },
  "key.extend": { label: "Extended a key", tone: "outline" },
  "key.setExpiry": { label: "Set key expiry", tone: "outline" },
  "key.releaseDevice": { label: "Moved a key", tone: "outline" },
  "admin.create": { label: "Added a user", tone: "secondary" },
  "admin.delete": { label: "Removed a user", tone: "destructive" },
  "admin.setPassword": { label: "Changed a password", tone: "secondary" },
  "admin.setRole": { label: "Changed a role", tone: "secondary" },
  "admin.disable": { label: "Disabled a user", tone: "destructive" },
  "admin.enable": { label: "Enabled a user", tone: "secondary" },
  "admin.rename": { label: "Renamed a user", tone: "secondary" },
  "settings.smtp": { label: "Saved email settings", tone: "secondary" },
  "settings.smtp_reset": { label: "Reset email settings", tone: "secondary" },
  "settings.smtp_test": { label: "Sent a test email", tone: "secondary" },
};

/** "to: a@b.c · included: 3" — primitives only, ids left out (the target already says what). */
function summarize(detail: Record<string, unknown>): string {
  return Object.entries(detail)
    .filter(([k, v]) => v !== undefined && v !== null && v !== "" && k !== "keyId" && (typeof v !== "object" || Array.isArray(v)))
    .map(([k, v]) => `${k}: ${Array.isArray(v) ? v.join(", ") : String(v)}`)
    .join(" · ");
}

export function AuditLog() {
  const [group, setGroup] = useState<Group>("all");
  const [q, setQ] = useState("");
  const [rows, setRows] = useState<Entry[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [loading, setLoading] = useState(false);
  const seq = useRef(0);

  const load = useCallback(async (g: Group, search: string) => {
    const mine = ++seq.current;
    setLoading(true);
    setFailed(false);
    try {
      const qs = new URLSearchParams({ group: g, limit: "100" });
      if (search.trim()) qs.set("q", search.trim());
      const r = await fetch(`/api/admin/audit?${qs}`, { cache: "no-store" });
      if (!r.ok) throw new Error(String(r.status));
      const b = (await r.json()) as { entries: Entry[] };
      if (mine === seq.current) setRows(b.entries);
    } catch {
      if (mine === seq.current) setFailed(true);
    } finally {
      if (mine === seq.current) setLoading(false);
    }
  }, []);

  // Search waits for a pause in typing; a tab change loads at once.
  useEffect(() => {
    const t = setTimeout(() => void load(group, q), q ? 300 : 0);
    return () => clearTimeout(t);
  }, [group, q, load]);

  return (
    <SectionCard
      title="Activity log"
      icon={History}
      description="Who showed, copied, emailed or reissued keys, and every team and email-settings change. Latest 100."
      actions={
        <Button
          variant="ghost"
          size="icon-sm"
          onClick={() => void load(group, q)}
          disabled={loading}
          aria-label="Refresh the activity log"
          title="Refresh"
          className="text-muted-foreground hover:text-foreground"
        >
          <RefreshCw className={cn(loading && "animate-spin")} />
        </Button>
      }
      flush
    >
      <div className="border-b px-3 py-2.5 sm:px-4">
        <DataToolbar
          search={q}
          onSearchChange={setQ}
          placeholder="Search admin, serial, action…"
          filters={
            <div className="min-w-0 max-w-full overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
              <Tabs value={group} onValueChange={(v) => setGroup(v as Group)}>
                <TabsList aria-label="Filter the activity log">
                  {GROUPS.map((g) => (
                    <TabsTrigger key={g.value} value={g.value} title={g.hint}>
                      {g.label}
                    </TabsTrigger>
                  ))}
                </TabsList>
              </Tabs>
            </div>
          }
        />
      </div>

      {failed && (
        <div className="p-3 sm:px-4">
          <Alert
            tone="danger"
            title="Couldn't load the activity log"
            action={
              <Button variant="outline" size="xs" onClick={() => void load(group, q)}>
                <RefreshCw /> Retry
              </Button>
            }
          />
        </div>
      )}

      {rows === null ? (
        !failed && (
          <div className="space-y-2 p-4" aria-hidden>
            {Array.from({ length: 5 }, (_, i) => (
              <Skeleton key={i} className="h-6" />
            ))}
          </div>
        )
      ) : rows.length === 0 ? (
        <EmptyState
          compact
          icon={History}
          title={q ? "Nothing matches that search" : "Nothing logged here yet"}
          description={q ? "Try a serial such as TXS-2026-0001, or an admin's email." : undefined}
        />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>When</TableHead>
              <TableHead>Action</TableHead>
              <TableHead>Target</TableHead>
              <TableHead className="hidden md:table-cell">By</TableHead>
              <TableHead className="hidden xl:table-cell">Details</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((e) => {
              const a = ACTIONS[e.action] ?? { label: e.action, tone: "outline" as Tone };
              const detail = summarize(e.detail);
              return (
                <TableRow key={e.id}>
                  <TableCell className="whitespace-nowrap" title={fmtDateTime(e.createdUtc)}>
                    {fmtRelative(e.createdUtc)}
                  </TableCell>
                  <TableCell className="whitespace-nowrap">
                    <Badge variant={a.tone} title={e.action}>
                      {a.label}
                    </Badge>
                  </TableCell>
                  <TableCell className="whitespace-nowrap font-mono text-xs">
                    {e.target || <span className="text-muted-foreground">—</span>}
                    {/* phones: who did it goes under the target */}
                    <div className="max-w-[180px] truncate font-sans text-[11px] text-muted-foreground md:hidden">{e.adminEmail}</div>
                  </TableCell>
                  <TableCell className="hidden md:table-cell">
                    <div className="max-w-[220px] truncate" title={e.adminEmail}>
                      {e.adminEmail}
                    </div>
                  </TableCell>
                  <TableCell className="hidden xl:table-cell">
                    <div className="max-w-[420px] truncate text-xs text-muted-foreground" title={detail || undefined}>
                      {detail || "—"}
                    </div>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      )}
    </SectionCard>
  );
}

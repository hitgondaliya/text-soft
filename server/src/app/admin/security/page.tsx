"use client";

import { Suspense, useCallback, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Search, ShieldAlert, ShieldCheck } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { AttemptsTable, REASON_OPTIONS, type AttemptRow } from "@/components/attempts-table";

export default function SecurityPage() {
  return (
    <Suspense fallback={null}>
      <SecurityInner />
    </Suspense>
  );
}

function SecurityInner() {
  const params = useSearchParams();
  const [rows, setRows] = useState<AttemptRow[] | null>(null);
  const [reason, setReason] = useState(params.get("reason") ?? "all");
  const [days, setDays] = useState(params.get("days") ?? "30");
  const [q, setQ] = useState(params.get("q") ?? "");

  const load = useCallback(() => {
    const p = new URLSearchParams();
    if (reason !== "all") p.set("reason", reason);
    if (days !== "all") p.set("days", days);
    if (q) p.set("q", q);
    fetch(`/api/admin/attempts?${p}`)
      .then((r) => r.json())
      .then((d) => setRows(d.attempts ?? []))
      .catch(() => setRows([]));
  }, [reason, days, q]);

  useEffect(() => {
    load();
  }, [load]);

  const secondDevice = rows?.filter((r) => r.reason === "already_bound").length ?? 0;

  return (
    <div>
      <PageHeader
        title="Security"
        description="Every activation the server refused. A key tried on a second PC shows up here with that PC's name and IP."
      />

      <div className="mb-4 grid gap-4 sm:grid-cols-3">
        <Card>
          <CardContent className="flex items-center gap-4 p-5">
            <div className={`flex h-10 w-10 items-center justify-center rounded-lg ${secondDevice ? "bg-destructive/10 text-destructive" : "bg-success/10 text-success"}`}>
              {secondDevice ? <ShieldAlert className="h-4 w-4" /> : <ShieldCheck className="h-4 w-4" />}
            </div>
            <div>
              <div className="text-2xl font-bold tracking-tight">{rows ? secondDevice : "…"}</div>
              <div className="text-xs font-medium text-muted-foreground">Second-device attempts in view</div>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex items-center gap-4 p-5">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted text-foreground">
              <ShieldAlert className="h-4 w-4" />
            </div>
            <div>
              <div className="text-2xl font-bold tracking-tight">{rows ? rows.length : "…"}</div>
              <div className="text-xs font-medium text-muted-foreground">All refusals in view</div>
            </div>
          </CardContent>
        </Card>
        <Card className="border-dashed">
          <CardContent className="p-5 text-xs text-muted-foreground">
            A refused attempt never changes the licence. To let a customer move to a new PC, open the key and use{" "}
            <strong>Move to another device</strong>.
          </CardContent>
        </Card>
      </div>

      <div className="mb-4 flex flex-col gap-3 sm:flex-row">
        <Select value={reason} onValueChange={setReason}>
          <SelectTrigger className="sm:w-48">
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
        <Select value={days} onValueChange={setDays}>
          <SelectTrigger className="sm:w-40">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="1">Last 24 hours</SelectItem>
            <SelectItem value="7">Last 7 days</SelectItem>
            <SelectItem value="30">Last 30 days</SelectItem>
            <SelectItem value="all">All time</SelectItem>
          </SelectContent>
        </Select>
        <div className="relative flex-1 sm:max-w-md">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="pl-9"
            placeholder="Search serial, key, PC name, IP…"
            value={q}
            onChange={(e) => setQ(e.target.value)}
          />
        </div>
      </div>

      <Card>
        <CardContent className="p-0">
          {rows === null ? (
            <div className="space-y-3 p-6">
              <Skeleton className="h-5 w-full" />
              <Skeleton className="h-5 w-5/6" />
              <Skeleton className="h-5 w-2/3" />
            </div>
          ) : (
            <AttemptsTable rows={rows} emptyText="Nothing refused in this period. Every activation was a legitimate one." />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

"use client";

import { Suspense, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Search, Users, Download } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { EditionBadge } from "@/components/status-badge";
import { PhoneDisplay } from "@/components/flag";
import { fmtDate, fmtRelative } from "@/lib/utils";

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

export default function UsersPage() {
  return (
    <Suspense fallback={null}>
      <UsersInner />
    </Suspense>
  );
}

function UsersInner() {
  const params = useSearchParams();
  const [rows, setRows] = useState<Row[] | null>(null);
  const [registered, setRegistered] = useState(params.get("registered") ?? "all");
  const [q, setQ] = useState(params.get("q") ?? "");

  const load = useCallback(() => {
    const p = new URLSearchParams({ status: "active" });
    if (registered !== "all") p.set("registered", registered);
    if (q) p.set("q", q);
    fetch(`/api/admin/activations?${p}`)
      .then((r) => r.json())
      .then((d) => setRows(d.activations ?? []))
      .catch(() => setRows([]));
  }, [registered, q]);

  useEffect(() => {
    load();
  }, [load]);

  function exportCsv() {
    if (!rows) return;
    const head = ["name", "email", "mobile", "serial", "key", "edition", "pc", "activated", "expiry"];
    const lines = rows.map((r) =>
      [
        r.user?.name ?? "",
        r.user?.email ?? "",
        r.user?.phoneE164 ?? "",
        `${r.serial} #${r.seq ?? ""}`,
        r.keyPrefix,
        r.edition,
        r.deviceName,
        r.activatedUtc.slice(0, 10),
        r.expiryUtc ? r.expiryUtc.slice(0, 10) : "perpetual",
      ]
        .map((v) => `"${String(v).replace(/"/g, '""')}"`)
        .join(","),
    );
    const blob = new Blob([[head.join(","), ...lines].join("\n") + "\n"], { type: "text/csv" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = "textsoft-users.csv";
    a.click();
    URL.revokeObjectURL(a.href);
  }

  return (
    <div>
      <PageHeader title="End users" description="Everyone who activated TextSoft — the name, email and mobile they gave, and the PC it runs on.">
        <Button variant="outline" onClick={exportCsv} disabled={!rows || rows.length === 0}>
          <Download /> Export CSV
        </Button>
      </PageHeader>

      <div className="mb-4 flex flex-col gap-3 sm:flex-row">
        <Select value={registered} onValueChange={setRegistered}>
          <SelectTrigger className="sm:w-48">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All activated PCs</SelectItem>
            <SelectItem value="yes">Registered</SelectItem>
            <SelectItem value="no">Not registered yet</SelectItem>
          </SelectContent>
        </Select>
        <div className="relative flex-1 sm:max-w-md">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="pl-9"
            placeholder="Search name, email, mobile, PC, serial…"
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
          ) : rows.length === 0 ? (
            <div className="flex flex-col items-center px-6 py-16 text-center">
              <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-primary/10 text-primary">
                <Users className="h-5 w-5" />
              </div>
              <h3 className="text-base font-semibold">No users yet</h3>
              <p className="mt-1 max-w-sm text-sm text-muted-foreground">
                When a customer activates and fills in the registration form, they show up here.
              </p>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>User</TableHead>
                  <TableHead>Mobile</TableHead>
                  <TableHead className="hidden md:table-cell">Email</TableHead>
                  <TableHead>Key</TableHead>
                  <TableHead className="hidden lg:table-cell">PC</TableHead>
                  <TableHead className="hidden xl:table-cell">Customer</TableHead>
                  <TableHead className="hidden text-right sm:table-cell">Activated</TableHead>
                  <TableHead className="hidden text-right lg:table-cell">Last seen</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>
                      {r.user ? (
                        <div className="font-medium">{r.user.name}</div>
                      ) : (
                        <Badge variant="warning">Not registered</Badge>
                      )}
                      <div className="mt-0.5 md:hidden">
                        <span className="text-xs text-muted-foreground">{r.user?.email}</span>
                      </div>
                    </TableCell>
                    <TableCell>
                      <PhoneDisplay country={r.user?.phoneCountry} text={r.user?.phoneDisplay} />
                    </TableCell>
                    <TableCell className="hidden md:table-cell">
                      {r.user ? (
                        <a href={`mailto:${r.user.email}`} className="hover:underline">
                          {r.user.email}
                        </a>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </TableCell>
                    <TableCell className="whitespace-nowrap font-mono text-xs">
                      <Link href={`/admin/keys/${r.keyId}`} className="text-primary hover:underline">
                        {r.serial} <span className="text-muted-foreground">#{r.seq}</span>
                      </Link>
                      <div className="mt-0.5">
                        <EditionBadge edition={r.edition} />
                        {r.expired && (
                          <Badge variant="warning" className="ml-1">
                            Expired
                          </Badge>
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="hidden lg:table-cell">
                      <div>{r.deviceName || "Unnamed PC"}</div>
                      <div className="text-xs text-muted-foreground">
                        {r.ip} · v{r.appVersion || "?"}
                      </div>
                    </TableCell>
                    <TableCell className="hidden xl:table-cell">{r.customerName || "—"}</TableCell>
                    <TableCell className="hidden whitespace-nowrap text-right text-muted-foreground sm:table-cell">
                      {fmtDate(r.activatedUtc)}
                    </TableCell>
                    <TableCell className="hidden whitespace-nowrap text-right text-muted-foreground lg:table-cell">
                      {fmtRelative(r.lastSeenUtc)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

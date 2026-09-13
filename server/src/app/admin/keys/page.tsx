"use client";

import { Suspense, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Plus, Search, KeyRound, ShieldAlert } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { EditionBadge, StatusBadge } from "@/components/status-badge";
import { fmtDate, fmtRelative } from "@/lib/utils";

interface KeyRow {
  id: string;
  prefix: string;
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

export default function KeysPage() {
  return (
    <Suspense fallback={null}>
      <Keys />
    </Suspense>
  );
}

function Keys() {
  const params = useSearchParams();
  const [rows, setRows] = useState<KeyRow[] | null>(null);
  const [status, setStatus] = useState(params.get("status") ?? "all");
  const [q, setQ] = useState(params.get("q") ?? "");

  const load = useCallback(() => {
    const p = new URLSearchParams();
    if (status !== "all") p.set("status", status);
    if (q) p.set("q", q);
    fetch(`/api/admin/keys?${p}`)
      .then((r) => r.json())
      .then((d) => setRows(d.keys ?? []))
      .catch(() => setRows([]));
  }, [status, q]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div>
      <PageHeader title="License keys" description="Every key ever issued, the PC it lives on and the person using it.">
        <Button asChild>
          <Link href="/admin/orders?new=1">
            <Plus /> New purchase
          </Link>
        </Button>
      </PageHeader>

      <div className="mb-4 flex flex-col gap-3 sm:flex-row">
        <Select value={status} onValueChange={setStatus}>
          <SelectTrigger className="sm:w-48">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All keys</SelectItem>
            <SelectItem value="active">Active</SelectItem>
            <SelectItem value="bound">On a PC</SelectItem>
            <SelectItem value="unused">Never activated</SelectItem>
            <SelectItem value="attempts">With blocked attempts</SelectItem>
            <SelectItem value="expired">Expired</SelectItem>
            <SelectItem value="revoked">Revoked</SelectItem>
          </SelectContent>
        </Select>
        <div className="relative flex-1 sm:max-w-md">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="pl-9"
            placeholder="Search key, serial, customer, user, email, mobile…"
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
                <KeyRound className="h-5 w-5" />
              </div>
              <h3 className="text-base font-semibold">No keys here</h3>
              <p className="mt-1 max-w-sm text-sm text-muted-foreground">
                Keys are issued from a purchase. Create one and they'll appear here with their serial.
              </p>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Key</TableHead>
                  <TableHead>Serial</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>User</TableHead>
                  <TableHead className="hidden lg:table-cell">PC</TableHead>
                  <TableHead className="hidden text-right md:table-cell">Blocked</TableHead>
                  <TableHead className="hidden md:table-cell">Expiry</TableHead>
                  <TableHead className="hidden xl:table-cell">Customer</TableHead>
                  <TableHead className="hidden text-right xl:table-cell">Last seen</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((k) => (
                  <TableRow key={k.id}>
                    <TableCell className="whitespace-nowrap font-mono">
                      <Link href={`/admin/keys/${k.id}`} className="text-primary hover:underline">
                        {k.prefix}…
                      </Link>
                      <div className="mt-0.5">
                        <EditionBadge edition={k.edition} />
                      </div>
                    </TableCell>
                    <TableCell className="whitespace-nowrap font-mono text-xs">
                      {k.serial ? (
                        <Link href={`/admin/orders/${k.serial}`} className="hover:underline">
                          {k.serial}
                          <span className="ml-1 text-muted-foreground">#{k.seq}</span>
                        </Link>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={k.status} expired={k.expired} />
                    </TableCell>
                    <TableCell>
                      {k.registered ? (
                        <>
                          <div className="font-medium">{k.userName}</div>
                          <div className="text-xs text-muted-foreground">{k.userPhone}</div>
                        </>
                      ) : k.bound ? (
                        <Badge variant="warning">Not registered</Badge>
                      ) : (
                        <span className="text-muted-foreground">Not activated</span>
                      )}
                    </TableCell>
                    <TableCell className="hidden text-muted-foreground lg:table-cell">
                      {k.deviceName || (k.bound ? "Unnamed PC" : "—")}
                    </TableCell>
                    <TableCell className="hidden text-right md:table-cell">
                      {k.attempts > 0 ? (
                        <Badge variant="destructive">
                          <ShieldAlert className="h-3 w-3" /> {k.attempts}
                        </Badge>
                      ) : (
                        <span className="text-muted-foreground">0</span>
                      )}
                    </TableCell>
                    <TableCell className="hidden whitespace-nowrap md:table-cell">
                      {k.expiryUtc ? (
                        <span className="text-muted-foreground">{fmtDate(k.expiryUtc)}</span>
                      ) : (
                        <Badge variant="outline">Perpetual</Badge>
                      )}
                    </TableCell>
                    <TableCell className="hidden xl:table-cell">
                      {k.customerName || <span className="text-muted-foreground">—</span>}
                    </TableCell>
                    <TableCell className="hidden whitespace-nowrap text-right text-muted-foreground xl:table-cell">
                      {k.lastSeenUtc ? fmtRelative(k.lastSeenUtc) : "never"}
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

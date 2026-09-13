"use client";

import { Suspense, useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Plus, Search, ShoppingBag } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { EditionBadge } from "@/components/status-badge";
import { fmtDate } from "@/lib/utils";
import { NewPurchaseDialog } from "./new-purchase-dialog";

interface OrderRow {
  id: string;
  serial: string;
  customerName: string;
  customerEmail: string;
  customerPhoneDisplay: string;
  company: string;
  edition: string;
  deviceLimit: number;
  quantity: number;
  issued: number;
  revoked: number;
  activated: number;
  expiryUtc: string | null;
  expired: boolean;
  createdUtc: string;
}

export default function OrdersPage() {
  return (
    <Suspense fallback={null}>
      <Orders />
    </Suspense>
  );
}

function Orders() {
  const params = useSearchParams();
  const [rows, setRows] = useState<OrderRow[] | null>(null);
  const [q, setQ] = useState("");
  const [open, setOpen] = useState(params.get("new") === "1");

  const load = useCallback(() => {
    const p = new URLSearchParams();
    if (q) p.set("q", q);
    fetch(`/api/admin/orders?${p}`)
      .then((r) => r.json())
      .then((d) => setRows(d.orders ?? []))
      .catch(() => setRows([]));
  }, [q]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div>
      <PageHeader
        title="Purchases"
        description="One serial number per sale. Buy 40 keys, get one serial with 40 keys under it."
      >
        <Button onClick={() => setOpen(true)}>
          <Plus /> New purchase
        </Button>
      </PageHeader>

      <NewPurchaseDialog open={open} onOpenChange={setOpen} onCreated={load} />

      <div className="relative mb-4 max-w-md">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          className="pl-9"
          placeholder="Search serial, customer, email, company…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
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
            <Empty onNew={() => setOpen(true)} searching={!!q} />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Serial</TableHead>
                  <TableHead>Customer</TableHead>
                  <TableHead className="hidden sm:table-cell">Edition</TableHead>
                  <TableHead className="text-right">Keys</TableHead>
                  <TableHead className="hidden text-right md:table-cell">In use</TableHead>
                  <TableHead className="hidden md:table-cell">Expiry</TableHead>
                  <TableHead className="hidden text-right lg:table-cell">Sold</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((o) => (
                  <TableRow key={o.id}>
                    <TableCell className="whitespace-nowrap font-mono font-medium">
                      <Link href={`/admin/orders/${o.serial}`} className="text-primary hover:underline">
                        {o.serial}
                      </Link>
                    </TableCell>
                    <TableCell>
                      <div className="font-medium">{o.customerName}</div>
                      {(o.company || o.customerPhoneDisplay || o.customerEmail) && (
                        <div className="truncate text-xs text-muted-foreground">
                          {o.company || o.customerPhoneDisplay || o.customerEmail}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="hidden sm:table-cell">
                      <EditionBadge edition={o.edition} />
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {o.issued}
                      {o.revoked > 0 && (
                        <span className="ml-1 text-xs text-destructive">({o.revoked} revoked)</span>
                      )}
                    </TableCell>
                    <TableCell className="hidden text-right tabular-nums md:table-cell">{o.activated}</TableCell>
                    <TableCell className="hidden whitespace-nowrap md:table-cell">
                      {o.expiryUtc ? (
                        <span className="text-muted-foreground">{fmtDate(o.expiryUtc)}</span>
                      ) : (
                        <Badge variant="outline">Perpetual</Badge>
                      )}
                      {o.expired && (
                        <Badge variant="warning" className="ml-2">
                          Expired
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="hidden whitespace-nowrap text-right text-muted-foreground lg:table-cell">
                      {fmtDate(o.createdUtc)}
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

function Empty({ onNew, searching }: { onNew: () => void; searching: boolean }) {
  return (
    <div className="flex flex-col items-center px-6 py-16 text-center">
      <div className="mb-4 flex h-12 w-12 items-center justify-center rounded-full bg-primary/10 text-primary">
        <ShoppingBag className="h-5 w-5" />
      </div>
      <h3 className="text-base font-semibold">{searching ? "Nothing matches" : "No purchases yet"}</h3>
      <p className="mt-1 max-w-sm text-sm text-muted-foreground">
        {searching
          ? "Try a serial number, customer name, email or company."
          : "Record your first sale and its keys will be issued under one serial number."}
      </p>
      {!searching && (
        <Button className="mt-6" onClick={onNew}>
          <Plus /> New purchase
        </Button>
      )}
    </div>
  );
}

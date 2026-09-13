"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ShoppingBag, KeyRound, CheckCircle2, Clock, Ban, MonitorSmartphone, ArrowRight, Plus } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { EditionBadge } from "@/components/status-badge";
import { fmtRelative } from "@/lib/utils";

interface Stats {
  orders: number;
  total: number;
  active: number;
  revoked: number;
  expiringSoon: number;
  activations: number;
  recent: Array<{ id: string; prefix: string; edition: string; lastSeenUtc: string }>;
}

const STATS: Array<{ key: keyof Stats; label: string; icon: React.ElementType; tone: string; href?: string }> = [
  { key: "orders", label: "Purchases", icon: ShoppingBag, tone: "text-primary bg-primary/10", href: "/admin/orders" },
  { key: "total", label: "Keys issued", icon: KeyRound, tone: "text-foreground bg-muted", href: "/admin/keys" },
  { key: "active", label: "Active", icon: CheckCircle2, tone: "text-success bg-success/10", href: "/admin/keys?status=active" },
  { key: "expiringSoon", label: "Expiring in 7 days", icon: Clock, tone: "text-warning bg-warning/10", href: "/admin/keys?status=expired" },
  { key: "revoked", label: "Revoked", icon: Ban, tone: "text-destructive bg-destructive/10", href: "/admin/keys?status=revoked" },
  { key: "activations", label: "Devices online", icon: MonitorSmartphone, tone: "text-primary bg-primary/10" },
];

export default function Dashboard() {
  const [s, setS] = useState<Stats | null>(null);

  useEffect(() => {
    fetch("/api/admin/stats")
      .then((r) => r.json())
      .then(setS)
      .catch(() => {});
  }, []);

  return (
    <div>
      <PageHeader title="Dashboard" description="What's happening across every licence you've issued.">
        <Button asChild>
          <Link href="/admin/orders?new=1">
            <Plus /> New purchase
          </Link>
        </Button>
      </PageHeader>

      <div className="grid grid-cols-2 gap-4 md:grid-cols-3 xl:grid-cols-6">
        {STATS.map(({ key, label, icon: Icon, tone, href }) => {
          const value = s ? (s[key] as number) : null;
          const body = (
            <Card className="h-full transition-shadow hover:shadow-md">
              <CardContent className="p-5">
                <div className={`mb-4 inline-flex h-9 w-9 items-center justify-center rounded-lg ${tone}`}>
                  <Icon className="h-4 w-4" />
                </div>
                {value === null ? (
                  <Skeleton className="h-8 w-16" />
                ) : (
                  <div className="text-3xl font-bold tracking-tight">{value.toLocaleString()}</div>
                )}
                <div className="mt-1 text-xs font-medium text-muted-foreground">{label}</div>
              </CardContent>
            </Card>
          );
          return href ? (
            <Link key={key} href={href} className="block">
              {body}
            </Link>
          ) : (
            <div key={key}>{body}</div>
          );
        })}
      </div>

      <Card className="mt-6">
        <CardHeader className="flex flex-row items-center justify-between space-y-0">
          <div>
            <CardTitle>Recently active keys</CardTitle>
            <CardDescription>Devices that checked in most recently.</CardDescription>
          </div>
          <Button variant="ghost" size="sm" asChild>
            <Link href="/admin/keys">
              All keys <ArrowRight />
            </Link>
          </Button>
        </CardHeader>
        <CardContent className="p-0">
          {!s ? (
            <div className="space-y-3 p-6">
              <Skeleton className="h-5 w-full" />
              <Skeleton className="h-5 w-4/5" />
              <Skeleton className="h-5 w-3/5" />
            </div>
          ) : s.recent.length === 0 ? (
            <div className="px-6 pb-8 pt-2 text-center text-sm text-muted-foreground">
              No device has activated yet. Once a customer signs in, they'll show up here.
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Key</TableHead>
                  <TableHead>Edition</TableHead>
                  <TableHead className="text-right">Last seen</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {s.recent.map((k) => (
                  <TableRow key={k.id}>
                    <TableCell className="font-mono">
                      <Link href={`/admin/keys/${k.id}`} className="text-primary hover:underline">
                        {k.prefix}…
                      </Link>
                    </TableCell>
                    <TableCell>
                      <EditionBadge edition={k.edition} />
                    </TableCell>
                    <TableCell className="text-right text-muted-foreground">{fmtRelative(k.lastSeenUtc)}</TableCell>
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

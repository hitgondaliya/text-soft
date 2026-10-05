import * as React from "react";
import Link from "next/link";
import { cn } from "@/lib/utils";
import { Skeleton } from "@/components/ui/skeleton";

/**
 * StatStrip — one card split into equal stat cells with hairline dividers.
 *
 *   <StatStrip items={[
 *     { label: "Purchases", value: 12, icon: ShoppingBag, tone: "primary", href: "/admin/orders" },
 *     { label: "Active keys", value: null, icon: CheckCircle2, tone: "success" },   // null = skeleton
 *   ]} />
 *
 * StatItem:
 *   label   string                      short label (truncates; keep it to ~12 characters)
 *   value   number | string | null      number is formatted (en-IN); null renders a skeleton
 *   icon    lucide icon component       shown in a small tinted square
 *   tone    "primary" | "success" | "warning" | "danger" | "neutral"   (default "neutral")
 *   href?   string                      makes the whole cell a link (e.g. to a filtered list)
 *   hint?   ReactNode                   small muted line under the value ("of 40", "last 7 days")
 *   title?  string                      native tooltip for the cell (longer explanation)
 *
 * Columns: 2 on phones, 4 from sm, and every item in ONE row from lg (up to 8 items).
 * With 7–8 items the icons hide between lg and xl so labels never truncate on a
 * 1024–1279px screen; from xl (1280px, e.g. a 1366×768 laptop) icons are shown too.
 * Props: items, className?, size? ("default" | "sm" — sm uses a text-xl value for detail pages).
 */

export type StatTone = "primary" | "success" | "warning" | "danger" | "neutral";

export interface StatItem {
  label: string;
  value: number | string | null;
  icon?: React.ElementType;
  tone?: StatTone;
  href?: string;
  hint?: React.ReactNode;
  title?: string;
}

export const STAT_TONES: Record<StatTone, string> = {
  primary: "bg-primary/10 text-primary",
  success: "bg-success/10 text-success",
  warning: "bg-warning/10 text-warning",
  danger: "bg-destructive/10 text-destructive",
  neutral: "bg-muted text-muted-foreground",
};

// Static class strings so Tailwind's JIT sees them.
const COLS: Record<number, string> = {
  1: "grid-cols-1",
  2: "grid-cols-2",
  3: "grid-cols-1 sm:grid-cols-3",
  4: "grid-cols-2 sm:grid-cols-4",
  5: "grid-cols-2 sm:grid-cols-3 lg:grid-cols-5",
  6: "grid-cols-2 sm:grid-cols-3 lg:grid-cols-6",
  7: "grid-cols-2 sm:grid-cols-4 lg:grid-cols-7",
  8: "grid-cols-2 sm:grid-cols-4 lg:grid-cols-8",
};

function formatValue(v: number | string): string {
  return typeof v === "number" ? v.toLocaleString("en-IN") : v;
}

export function StatStrip({
  items,
  className,
  size = "default",
}: {
  items: StatItem[];
  className?: string;
  size?: "default" | "sm";
}) {
  const n = Math.max(1, Math.min(items.length, 8));
  const dense = n >= 7;
  return (
    <div className={cn("overflow-hidden rounded-lg border bg-card text-card-foreground", className)}>
      {/* -mr-px/-mb-px push the last column's and last row's divider under the clip,
          so only inner dividers show whatever the wrap. */}
      <div className={cn("-mb-px -mr-px grid", COLS[n])}>
        {items.slice(0, 8).map((item, i) => (
          <StatCell key={`${item.label}-${i}`} item={item} dense={dense} size={size} twoUp={n !== 1 && n !== 3} />
        ))}
      </div>
    </div>
  );
}

function StatCell({ item, dense, size, twoUp }: { item: StatItem; dense: boolean; size: "default" | "sm"; twoUp: boolean }) {
  const { label, value, icon: Icon, tone = "neutral", href, hint, title } = item;
  const inner = (
    <>
      <div className="flex min-w-0 items-center gap-1.5">
        {Icon && (
          <span
            className={cn(
              "flex h-5 w-5 shrink-0 items-center justify-center rounded",
              STAT_TONES[tone],
              dense && "lg:hidden xl:flex",
            )}
          >
            <Icon className="h-3 w-3" aria-hidden />
          </span>
        )}
        <span className="truncate whitespace-nowrap text-xs font-medium text-muted-foreground">{label}</span>
      </div>
      {value === null ? (
        <Skeleton className={cn("mt-0.5", size === "sm" ? "h-6 w-10" : "h-7 w-12")} />
      ) : (
        <div
          className={cn(
            "truncate font-semibold tabular-nums tracking-tight",
            // a touch smaller on phones, so 8 stats do not fill half the first screen
            size === "sm" ? "text-xl leading-7" : "text-xl leading-7 sm:text-2xl sm:leading-8",
            size === "default" && dense && "lg:text-xl lg:leading-8 xl:text-2xl",
          )}
        >
          {formatValue(value)}
        </div>
      )}
      {hint && <div className="-mt-0.5 truncate text-[11px] text-muted-foreground">{hint}</div>}
    </>
  );
  const cell = cn(
    "relative flex min-w-0 flex-col gap-1 px-3 py-2 shadow-[inset_-1px_-1px_0_0_hsl(var(--border))] sm:px-4 sm:py-2.5",
    dense && "lg:px-2.5 xl:px-3 2xl:px-4",
    // two-up phone grid with an odd count: the last cell fills its row instead of leaving a hole
    twoUp && "[&:last-child:nth-child(odd)]:col-span-2 sm:[&:last-child:nth-child(odd)]:col-span-1",
  );
  if (href) {
    return (
      <Link
        href={href}
        title={title ?? label}
        className={cn(
          cell,
          "group transition-colors hover:bg-muted/40 focus-visible:bg-muted/60 focus-visible:outline-none",
        )}
      >
        {inner}
      </Link>
    );
  }
  return (
    <div className={cell} title={title}>
      {inner}
    </div>
  );
}

import * as React from "react";
import { cn } from "@/lib/utils";
import { CopyButton } from "@/components/copy-button";

/**
 * KvList — compact label/value rows for detail panels.
 *
 *   <KvList items={[
 *     { label: "Serial", value: order.serial, mono: true, copy: order.serial },
 *     { label: "Customer", value: order.customerName },
 *     { label: "Notes", value: order.notes, hidden: !order.notes },
 *   ]} />
 *
 * KvItem:
 *   label    ReactNode
 *   value    ReactNode         null / undefined / "" render a muted "—"
 *   mono?    boolean           monospace value (serials, keys, IPs, fingerprints)
 *   copy?    string            adds a small copy button after the value
 *   hidden?  boolean           skip the row
 *   key?     string            React key (defaults to the label when it is a string)
 *
 * Props: items, className?, labelWidth? (CSS width of the label column, default "8.5rem"),
 *        divided? (hairline between rows, default true)
 */
export interface KvItem {
  label: React.ReactNode;
  value: React.ReactNode;
  mono?: boolean;
  copy?: string;
  hidden?: boolean;
  key?: string;
}

function isEmpty(v: React.ReactNode): boolean {
  return v === null || v === undefined || v === "" || v === false;
}

export function KvList({
  items,
  className,
  labelWidth = "8.5rem",
  divided = true,
}: {
  items: KvItem[];
  className?: string;
  labelWidth?: string;
  divided?: boolean;
}) {
  const rows = items.filter((i) => !i.hidden);
  return (
    <dl className={cn("text-[13px]", divided && "divide-y", className)}>
      {rows.map((item, i) => (
        <div
          key={item.key ?? (typeof item.label === "string" ? item.label : i)}
          className="grid items-baseline gap-x-3 gap-y-0.5 py-2 first:pt-0 last:pb-0 sm:grid-cols-[var(--kv-label)_minmax(0,1fr)]"
          style={{ ["--kv-label" as string]: labelWidth } as React.CSSProperties}
        >
          <dt className="text-xs text-muted-foreground">{item.label}</dt>
          <dd className={cn("flex min-w-0 items-center gap-1 break-words", item.mono && "font-mono text-xs")}>
            <span className="min-w-0">{isEmpty(item.value) ? <span className="text-muted-foreground">—</span> : item.value}</span>
            {item.copy && <CopyButton value={item.copy} size="icon-xs" className="-my-1 shrink-0 text-muted-foreground" />}
          </dd>
        </div>
      ))}
    </dl>
  );
}

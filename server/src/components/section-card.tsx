import * as React from "react";
import { cn } from "@/lib/utils";

/**
 * SectionCard — the standard titled card: compact header bar + body.
 *
 *   <SectionCard title="Keys" description="40 keys under this purchase" actions={<Button size="sm">…</Button>} flush>
 *     <Table>…</Table>
 *   </SectionCard>
 *
 * Props:
 *   title?          ReactNode   text-sm semibold; the header is omitted when there is no title and no actions
 *   description?    ReactNode   one muted text-xs line under the title
 *   icon?           ElementType optional lucide icon before the title (muted unless iconClassName says otherwise)
 *   iconClassName?  string      e.g. "text-destructive"
 *   actions?        ReactNode   right side of the header (buttons, a select, a link)
 *   footer?         ReactNode   bottom bar with a top border (px-4 py-2.5)
 *   flush?          boolean     no body padding — for tables and lists that run edge to edge
 *   className?      string      on the outer <section>
 *   bodyClassName?  string      on the body <div>
 *   headerClassName? string     on the header
 *   id?             string
 */
export function SectionCard({
  title,
  description,
  icon: Icon,
  iconClassName,
  actions,
  footer,
  children,
  flush = false,
  className,
  bodyClassName,
  headerClassName,
  id,
}: {
  title?: React.ReactNode;
  description?: React.ReactNode;
  icon?: React.ElementType;
  iconClassName?: string;
  actions?: React.ReactNode;
  footer?: React.ReactNode;
  children?: React.ReactNode;
  flush?: boolean;
  className?: string;
  bodyClassName?: string;
  headerClassName?: string;
  id?: string;
}) {
  const hasHeader = title != null || actions != null;
  return (
    <section id={id} className={cn("min-w-0 rounded-lg border bg-card text-card-foreground", className)}>
      {hasHeader && (
        <header className={cn("flex min-h-[48px] items-center justify-between gap-3 border-b px-4 py-2.5", headerClassName)}>
          <div className="min-w-0">
            {title != null && (
              <h2 className="flex items-center gap-2 text-sm font-semibold leading-5 tracking-tight">
                {Icon && <Icon className={cn("h-4 w-4 shrink-0 text-muted-foreground", iconClassName)} aria-hidden />}
                <span className="min-w-0 truncate">{title}</span>
              </h2>
            )}
            {description != null && <div className="mt-0.5 text-xs leading-4 text-muted-foreground">{description}</div>}
          </div>
          {actions != null && <div className="flex shrink-0 flex-wrap items-center justify-end gap-1.5">{actions}</div>}
        </header>
      )}
      <div className={cn(!flush && "p-4", bodyClassName)}>{children}</div>
      {footer != null && <div className="flex items-center gap-2 border-t px-4 py-2.5 text-xs text-muted-foreground">{footer}</div>}
    </section>
  );
}

import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { ThemeToggle } from "@/components/theme-toggle";
import { cn } from "@/lib/utils";

/**
 * PageHeader — compact page title row.
 *
 *   <PageHeader title="Purchases" description="Every sale and its keys."
 *               back={{ href: "/admin/orders", label: "Purchases" }}
 *               actions={<Button>New purchase</Button>} />
 *
 * Props:
 *   title         ReactNode   text-xl semibold (badges may sit inside it)
 *   description?  ReactNode   one muted line under the title
 *   back?         { href, label }   small "← label" link above the title (detail pages)
 *   actions?      ReactNode   right side; `children` is accepted too and rendered after actions
 *   meta?         ReactNode   extra row under the description (chips, small stats)
 *   themeToggle?  boolean     show the theme toggle at the right on desktop (default true)
 *   className?    string
 */
export function PageHeader({
  title,
  description,
  back,
  actions,
  meta,
  children,
  themeToggle = true,
  className,
}: {
  title: React.ReactNode;
  description?: React.ReactNode;
  back?: { href: string; label: React.ReactNode };
  actions?: React.ReactNode;
  meta?: React.ReactNode;
  children?: React.ReactNode;
  themeToggle?: boolean;
  className?: string;
}) {
  return (
    <div className={cn("mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between", className)}>
      <div className="min-w-0">
        {back && (
          <Link
            href={back.href}
            className="mb-1 inline-flex items-center gap-1 rounded text-xs font-medium text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <ArrowLeft className="h-3.5 w-3.5" aria-hidden />
            {back.label}
          </Link>
        )}
        <h1 className="text-xl font-semibold leading-7 tracking-tight">{title}</h1>
        {description && <div className="mt-0.5 text-[13px] text-muted-foreground">{description}</div>}
        {meta && <div className="mt-2 flex flex-wrap items-center gap-2">{meta}</div>}
      </div>
      {(actions || children || themeToggle) && (
        <div className={cn("flex shrink-0 flex-wrap items-center gap-2", !actions && !children && "hidden lg:flex")}>
          {actions}
          {children}
          {themeToggle && <ThemeToggle className="hidden lg:inline-flex" />}
        </div>
      )}
    </div>
  );
}

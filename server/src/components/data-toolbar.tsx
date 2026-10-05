"use client";

import * as React from "react";
import { Search, X } from "lucide-react";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

/**
 * DataToolbar — search box with an icon, optional filters beside it, actions on the right.
 * Stacks on phones, one row from sm.
 *
 *   <DataToolbar search={q} onSearchChange={setQ} placeholder="Search serial, customer…"
 *                filters={<Select …/>} actions={<Button size="sm">Export</Button>} />
 *
 * Props:
 *   search?          string                 controlled value; the search box shows when onSearchChange is set
 *   onSearchChange?  (value: string) => void
 *   placeholder?     string                 (default "Search…")
 *   filters?         ReactNode              left side, after the search box (selects, segmented tabs)
 *   actions?         ReactNode              right side
 *   children?        ReactNode              same as actions (appended after them)
 *   searchClassName? string                 width override for the search box (default sm:w-72 lg:w-80)
 *   className?       string
 *   autoFocus?       boolean
 */
export function DataToolbar({
  search = "",
  onSearchChange,
  placeholder = "Search…",
  filters,
  actions,
  children,
  searchClassName,
  className,
  autoFocus,
}: {
  search?: string;
  onSearchChange?: (value: string) => void;
  placeholder?: string;
  filters?: React.ReactNode;
  actions?: React.ReactNode;
  children?: React.ReactNode;
  searchClassName?: string;
  className?: string;
  autoFocus?: boolean;
}) {
  const right = actions != null || children != null;
  return (
    <div className={cn("flex flex-col gap-2 sm:flex-row sm:items-center", className)}>
      {onSearchChange && (
        <div className={cn("relative w-full sm:w-72 lg:w-80", searchClassName)}>
          <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            type="search"
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder={placeholder}
            aria-label={placeholder}
            autoFocus={autoFocus}
            className="pl-8 pr-8 [&::-webkit-search-cancel-button]:hidden"
          />
          {search && (
            <button
              type="button"
              onClick={() => onSearchChange("")}
              aria-label="Clear search"
              title="Clear search"
              className="absolute right-1.5 top-1/2 inline-flex h-6 w-6 -translate-y-1/2 items-center justify-center rounded text-muted-foreground hover:bg-muted hover:text-foreground"
            >
              <X className="h-3.5 w-3.5" />
            </button>
          )}
        </div>
      )}
      {filters != null && <div className="flex flex-wrap items-center gap-2">{filters}</div>}
      {right && (
        <div className="flex flex-wrap items-center gap-2 sm:ml-auto">
          {actions}
          {children}
        </div>
      )}
    </div>
  );
}

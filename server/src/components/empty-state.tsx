import * as React from "react";
import { Inbox } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * EmptyState — centred placeholder for an empty list or a failed load.
 *
 *   <EmptyState icon={KeyRound} title="No keys yet" description="Create a purchase to issue keys."
 *               action={<Button size="sm">New purchase</Button>} />
 *
 * Props:
 *   title         ReactNode
 *   description?  ReactNode
 *   icon?         ElementType  (default Inbox)
 *   tone?         "neutral" | "primary" | "success" | "danger"   icon tint (default "neutral")
 *   action?       ReactNode    buttons under the text
 *   compact?      boolean      py-6 instead of py-10 (inside small cards)
 *   className?    string
 */
const TONES = {
  neutral: "bg-muted text-muted-foreground",
  primary: "bg-primary/10 text-primary",
  success: "bg-success/10 text-success",
  danger: "bg-destructive/10 text-destructive",
} as const;

export function EmptyState({
  title,
  description,
  icon: Icon = Inbox,
  tone = "neutral",
  action,
  compact = false,
  className,
}: {
  title: React.ReactNode;
  description?: React.ReactNode;
  icon?: React.ElementType;
  tone?: keyof typeof TONES;
  action?: React.ReactNode;
  compact?: boolean;
  className?: string;
}) {
  return (
    <div className={cn("flex flex-col items-center px-4 text-center", compact ? "py-6" : "py-10", className)}>
      <div className={cn("mb-2.5 flex h-9 w-9 items-center justify-center rounded-full", TONES[tone])}>
        <Icon className="h-4 w-4" aria-hidden />
      </div>
      <div className="text-sm font-medium">{title}</div>
      {description && <div className="mt-1 max-w-sm text-xs text-muted-foreground">{description}</div>}
      {action && <div className="mt-3 flex flex-wrap items-center justify-center gap-2">{action}</div>}
    </div>
  );
}

import * as React from "react";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

// Small, square-ish status chip (h-5, 11px). Tinted variants carry a faint
// border so they stay readable on both card and muted backgrounds.
// variant: default | secondary | outline | success | warning | destructive | brand
const badgeVariants = cva(
  "inline-flex h-5 shrink-0 items-center gap-1 whitespace-nowrap rounded-md border px-1.5 text-[11px] font-medium leading-none transition-colors focus:outline-none [&_svg]:size-3 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        default: "border-transparent bg-primary text-primary-foreground",
        secondary: "border-transparent bg-muted text-muted-foreground",
        outline: "border-border text-foreground",
        success: "border-success/20 bg-success/10 text-success",
        warning: "border-warning/25 bg-warning/10 text-warning",
        destructive: "border-destructive/20 bg-destructive/10 text-destructive",
        brand: "border-primary/20 bg-primary/10 text-primary-strong",
      },
    },
    defaultVariants: { variant: "default" },
  },
);

export interface BadgeProps extends React.HTMLAttributes<HTMLDivElement>, VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return <div className={cn(badgeVariants({ variant }), className)} {...props} />;
}

export { Badge, badgeVariants };

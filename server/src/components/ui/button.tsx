import * as React from "react";
import { Slot } from "@radix-ui/react-slot";
import { cva, type VariantProps } from "class-variance-authority";
import { cn } from "@/lib/utils";

// Compact by default: h-9 for page actions, h-8 for toolbar/table actions,
// h-7 for in-row actions. Icon-only buttons need aria-label + title.
//
// variant: default | destructive | outline | secondary | ghost | link | soft (orange tint)
// size:    default (h-9) | sm (h-8) | xs (h-7) | lg (h-10) | icon (8x8) | icon-sm (7x7) | icon-xs (6x6)
const buttonVariants = cva(
  "inline-flex items-center justify-center gap-1.5 whitespace-nowrap rounded-md text-[13px] font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1 ring-offset-background disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        // hover goes to primary-strong (darker in light, brighter in dark) so the text stays AA
        default: "bg-primary text-primary-foreground shadow-sm hover:bg-primary-strong",
        destructive: "bg-destructive text-destructive-foreground shadow-sm hover:bg-destructive/90",
        outline: "border border-input bg-card text-foreground hover:bg-muted",
        secondary: "bg-secondary text-secondary-foreground hover:bg-secondary/70",
        ghost: "text-foreground hover:bg-muted",
        link: "text-primary-strong underline-offset-4 hover:underline",
        soft: "bg-primary/10 text-primary-strong hover:bg-primary/15",
      },
      size: {
        default: "h-9 px-3.5",
        sm: "h-8 px-3 text-xs",
        xs: "h-7 gap-1 rounded px-2 text-xs [&_svg]:size-3.5",
        lg: "h-10 px-5 text-sm",
        icon: "h-8 w-8",
        "icon-sm": "h-7 w-7 [&_svg]:size-3.5",
        "icon-xs": "h-6 w-6 rounded [&_svg]:size-3.5",
      },
    },
    defaultVariants: { variant: "default", size: "default" },
  },
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : "button";
    return <Comp className={cn(buttonVariants({ variant, size, className }))} ref={ref} {...props} />;
  },
);
Button.displayName = "Button";

export { Button, buttonVariants };

import * as React from "react";
import { Info, CircleCheck, TriangleAlert, CircleAlert } from "lucide-react";
import { cn } from "@/lib/utils";

// Inline notice box.
//   <Alert tone="warning" title="Using the .env values">Save to override them.</Alert>
// tone: "info" (default) | "success" | "warning" | "danger"
// icon: override the tone's icon (pass null for none). action: right-aligned slot.

type AlertTone = "info" | "success" | "warning" | "danger";

const TONES: Record<AlertTone, { box: string; icon: string; Icon: React.ElementType }> = {
  info: { box: "border-border bg-muted/50", icon: "text-muted-foreground", Icon: Info },
  success: { box: "border-success/25 bg-success/5", icon: "text-success", Icon: CircleCheck },
  warning: { box: "border-warning/30 bg-warning/5", icon: "text-warning", Icon: TriangleAlert },
  danger: { box: "border-destructive/25 bg-destructive/5", icon: "text-destructive", Icon: CircleAlert },
};

export interface AlertProps extends Omit<React.HTMLAttributes<HTMLDivElement>, "title"> {
  tone?: AlertTone;
  title?: React.ReactNode;
  icon?: React.ElementType | null;
  action?: React.ReactNode;
}

function Alert({ tone = "info", title, icon, action, className, children, ...props }: AlertProps) {
  const t = TONES[tone];
  const Icon = icon === null ? null : (icon ?? t.Icon);
  return (
    <div
      role={tone === "danger" ? "alert" : "status"}
      className={cn("flex items-start gap-2.5 rounded-md border px-3 py-2.5 text-[13px]", t.box, className)}
      {...props}
    >
      {Icon && <Icon className={cn("mt-0.5 h-4 w-4 shrink-0", t.icon)} />}
      <div className="min-w-0 flex-1">
        {title && <div className="font-medium leading-5">{title}</div>}
        {children && <div className={cn("text-muted-foreground", title && "mt-0.5")}>{children}</div>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}

export { Alert };
export type { AlertTone };

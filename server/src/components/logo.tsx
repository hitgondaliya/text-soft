import Image from "next/image";
import { cn } from "@/lib/utils";

/**
 * The TextSoft mark with the wordmark. `size` is the mark's pixel size.
 * `tone="sidebar"` renders the wordmark for the dark sidebar; `caption` is the small
 * uppercase line under it (default "Licences").
 */
export function Logo({
  size = 36,
  wordmark = true,
  tone = "default",
  caption = "Licences",
  className,
}: {
  size?: number;
  wordmark?: boolean;
  tone?: "default" | "sidebar";
  caption?: string | null;
  className?: string;
}) {
  return (
    <div className={cn("flex items-center gap-2.5", className)}>
      <Image src="/logo.webp" alt="TextSoft" width={size} height={size} priority className="shrink-0" />
      {wordmark && (
        <div className="min-w-0 leading-tight">
          <div className={cn("text-[15px] font-bold tracking-tight", tone === "sidebar" && "text-white")}>
            Text<span className={tone === "sidebar" ? "text-sidebar-primary" : "text-primary"}>Soft</span>
          </div>
          {caption && (
            <div
              className={cn(
                "text-[10px] font-medium uppercase tracking-[0.16em]",
                tone === "sidebar" ? "text-sidebar-foreground/60" : "text-muted-foreground",
              )}
            >
              {caption}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

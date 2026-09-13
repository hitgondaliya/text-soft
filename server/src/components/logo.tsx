import Image from "next/image";
import { cn } from "@/lib/utils";

/** The TextSoft mark with the wordmark. `size` is the mark's pixel size. */
export function Logo({ size = 36, wordmark = true, className }: { size?: number; wordmark?: boolean; className?: string }) {
  return (
    <div className={cn("flex items-center gap-3", className)}>
      <Image src="/logo.webp" alt="TextSoft" width={size} height={size} priority className="shrink-0" />
      {wordmark && (
        <div className="leading-tight">
          <div className="text-[17px] font-bold tracking-tight">
            Text<span className="text-primary">Soft</span>
          </div>
          <div className="text-[11px] font-medium uppercase tracking-[0.18em] text-muted-foreground">Licenses</div>
        </div>
      )}
    </div>
  );
}

import { cn } from "@/lib/utils";
import { findCountry } from "@/lib/countries";

/**
 * A country flag from the flag-icons sprite (SVG, so it renders on Windows too,
 * where emoji flags show up as two letters). Falls back to the ISO code.
 */
export function Flag({ code, className, title }: { code: string | undefined | null; className?: string; title?: string }) {
  const c = findCountry(code);
  if (!c) return <span className={cn("inline-block h-[0.9em] w-[1.2em] rounded-sm bg-muted align-[-0.1em]", className)} />;
  return (
    <span
      className={cn(`fi fi-${c.code.toLowerCase()} rounded-[3px] shadow-[0_0_0_1px_rgba(0,0,0,0.08)]`, className)}
      title={title ?? c.name}
      role="img"
      aria-label={c.name}
    />
  );
}

/** "🇮🇳 +91 98250 00000" — flag plus the formatted number, or a dash. */
export function PhoneDisplay({ country, text, className }: { country?: string | null; text?: string | null; className?: string }) {
  if (!text) return <span className="text-muted-foreground">—</span>;
  return (
    <span className={cn("inline-flex items-center gap-1.5 whitespace-nowrap tabular-nums", className)}>
      <Flag code={country} className="shrink-0" />
      {text}
    </span>
  );
}

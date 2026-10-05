"use client";

import { useMemo, useState } from "react";
import { Check, ChevronDown, Search } from "lucide-react";
import { COUNTRIES, DEFAULT_COUNTRY, findCountry } from "@/lib/countries";
import { cn } from "@/lib/utils";
import { Flag } from "@/components/flag";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";

export interface PhoneValue {
  country: string; // ISO alpha-2
  number: string; // national digits as typed
}

/**
 * Country-code picker + number input, one h-9 control that matches <Input>.
 * Every country with its flag and dialling code; India first and selected by
 * default. The picker filters as you type a country name, code or dial prefix,
 * and Enter picks the first match.
 */
export function PhoneField({
  value,
  onChange,
  id,
  invalid,
  placeholder = "98250 00000",
  autoFocus,
}: {
  value: PhoneValue;
  onChange: (v: PhoneValue) => void;
  id?: string;
  invalid?: boolean;
  placeholder?: string;
  autoFocus?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [q, setQ] = useState("");
  const country = findCountry(value.country) ?? findCountry(DEFAULT_COUNTRY)!;

  const list = useMemo(() => {
    const needle = q.trim().toLowerCase().replace(/^\+/, "");
    const pinned = COUNTRIES.filter((c) => c.code === DEFAULT_COUNTRY);
    const rest = COUNTRIES.filter((c) => c.code !== DEFAULT_COUNTRY);
    const all = [...pinned, ...rest];
    if (!needle) return all;
    return all.filter(
      (c) => c.name.toLowerCase().includes(needle) || c.code.toLowerCase() === needle || c.dial.startsWith(needle),
    );
  }, [q]);

  function pick(code: string) {
    onChange({ ...value, country: code });
    setOpen(false);
    setQ("");
  }

  return (
    <div
      className={cn(
        "group flex rounded-md transition-[box-shadow] focus-within:ring-[3px]",
        invalid ? "focus-within:ring-destructive/20" : "focus-within:ring-ring/20",
      )}
    >
      <Popover
        open={open}
        onOpenChange={(v) => {
          setOpen(v);
          if (!v) setQ("");
        }}
      >
        <PopoverTrigger asChild>
          <button
            type="button"
            aria-label={`Country code: ${country.name} +${country.dial}`}
            title={`${country.name} +${country.dial}`}
            className={cn(
              "flex h-9 shrink-0 items-center gap-1.5 rounded-l-md border border-r-0 bg-muted/40 px-2.5 text-[13px] transition-colors hover:bg-muted focus-visible:outline-none data-[state=open]:bg-muted",
              invalid ? "border-destructive" : "border-input",
            )}
          >
            <Flag code={country.code} />
            <span className="tabular-nums">+{country.dial}</span>
            <ChevronDown className="h-3.5 w-3.5 opacity-50" aria-hidden />
          </button>
        </PopoverTrigger>
        <PopoverContent className="w-72 p-0" align="start">
          <div className="relative border-b p-1.5">
            <Search className="pointer-events-none absolute left-3.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input
              autoFocus
              className="h-8 pl-7"
              placeholder="Search country or code"
              aria-label="Search country or code"
              value={q}
              onChange={(e) => setQ(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && list.length > 0) {
                  e.preventDefault();
                  pick(list[0].code);
                }
              }}
            />
          </div>
          <ul className="max-h-64 overflow-y-auto p-1" role="listbox" aria-label="Countries">
            {list.length === 0 && <li className="px-3 py-5 text-center text-xs text-muted-foreground">No country matches</li>}
            {list.map((c) => {
              const selected = c.code === country.code;
              return (
                <li key={c.code}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={selected}
                    onClick={() => pick(c.code)}
                    className={cn(
                      "flex h-8 w-full items-center gap-2.5 rounded px-2 text-left text-[13px] hover:bg-muted focus-visible:bg-muted focus-visible:outline-none",
                      selected && "bg-primary/10 text-foreground",
                    )}
                  >
                    <Flag code={c.code} />
                    <span className="min-w-0 flex-1 truncate">{c.name}</span>
                    <span className="tabular-nums text-xs text-muted-foreground">+{c.dial}</span>
                    <Check className={cn("h-3.5 w-3.5 text-primary", !selected && "invisible")} aria-hidden />
                  </button>
                </li>
              );
            })}
          </ul>
        </PopoverContent>
      </Popover>
      <Input
        id={id}
        type="tel"
        inputMode="numeric"
        autoComplete="tel-national"
        autoFocus={autoFocus}
        aria-invalid={invalid || undefined}
        className={cn(
          "rounded-l-none tabular-nums focus-visible:ring-0",
          invalid && "border-destructive focus-visible:border-destructive",
        )}
        placeholder={placeholder}
        value={value.number}
        onChange={(e) => onChange({ ...value, number: e.target.value.replace(/[^\d\s-]/g, "") })}
      />
    </div>
  );
}

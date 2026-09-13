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
 * Country-code picker + number input. Every country with its flag and dialling
 * code; India first and selected by default. The picker filters as you type a
 * country name, code or dial prefix.
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

  return (
    <div className={cn("flex rounded-md shadow-sm", invalid && "ring-1 ring-destructive rounded-md")}>
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
            aria-label={`Country code ${country.name} +${country.dial}`}
            className="flex h-9 shrink-0 items-center gap-2 rounded-l-md border border-r-0 border-input bg-muted/50 px-2.5 text-sm hover:bg-muted focus:outline-none focus:ring-2 focus:ring-ring"
          >
            <Flag code={country.code} className="text-base" />
            <span className="tabular-nums">+{country.dial}</span>
            <ChevronDown className="h-3.5 w-3.5 opacity-60" />
          </button>
        </PopoverTrigger>
        <PopoverContent className="w-80 p-0" align="start">
          <div className="relative border-b p-2">
            <Search className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              autoFocus
              className="h-8 pl-8"
              placeholder="Search country or code"
              value={q}
              onChange={(e) => setQ(e.target.value)}
            />
          </div>
          <ul className="max-h-72 overflow-y-auto p-1" role="listbox">
            {list.length === 0 && <li className="px-3 py-6 text-center text-sm text-muted-foreground">No country matches</li>}
            {list.map((c) => {
              const selected = c.code === country.code;
              return (
                <li key={c.code}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={selected}
                    onClick={() => {
                      onChange({ ...value, country: c.code });
                      setOpen(false);
                      setQ("");
                    }}
                    className={cn(
                      "flex w-full items-center gap-3 rounded-sm px-2 py-1.5 text-left text-sm hover:bg-accent",
                      selected && "bg-accent/60",
                    )}
                  >
                    <Flag code={c.code} className="text-base" />
                    <span className="min-w-0 flex-1 truncate">{c.name}</span>
                    <span className="tabular-nums text-muted-foreground">+{c.dial}</span>
                    {selected && <Check className="h-4 w-4 text-primary" />}
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
        className={cn("rounded-l-none shadow-none", invalid && "border-destructive")}
        placeholder={placeholder}
        value={value.number}
        onChange={(e) => onChange({ ...value, number: e.target.value.replace(/[^\d\s-]/g, "") })}
      />
    </div>
  );
}

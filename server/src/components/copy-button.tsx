"use client";

import * as React from "react";
import { Check, Copy, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { Button, type ButtonProps } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/**
 * CopyButton — copies text, shows a check for 1.5 s and toasts "Copied".
 *
 *   <CopyButton value="TXS-2026-0001" />                         // icon-only (aria-label "Copy")
 *   <CopyButton value={() => revealKeyById(id)} label="Copy key" />  // fetches first, then copies
 *
 * Props:
 *   value        string | (() => Promise<string>)   text, or an async fetcher (throw an Error to show its message)
 *   label?       string        visible text; without it the button is icon-only
 *   ariaLabel?   string        accessible name + title for the icon-only button (default "Copy")
 *   size?        Button size   default "icon-sm" when icon-only, "sm" with a label
 *   variant?     Button variant (default "ghost")
 *   toastText?   string        success toast (default "Copied")
 *   disabled?, className?
 *   onCopied?    (text) => void
 *
 * If the clipboard is blocked (insecure origin, permissions) it falls back to a
 * hidden textarea + execCommand, and failing that shows the text in a toast so it
 * can be selected by hand.
 */

export async function copyText(text: string): Promise<boolean> {
  try {
    if (typeof navigator !== "undefined" && navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch {
    /* fall through to the legacy path */
  }
  try {
    const ta = document.createElement("textarea");
    ta.value = text;
    ta.setAttribute("readonly", "");
    ta.style.position = "fixed";
    ta.style.top = "-1000px";
    ta.style.opacity = "0";
    document.body.appendChild(ta);
    ta.select();
    const ok = document.execCommand("copy");
    document.body.removeChild(ta);
    return ok;
  } catch {
    return false;
  }
}

export interface CopyButtonProps {
  value: string | (() => Promise<string>);
  label?: string;
  ariaLabel?: string;
  size?: ButtonProps["size"];
  variant?: ButtonProps["variant"];
  toastText?: string;
  disabled?: boolean;
  className?: string;
  onCopied?: (text: string) => void;
}

export function CopyButton({
  value,
  label,
  ariaLabel,
  size,
  variant = "ghost",
  toastText = "Copied",
  disabled,
  className,
  onCopied,
}: CopyButtonProps) {
  const [state, setState] = React.useState<"idle" | "busy" | "done">("idle");
  const timer = React.useRef<ReturnType<typeof setTimeout> | null>(null);

  React.useEffect(() => () => {
    if (timer.current) clearTimeout(timer.current);
  }, []);

  async function onClick(e: React.MouseEvent) {
    e.preventDefault();
    e.stopPropagation();
    if (state === "busy") return;
    let text: string;
    try {
      if (typeof value === "function") {
        setState("busy");
        text = await value();
      } else {
        text = value;
      }
    } catch (err) {
      setState("idle");
      toast.error(err instanceof Error && err.message ? err.message : "Could not fetch that value.");
      return;
    }
    const ok = await copyText(text);
    if (ok) {
      toast.success(toastText);
      onCopied?.(text);
      setState("done");
      if (timer.current) clearTimeout(timer.current);
      timer.current = setTimeout(() => setState("idle"), 1500);
    } else {
      setState("idle");
      toast("Copy it by hand", {
        description: <span className="select-all break-all font-mono text-xs">{text}</span>,
        duration: 15000,
      });
    }
  }

  const name = ariaLabel ?? "Copy";
  const Icon = state === "busy" ? Loader2 : state === "done" ? Check : Copy;
  return (
    <Button
      type="button"
      variant={variant}
      size={size ?? (label ? "sm" : "icon-sm")}
      disabled={disabled}
      onClick={onClick}
      aria-label={label ? undefined : name}
      title={label ? undefined : name}
      // The green "copied" tint is for transparent buttons only, and wins over a caller's
      // muted text colour. Filled buttons keep their foreground and show just the check.
      className={cn(className, state === "done" && (variant === "ghost" || variant === "outline") && "text-success hover:text-success")}
    >
      <Icon className={cn(state === "busy" && "animate-spin")} />
      {label}
    </Button>
  );
}

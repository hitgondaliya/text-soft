"use client";

import { Toaster as Sonner } from "sonner";
import { useTheme } from "next-themes";

/** Sonner toaster that follows the light/dark theme. Mounted once in the root layout. */
export function Toaster() {
  const { resolvedTheme } = useTheme();
  return (
    <Sonner
      theme={resolvedTheme === "dark" ? "dark" : "light"}
      richColors
      closeButton
      position="bottom-right"
      toastOptions={{ classNames: { toast: "text-[13px]", description: "text-xs" } }}
    />
  );
}

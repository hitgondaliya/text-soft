"use client";

import { Suspense, useState } from "react";
import Image from "next/image";
import { useRouter, useSearchParams } from "next/navigation";
import { KeyRound, ShieldCheck, Users, ArrowRight, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { ThemeToggle } from "@/components/theme-toggle";

export default function LoginPage() {
  return (
    <Suspense fallback={null}>
      <LoginForm />
    </Suspense>
  );
}

function LoginForm() {
  const router = useRouter();
  const params = useSearchParams();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError("");
    const res = await fetch("/api/login", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ email, password }),
    });
    setBusy(false);
    if (res.ok) {
      router.push(params.get("next") || "/admin");
      router.refresh();
    } else {
      setError("That email and password don't match.");
    }
  }

  return (
    <div className="grid min-h-screen lg:grid-cols-[1.1fr_1fr]">
      {/* Brand panel */}
      <div className="relative hidden overflow-hidden bg-sidebar text-sidebar-foreground lg:flex lg:flex-col lg:justify-between lg:p-12">
        <div className="brand-glow absolute inset-0" />
        <div className="relative flex items-center gap-3">
          <Image src="/logo.webp" alt="TextSoft" width={44} height={44} priority />
          <div className="leading-tight">
            <div className="text-xl font-bold tracking-tight text-white">
              Text<span className="text-primary">Soft</span>
            </div>
            <div className="text-[11px] font-medium uppercase tracking-[0.2em] text-sidebar-foreground/50">
              Beyond Typing
            </div>
          </div>
        </div>

        <div className="relative max-w-md">
          <h2 className="text-balance text-4xl font-bold leading-tight tracking-tight text-white">
            Every licence, every customer, one place.
          </h2>
          <p className="mt-4 text-balance text-base text-sidebar-foreground/70">
            Issue keys by purchase, watch activations, and renew or revoke a whole sale in one click.
          </p>
          <ul className="mt-8 space-y-3 text-sm text-sidebar-foreground/80">
            <li className="flex items-center gap-3">
              <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/15 text-primary">
                <ShieldCheck className="h-4 w-4" />
              </span>
              Ed25519-signed licences — the private key never leaves this server
            </li>
            <li className="flex items-center gap-3">
              <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/15 text-primary">
                <Users className="h-4 w-4" />
              </span>
              One serial number per sale, every key traceable to its buyer
            </li>
            <li className="flex items-center gap-3">
              <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/15 text-primary">
                <KeyRound className="h-4 w-4" />
              </span>
              Per-device limits, offline grace, instant revocation
            </li>
          </ul>
        </div>

        <div className="relative text-xs text-sidebar-foreground/40">
          © {new Date().getFullYear()} TextSoft · Licence administration
        </div>
      </div>

      {/* Form panel */}
      <div className="relative flex items-center justify-center px-6 py-12">
        <div className="absolute right-4 top-4">
          <ThemeToggle />
        </div>
        <div className="w-full max-w-sm animate-fade-up">
          <div className="mb-8 flex items-center gap-3 lg:hidden">
            <Image src="/logo.webp" alt="TextSoft" width={40} height={40} priority />
            <div className="text-xl font-bold tracking-tight">
              Text<span className="text-primary">Soft</span>
            </div>
          </div>

          <h1 className="text-2xl font-bold tracking-tight">Welcome back</h1>
          <p className="mt-1 text-sm text-muted-foreground">Sign in to the licence panel.</p>

          <form onSubmit={submit} className="mt-8 space-y-5">
            <div className="space-y-2">
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                autoComplete="username"
                placeholder="you@textsoft.in"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                autoFocus
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                placeholder="••••••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
            </div>
            {error && (
              <div className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {error}
              </div>
            )}
            <Button type="submit" size="lg" className="w-full" disabled={busy}>
              {busy ? <Loader2 className="animate-spin" /> : <ArrowRight />}
              {busy ? "Signing in…" : "Sign in"}
            </Button>
          </form>

          <p className="mt-8 text-center text-xs text-muted-foreground">
            Access is restricted to TextSoft staff. Every action here is logged.
          </p>
        </div>
      </div>
    </div>
  );
}

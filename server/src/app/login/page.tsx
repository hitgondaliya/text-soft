"use client";

import { Suspense, useState } from "react";
import Image from "next/image";
import { useRouter, useSearchParams } from "next/navigation";
import { ArrowRight, Eye, EyeOff, KeyRound, Loader2, MailCheck, MonitorSmartphone, ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert } from "@/components/ui/alert";
import { ThemeToggle } from "@/components/theme-toggle";

// Sign-in. Split layout: dark brand panel on lg+ screens, the form on the right.
// POST /api/login sets the session cookie; then we go to ?next= (same-site paths only).

export default function LoginPage() {
  return (
    <Suspense fallback={null}>
      <LoginForm />
    </Suspense>
  );
}

/** Only follow ?next= to a path on this site — never to another origin. */
function safeNext(raw: string | null): string {
  if (!raw || !raw.startsWith("/") || raw.startsWith("//") || raw.startsWith("/\\")) return "/admin";
  return raw;
}

const FEATURES = [
  { icon: KeyRound, title: "Keys by purchase", text: "One serial per sale; copy any key again later." },
  { icon: MonitorSmartphone, title: "One key, one PC", text: "See every activation and move a key when needed." },
  { icon: ShieldCheck, title: "Signed licences", text: "Ed25519 — the private key never leaves this server." },
  { icon: MailCheck, title: "Keys by email", text: "Each purchase emails its keys to the customer." },
];

function LoginForm() {
  const router = useRouter();
  const params = useSearchParams();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPw, setShowPw] = useState(false);
  const [capsLock, setCapsLock] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  /** Sent here by the panel when the cookie is still signed but the account no longer may sign in. */
  const signedOut = params.get("reason") === "signed_out";

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError("");
    let res: Response;
    try {
      res = await fetch("/api/login", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email, password }),
      });
    } catch {
      setBusy(false);
      setError("Could not reach the server. Check your connection and try again.");
      return;
    }
    if (res.ok) {
      router.push(safeNext(params.get("next")));
      router.refresh();
      return; // keep the button busy while the panel loads
    }
    setBusy(false);
    if (res.status === 400) setError("Enter a valid email address and your password.");
    else if (res.status === 401) setError("That email and password don't match.");
    else if (res.status === 429) setError("Too many sign-in attempts. Wait 10 minutes, then try again.");
    else setError("The server could not sign you in right now. Try again in a moment.");
  }

  function trackCaps(e: React.KeyboardEvent) {
    setCapsLock(e.getModifierState?.("CapsLock") ?? false);
  }

  return (
    <div className="grid min-h-screen bg-background lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] xl:grid-cols-[minmax(0,5fr)_minmax(0,4fr)]">
      {/* ---------------- brand panel ---------------- */}
      <aside className="relative hidden overflow-hidden border-r border-sidebar-border bg-sidebar text-sidebar-foreground lg:flex lg:flex-col lg:justify-between lg:p-10">
        <div className="brand-glow absolute inset-0" aria-hidden />
        <div
          className="absolute inset-0 opacity-[0.07]"
          aria-hidden
          style={{
            backgroundImage:
              "linear-gradient(hsl(var(--sidebar-foreground)) 1px, transparent 1px), linear-gradient(90deg, hsl(var(--sidebar-foreground)) 1px, transparent 1px)",
            backgroundSize: "36px 36px",
            maskImage: "radial-gradient(70% 60% at 30% 40%, black, transparent)",
            WebkitMaskImage: "radial-gradient(70% 60% at 30% 40%, black, transparent)",
          }}
        />

        <div className="relative flex items-center gap-2.5">
          <Image src="/logo.webp" alt="TextSoft" width={36} height={36} priority />
          <div className="leading-tight">
            <div className="text-[15px] font-bold tracking-tight text-white">
              Text<span className="text-sidebar-primary">Soft</span>
            </div>
            <div className="text-[10px] font-medium uppercase tracking-[0.16em] text-sidebar-foreground/60">Beyond Typing</div>
          </div>
        </div>

        <div className="relative max-w-lg">
          <div className="mb-3 inline-flex h-6 items-center gap-1.5 rounded-full border border-white/10 bg-white/5 px-2.5 text-[11px] font-medium text-sidebar-foreground/80">
            <span className="h-1.5 w-1.5 rounded-full bg-sidebar-primary" aria-hidden />
            Licence administration
          </div>
          <h2 className="text-balance text-3xl font-semibold leading-tight tracking-tight text-white">
            Every licence, every customer, one place.
          </h2>
          <p className="mt-3 max-w-md text-balance text-sm leading-6 text-sidebar-foreground/65">
            Issue keys by purchase, watch activations, and renew or revoke a whole sale in one click.
          </p>
          <ul className="mt-7 grid gap-x-6 gap-y-4 sm:grid-cols-2">
            {FEATURES.map(({ icon: Icon, title, text }) => (
              <li key={title} className="flex gap-3">
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md border border-white/10 bg-white/5 text-sidebar-primary">
                  <Icon className="h-4 w-4" aria-hidden />
                </span>
                <span className="min-w-0">
                  <span className="block text-[13px] font-medium text-white">{title}</span>
                  <span className="block text-xs leading-5 text-sidebar-foreground/55">{text}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>

        <div className="relative flex items-center justify-between text-[11px] text-sidebar-foreground/55">
          <span>© {new Date().getFullYear()} TextSoft</span>
          <span>Every action in this panel is logged.</span>
        </div>
      </aside>

      {/* ---------------- form ---------------- */}
      <main className="relative flex items-center justify-center px-4 py-10 sm:px-6">
        <div className="absolute right-3 top-3">
          <ThemeToggle />
        </div>

        <div className="w-full max-w-[360px] animate-fade-up">
          <div className="mb-8 flex items-center gap-2.5 lg:hidden">
            <Image src="/logo.webp" alt="TextSoft" width={36} height={36} priority />
            <div className="leading-tight">
              <div className="text-[15px] font-bold tracking-tight">
                Text<span className="text-primary">Soft</span>
              </div>
              <div className="text-[10px] font-medium uppercase tracking-[0.16em] text-muted-foreground">Licences</div>
            </div>
          </div>

          <h1 className="text-xl font-semibold tracking-tight">Sign in</h1>
          <p className="mt-1 text-[13px] text-muted-foreground">Welcome back. Sign in to the licence panel.</p>

          {signedOut && !error && (
            <Alert tone="info" title="You were signed out" className="mt-4">
              Your account or password was changed. Sign in again to continue.
            </Alert>
          )}

          <form onSubmit={submit} className="mt-6 space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="email" className="text-xs">
                Email
              </Label>
              <Input
                id="email"
                type="email"
                autoComplete="username"
                placeholder="you@textsoft.in"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                autoFocus
                required
                spellCheck={false}
                autoCapitalize="none"
                className="h-10"
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="password" className="text-xs">
                Password
              </Label>
              <div className="relative">
                <Input
                  id="password"
                  type={showPw ? "text" : "password"}
                  autoComplete="current-password"
                  placeholder="Your password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  onKeyDown={trackCaps}
                  onKeyUp={trackCaps}
                  onBlur={() => setCapsLock(false)}
                  required
                  aria-describedby={capsLock ? "caps-hint" : undefined}
                  className="h-10 pr-10"
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  onClick={() => setShowPw((v) => !v)}
                  aria-label={showPw ? "Hide password" : "Show password"}
                  title={showPw ? "Hide password" : "Show password"}
                  className="absolute right-1.5 top-1/2 -translate-y-1/2 text-muted-foreground"
                >
                  {showPw ? <EyeOff /> : <Eye />}
                </Button>
              </div>
              {capsLock && (
                <p id="caps-hint" className="text-[11px] text-warning">
                  Caps Lock is on.
                </p>
              )}
            </div>

            {error && <Alert tone="danger" title={error} />}

            <Button type="submit" size="lg" className="w-full" disabled={busy}>
              {busy ? <Loader2 className="animate-spin" /> : null}
              {busy ? "Signing in…" : "Sign in"}
              {!busy && <ArrowRight />}
            </Button>
          </form>

          <p className="mt-6 border-t pt-4 text-center text-[11px] leading-5 text-muted-foreground">
            Access is restricted to TextSoft staff. Forgot your password? Ask a panel owner to reset it.
          </p>
        </div>
      </main>
    </div>
  );
}

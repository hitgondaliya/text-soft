"use client";

import * as React from "react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import {
  Activity,
  Check,
  ExternalLink,
  Eye,
  EyeOff,
  Loader2,
  Mail,
  RefreshCw,
  RotateCcw,
  Save,
  Send,
} from "lucide-react";
import { SectionCard } from "@/components/section-card";
import { KvList } from "@/components/kv-list";
import { Alert, type AlertTone } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { cn, fmtDateTime, fmtRelative } from "@/lib/utils";

/*
 * Settings → Email (SMTP). Talks to:
 *   GET    /api/admin/settings/smtp        current effective settings (never the password)
 *   PUT    /api/admin/settings/smtp        save; a blank password keeps the stored one
 *                                          (same host + username only, else 400 field "password")
 *   DELETE /api/admin/settings/smtp        forget the panel's settings → back to .env
 *   POST   /api/admin/settings/smtp/test   { to, config } — tries the CURRENT form values
 *
 * Props: email (default test recipient — the signed-in owner), aside (extra cards for
 * the right-hand column, e.g. the server-rendered "Server" card).
 */

type Security = "ssl" | "starttls" | "none";
type Source = "db" | "env" | "none";

interface SmtpView {
  configured: boolean;
  source: Source;
  host: string;
  port: number;
  security: Security;
  user: string;
  hasPassword: boolean;
  fromName: string;
  fromEmail: string;
  replyTo: string;
  updatedBy?: string;
  updatedUtc?: string;
}

interface Form {
  host: string;
  port: string;
  security: Security;
  user: string;
  password: string;
  fromName: string;
  fromEmail: string;
  replyTo: string;
}

type FieldName = keyof Form | "to";
type Errors = Partial<Record<FieldName, string>>;
type Result = { tone: AlertTone; title: string; detail?: string } | null;

type ApiResult<T> =
  | { ok: true; data: T }
  | { ok: false; status: number; error: string; field?: string; detail?: string };

const SECURITY_LABEL: Record<Security, string> = { ssl: "SSL/TLS", starttls: "STARTTLS", none: "no encryption" };
const STANDARD_PORT: Partial<Record<Security, number>> = { ssl: 465, starttls: 587 };
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const HOST_RE = /^[A-Za-z0-9.:\-]+$/;

// ---- provider presets ------------------------------------------------------------

type PresetId = "gmail" | "outlook" | "zoho" | "custom";

interface Preset {
  id: PresetId;
  label: string;
  host?: string;
  port?: number;
  security?: Security;
  /** Hosts that count as this provider when the settings are loaded. */
  matches: string[];
  note: React.ReactNode;
}

const PRESETS: Preset[] = [
  {
    id: "gmail",
    label: "Gmail",
    host: "smtp.gmail.com",
    port: 465,
    security: "ssl",
    matches: ["smtp.gmail.com", "smtp.googlemail.com"],
    note: (
      <>
        <b className="font-medium text-foreground">Use a Google App Password, not the account password.</b> Turn on
        2-Step Verification for the Google account, then{" "}
        <a
          href="https://myaccount.google.com/apppasswords"
          target="_blank"
          rel="noreferrer"
          className="font-medium text-primary-strong underline-offset-2 hover:underline"
        >
          create an App Password
        </a>{" "}
        and paste the 16 letters below. The username is the full Gmail address; Gmail sends from that address.
      </>
    ),
  },
  {
    id: "outlook",
    label: "Outlook / 365",
    host: "smtp.office365.com",
    port: 587,
    security: "starttls",
    matches: ["smtp.office365.com", "smtp-mail.outlook.com"],
    note: (
      <>
        The username is the full mailbox address. The mailbox needs <b className="font-medium text-foreground">SMTP AUTH</b>{" "}
        turned on (Microsoft 365 admin centre → the user → Mail → Manage email apps). Accounts with multi-factor sign-in need
        an app password.
      </>
    ),
  },
  {
    id: "zoho",
    label: "Zoho Mail",
    host: "smtp.zoho.in",
    port: 465,
    security: "ssl",
    matches: ["smtp.zoho.in", "smtp.zoho.com", "smtppro.zoho.in", "smtppro.zoho.com"],
    note: (
      <>
        <span className="font-mono text-foreground">smtp.zoho.in</span> is for accounts on Zoho&apos;s India data centre; use{" "}
        <span className="font-mono text-foreground">smtp.zoho.com</span> for others. With two-factor sign-in on, create an
        app-specific password in Zoho Accounts.
      </>
    ),
  },
  {
    id: "custom",
    label: "Custom",
    matches: [],
    note: (
      <>
        Any SMTP server — your hosting or email provider lists the values. Port 465 usually means SSL/TLS, 587 means
        STARTTLS.
      </>
    ),
  },
];

function detectPreset(host: string): PresetId {
  const h = host.trim().toLowerCase();
  return PRESETS.find((p) => p.matches.includes(h))?.id ?? "custom";
}

// ---- helpers -----------------------------------------------------------------------

function formFromView(v: SmtpView): Form {
  return {
    host: v.host ?? "",
    port: v.port ? String(v.port) : "",
    security: v.security ?? "ssl",
    user: v.user ?? "",
    password: "",
    fromName: v.fromName ?? "",
    fromEmail: v.fromEmail ?? "",
    replyTo: v.replyTo ?? "",
  };
}

function sameServer(f: Form, v: SmtpView | null): boolean {
  if (!v) return false;
  return f.host.trim().toLowerCase() === v.host.trim().toLowerCase() && f.user.trim() === v.user.trim();
}

/** The server reuses a stored password only for the same host + username. */
function needsPassword(f: Form, v: SmtpView | null): boolean {
  if (f.security === "none" || f.password.trim()) return false;
  return !(v?.hasPassword && sameServer(f, v));
}

function bodyFrom(f: Form) {
  return {
    host: f.host.trim(),
    port: Number(f.port),
    security: f.security,
    user: f.user.trim(),
    ...(f.password ? { password: f.password } : {}),
    fromName: f.fromName.trim(),
    fromEmail: f.fromEmail.trim(),
    replyTo: f.replyTo.trim(),
  };
}

const FIELD_LABEL: Record<FieldName, string> = {
  host: "Host",
  port: "Port",
  security: "Security",
  user: "Username",
  password: "Password",
  fromName: "From name",
  fromEmail: "From email",
  replyTo: "Reply-to",
  to: "Recipient",
};

function fieldMessage(field: FieldName): string {
  switch (field) {
    case "host":
      return "Enter a host such as smtp.gmail.com — letters, digits, dots and dashes only.";
    case "port":
      return "Enter a port between 1 and 65535.";
    case "security":
      return "Choose SSL/TLS, STARTTLS or None.";
    case "user":
      return "SSL/TLS and STARTTLS servers need a username — usually the full email address.";
    case "password":
      return "Type the password. A saved password is only reused for the same host and username.";
    case "fromName":
      return "Keep the from name under 100 characters.";
    case "fromEmail":
      return "Enter a valid sender address.";
    case "replyTo":
      return "Enter a valid reply-to address, or leave it blank.";
    case "to":
      return "Enter a valid recipient address.";
  }
}

function asField(raw: string | undefined): FieldName | undefined {
  if (!raw) return undefined;
  const f = raw.replace(/^config\./, "");
  return f in FIELD_LABEL ? (f as FieldName) : undefined;
}

function describeError(r: { status: number; error: string; field?: string; detail?: string }): string {
  if (r.status === 0) return "Could not reach the panel. Check the connection and try again.";
  if (r.status === 401) return "Your session has ended. Sign in again.";
  if (r.status === 403) return "Only the owner can change settings.";
  if (r.error === "bad_request") {
    const f = asField(r.field);
    // the server explains rules the page cannot check itself (e.g. a port on this server's own network)
    return f ? `${FIELD_LABEL[f]}: ${r.detail || fieldMessage(f)}` : r.detail || "Some values are not valid. Check the form.";
  }
  if (r.error === "mail_failed") return r.detail || "The mail server refused the message.";
  return r.detail || `The request failed (${r.error || r.status}).`;
}

async function api<T>(url: string, init?: RequestInit): Promise<ApiResult<T>> {
  try {
    const res = await fetch(url, {
      cache: "no-store",
      ...init,
      headers: init?.body ? { "content-type": "application/json" } : undefined,
    });
    const body = (await res.json().catch(() => ({}))) as Record<string, unknown>;
    if (res.ok) return { ok: true, data: body as T };
    return {
      ok: false,
      status: res.status,
      error: typeof body.error === "string" ? body.error : "",
      field: typeof body.field === "string" ? body.field : undefined,
      detail: typeof body.detail === "string" ? body.detail : undefined,
    };
  } catch {
    return { ok: false, status: 0, error: "network" };
  }
}

/** Client-side checks that mirror the server's, so most mistakes never leave the page. */
function validate(f: Form, v: SmtpView | null): Errors {
  const e: Errors = {};
  const host = f.host.trim();
  if (!host) e.host = "Enter the SMTP host.";
  else if (!HOST_RE.test(host)) e.host = fieldMessage("host");
  const port = Number(f.port);
  if (!f.port.trim() || !Number.isInteger(port) || port < 1 || port > 65535) e.port = fieldMessage("port");
  if (f.security !== "none" && !f.user.trim()) e.user = fieldMessage("user");
  else if (needsPassword(f, v))
    e.password =
      v?.hasPassword && !sameServer(f, v)
        ? "You changed the host or username, so type the password for this account."
        : "Type the password for this account.";
  if (f.fromName.trim().length > 100) e.fromName = fieldMessage("fromName");
  if (!f.fromEmail.trim()) e.fromEmail = "Enter the address emails are sent from.";
  else if (!EMAIL_RE.test(f.fromEmail.trim())) e.fromEmail = fieldMessage("fromEmail");
  if (f.replyTo.trim() && !EMAIL_RE.test(f.replyTo.trim())) e.replyTo = fieldMessage("replyTo");
  return e;
}

const FIELD_ORDER: FieldName[] = ["host", "port", "security", "user", "password", "fromName", "fromEmail", "replyTo"];

// ---- component ---------------------------------------------------------------------

export function SmtpSettings({ email, aside }: { email: string; aside?: React.ReactNode }) {
  const [view, setView] = useState<SmtpView | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState<Form | null>(null);
  const [errors, setErrors] = useState<Errors>({});
  const [presetChoice, setPresetChoice] = useState<PresetId | null>(null);
  const [showPw, setShowPw] = useState(false);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [resetting, setResetting] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);
  const [saveResult, setSaveResult] = useState<Result>(null);
  const [testResult, setTestResult] = useState<Result>(null);
  const [to, setTo] = useState(email);
  const refs = useRef<Partial<Record<FieldName, HTMLElement | null>>>({});

  const adopt = useCallback((v: SmtpView) => {
    setView(v);
    setForm(formFromView(v));
    setErrors({});
    setPresetChoice(null);
    setShowPw(false);
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    const r = await api<SmtpView>("/api/admin/settings/smtp");
    if (r.ok) adopt(r.data);
    else setLoadError(describeError(r));
    setLoading(false);
  }, [adopt]);

  useEffect(() => {
    void load();
  }, [load]);

  const initial = useMemo(() => (view ? formFromView(view) : null), [view]);
  const dirty = useMemo(() => {
    if (!form || !initial) return false;
    return (Object.keys(form) as Array<keyof Form>).some((k) => form[k] !== initial[k]);
  }, [form, initial]);

  // Warn before leaving with unsaved edits.
  useEffect(() => {
    if (!dirty) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = "";
    };
    window.addEventListener("beforeunload", onBeforeUnload);
    return () => window.removeEventListener("beforeunload", onBeforeUnload);
  }, [dirty]);

  const preset: PresetId = presetChoice ?? (form ? detectPreset(form.host) : "custom");
  const presetInfo = PRESETS.find((p) => p.id === preset) ?? PRESETS[PRESETS.length - 1];

  function set<K extends keyof Form>(key: K, value: Form[K]) {
    setForm((f) => (f ? { ...f, [key]: value } : f));
    setErrors((e) => (e[key] ? { ...e, [key]: undefined } : e));
    setSaveResult(null);
    setTestResult(null);
  }

  function applyPreset(p: Preset) {
    setSaveResult(null);
    setTestResult(null);
    if (p.id === "custom") {
      setPresetChoice("custom");
      setTimeout(() => refs.current.host?.focus(), 0);
      return;
    }
    setPresetChoice(null);
    setForm((f) => (f ? { ...f, host: p.host ?? f.host, port: String(p.port ?? f.port), security: p.security ?? f.security } : f));
    setErrors((e) => ({ ...e, host: undefined, port: undefined, security: undefined }));
  }

  function setSecurity(s: Security) {
    if (!form) return;
    // Follow the standard port when switching between SSL/TLS (465) and STARTTLS (587).
    const std = STANDARD_PORT[form.security];
    const next = STANDARD_PORT[s];
    if (next && std && Number(form.port) === std) set("port", String(next));
    set("security", s);
  }

  function focusFirst(errs: Errors) {
    const first = FIELD_ORDER.find((k) => errs[k]);
    if (first) refs.current[first]?.focus();
  }

  /** Apply a server error to the form; returns the message to show. */
  function applyServerError(r: { status: number; error: string; field?: string; detail?: string }): string {
    const msg = describeError(r);
    const f = asField(r.field);
    if (r.error === "bad_request" && f) {
      setErrors((e) => ({ ...e, [f]: r.detail || fieldMessage(f) }));
      if (f !== "to") refs.current[f]?.focus();
    }
    return msg;
  }

  async function save(e?: React.FormEvent) {
    e?.preventDefault();
    if (!form) return;
    const errs = validate(form, view);
    setErrors(errs);
    if (Object.values(errs).some(Boolean)) {
      setSaveResult({ tone: "danger", title: "Fix the highlighted fields", detail: "Nothing was saved." });
      focusFirst(errs);
      return;
    }
    setSaving(true);
    setSaveResult(null);
    const r = await api<SmtpView>("/api/admin/settings/smtp", { method: "PUT", body: JSON.stringify(bodyFrom(form)) });
    setSaving(false);
    if (r.ok) {
      adopt(r.data);
      const where = `${r.data.host}:${r.data.port}`;
      setSaveResult({
        tone: r.data.configured ? "success" : "warning",
        title: r.data.configured ? "Email settings saved" : "Saved, but email is not ready yet",
        detail: r.data.configured
          ? `Purchase emails now go out through ${where}. Send a test to be sure.`
          : "Something is still missing — check the username and password.",
      });
      toast.success("Email settings saved");
    } else {
      const msg = applyServerError(r);
      setSaveResult({ tone: "danger", title: "Could not save the settings", detail: msg });
      toast.error(msg);
    }
  }

  async function sendTest(e: React.FormEvent) {
    e.preventDefault();
    if (!form) return;
    const recipient = to.trim();
    const errs = validate(form, view);
    if (!recipient || !EMAIL_RE.test(recipient)) errs.to = fieldMessage("to");
    setErrors(errs);
    if (Object.values(errs).some(Boolean)) {
      setTestResult({
        tone: "danger",
        title: "Nothing was sent",
        detail: errs.to && Object.keys(errs).length === 1 ? errs.to : "Fix the highlighted fields first.",
      });
      if (errs.to && Object.values(errs).filter(Boolean).length === 1) refs.current.to?.focus();
      else focusFirst(errs);
      return;
    }
    setTesting(true);
    setTestResult(null);
    const r = await api<{ ok: true; to: string }>("/api/admin/settings/smtp/test", {
      method: "POST",
      body: JSON.stringify({ to: recipient, config: bodyFrom(form) }),
    });
    setTesting(false);
    if (r.ok) {
      setTestResult({
        tone: "success",
        title: `Test email sent to ${r.data.to}`,
        detail: dirty
          ? "These values work. Save them so purchase emails use them too."
          : "Check the inbox (and the spam folder).",
      });
      toast.success(`Test email sent to ${r.data.to}`);
    } else {
      const msg = applyServerError(r);
      setTestResult({
        tone: "danger",
        title: r.error === "mail_failed" ? "The mail server refused the test" : "Could not send the test",
        detail: msg,
      });
      toast.error("Test email failed");
    }
  }

  async function reset() {
    setResetting(true);
    const r = await api<SmtpView>("/api/admin/settings/smtp", { method: "DELETE" });
    setResetting(false);
    setResetOpen(false);
    if (r.ok) {
      adopt(r.data);
      setTestResult(null);
      setSaveResult(
        r.data.source === "env"
          ? { tone: "success", title: "Reset to server defaults", detail: "Mail now uses the SMTP values from the server's .env file." }
          : {
              tone: "warning",
              title: "Reset — email is now not set up",
              detail: "The server's .env has no SMTP account, so purchase emails cannot be sent until you save settings here.",
            },
      );
      toast.success("Email settings reset");
    } else {
      const msg = describeError(r);
      setSaveResult({ tone: "danger", title: "Could not reset the settings", detail: msg });
      toast.error(msg);
    }
  }

  const busy = saving || testing || resetting;
  const err = (k: FieldName) => errors[k];
  const inputProps = (k: FieldName) => ({
    id: `smtp-${k}`,
    ref: (el: HTMLInputElement | null) => {
      refs.current[k] = el;
    },
    "aria-invalid": err(k) ? true : undefined,
    "aria-describedby": err(k) || HINTS_FOR.has(k) ? `smtp-${k}-msg` : undefined,
    className: cn(err(k) && "border-destructive focus-visible:border-destructive focus-visible:ring-destructive/20"),
  });

  const portMismatch =
    form && form.security === "ssl" && Number(form.port) === 587
      ? "Port 587 normally uses STARTTLS."
      : form && form.security === "starttls" && Number(form.port) === 465
        ? "Port 465 normally uses SSL/TLS."
        : null;

  let passwordHint: React.ReactNode = "The account's SMTP or app password.";
  let passwordHintWarn = false;
  if (form) {
    if (form.security === "none") passwordHint = "Optional for a local test server.";
    else if (form.password) passwordHint = "Saved encrypted. It is never shown again.";
    else if (view?.hasPassword && sameServer(form, view)) passwordHint = "A password is saved. Leave blank to keep it.";
    else if (view?.hasPassword) {
      passwordHint = "You changed the host or username — type the password for this account.";
      passwordHintWarn = true;
    }
  }

  return (
    <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1fr)_340px]">
      {/* ---------------- the form ---------------- */}
      <form onSubmit={save} noValidate autoComplete="off" aria-busy={busy}>
        <SectionCard
          title="Email (SMTP)"
          icon={Mail}
          iconClassName="text-primary"
          description="The mailbox purchase emails, key re-sends and test messages are sent from."
          actions={view ? <SourceBadge source={view.source} /> : null}
          bodyClassName="space-y-5"
          footer={
            <div className="flex w-full flex-wrap items-center justify-between gap-2">
              <span className="flex items-center gap-1.5">
                {dirty ? (
                  <>
                    <span className="h-1.5 w-1.5 rounded-full bg-warning" aria-hidden />
                    Unsaved changes
                  </>
                ) : view?.source === "db" ? (
                  "Saved in the panel — overrides the server .env."
                ) : view?.source === "env" ? (
                  "Using the server .env. Save to manage it here."
                ) : (
                  "Not set up yet."
                )}
              </span>
              <span className="flex flex-wrap items-center gap-2">
                {view?.source === "db" && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    disabled={busy}
                    onClick={() => setResetOpen(true)}
                    className="text-muted-foreground hover:text-destructive"
                  >
                    <RotateCcw /> Reset to server defaults
                  </Button>
                )}
                {dirty && (
                  <Button type="button" variant="outline" size="sm" disabled={busy} onClick={() => view && adopt(view)}>
                    Discard
                  </Button>
                )}
                <Button type="submit" size="sm" disabled={busy || !form || (!dirty && view?.source === "db")}>
                  {saving ? <Loader2 className="animate-spin" /> : <Save />} Save settings
                </Button>
              </span>
            </div>
          }
        >
          {loading && !form ? (
            <FormSkeleton />
          ) : loadError && !form ? (
            <Alert
              tone="danger"
              title="Could not load the email settings"
              action={
                <Button type="button" variant="outline" size="xs" onClick={() => void load()}>
                  <RefreshCw /> Retry
                </Button>
              }
            >
              {loadError}
            </Alert>
          ) : form ? (
            <>
              {view?.source === "env" && (
                <Alert tone="info" title="These values come from the server's .env file">
                  Save to keep them in the panel instead. The .env stays as the fallback if you reset later.
                </Alert>
              )}
              {view?.source === "none" && (
                <Alert tone="warning" title="Email is not set up">
                  Choose a provider, fill in the account and save. Purchase emails cannot be sent until then.
                </Alert>
              )}

              {/* Provider */}
              <fieldset className="space-y-2.5">
                <legend className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Provider</legend>
                <div role="group" aria-label="Email provider" className="grid grid-cols-2 gap-2 md:grid-cols-4">
                  {PRESETS.map((p) => {
                    const active = preset === p.id;
                    return (
                      <button
                        key={p.id}
                        type="button"
                        aria-pressed={active}
                        aria-label={p.host ? `${p.label} (${p.host})` : `${p.label} server`}
                        onClick={() => applyPreset(p)}
                        className={cn(
                          "flex min-w-0 flex-col items-start rounded-md border px-3 py-2 text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                          active ? "border-primary/60 bg-primary/5 ring-1 ring-primary/25" : "bg-card hover:bg-muted/60",
                        )}
                      >
                        <span className="flex w-full items-center justify-between gap-2 text-[13px] font-medium">
                          <span className="truncate">{p.label}</span>
                          {active && <Check className="h-3.5 w-3.5 shrink-0 text-primary" aria-hidden />}
                        </span>
                        <span className="mt-0.5 w-full truncate font-mono text-[11px] text-muted-foreground">
                          {p.host ?? "Any SMTP server"}
                        </span>
                      </button>
                    );
                  })}
                </div>
                <p className="rounded-md bg-muted/50 px-3 py-2 text-xs leading-5 text-muted-foreground">{presetInfo.note}</p>
              </fieldset>

              {/* Server */}
              <fieldset className="space-y-3">
                <legend className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Server</legend>
                <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_96px_minmax(0,190px)]">
                  <Field id="smtp-host" label="Host" error={err("host")}>
                    <Input
                      {...inputProps("host")}
                      value={form.host}
                      onChange={(e) => {
                        setPresetChoice(null);
                        set("host", e.target.value);
                      }}
                      placeholder="smtp.example.com"
                      spellCheck={false}
                      autoCapitalize="none"
                      className={cn("font-mono text-xs", inputProps("host").className)}
                    />
                  </Field>
                  <Field id="smtp-port" label="Port" error={err("port")}>
                    <Input
                      {...inputProps("port")}
                      value={form.port}
                      onChange={(e) => set("port", e.target.value.replace(/[^\d]/g, "").slice(0, 5))}
                      inputMode="numeric"
                      placeholder="465"
                      className={cn("font-mono text-xs tabular-nums", inputProps("port").className)}
                    />
                  </Field>
                  <Field id="smtp-security" label="Security" error={err("security")}>
                    <Select value={form.security} onValueChange={(v) => setSecurity(v as Security)}>
                      <SelectTrigger
                        id="smtp-security"
                        ref={(el) => {
                          refs.current.security = el;
                        }}
                        aria-invalid={err("security") ? true : undefined}
                      >
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="ssl">SSL/TLS</SelectItem>
                        <SelectItem value="starttls">STARTTLS</SelectItem>
                        <SelectItem value="none">None — testing only</SelectItem>
                      </SelectContent>
                    </Select>
                  </Field>
                </div>
                {portMismatch && <p className="text-[11px] text-warning">{portMismatch}</p>}
                {form.security === "none" && (
                  <Alert tone="warning" title="No encryption">
                    The password and every email cross the network in plain text. Use None only for a local test server such
                    as 127.0.0.1 — never for a real mailbox.
                  </Alert>
                )}
              </fieldset>

              {/* Sign-in */}
              <fieldset className="space-y-3">
                <legend className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Sign-in</legend>
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field id="smtp-user" label="Username" error={err("user")} optional={form.security === "none"}>
                    <Input
                      {...inputProps("user")}
                      name="smtp-username"
                      value={form.user}
                      onChange={(e) => set("user", e.target.value)}
                      onBlur={() => {
                        if (!form.fromEmail.trim() && EMAIL_RE.test(form.user.trim())) set("fromEmail", form.user.trim());
                      }}
                      placeholder={preset === "gmail" ? "you@gmail.com" : "you@example.com"}
                      autoComplete="off"
                      spellCheck={false}
                      autoCapitalize="none"
                    />
                  </Field>
                  <Field
                    id="smtp-password"
                    label="Password"
                    error={err("password")}
                    hint={passwordHint}
                    hintWarn={passwordHintWarn}
                    optional={form.security === "none"}
                  >
                    <div className="relative">
                      <Input
                        {...inputProps("password")}
                        name="smtp-password"
                        type={showPw ? "text" : "password"}
                        value={form.password}
                        onChange={(e) => set("password", e.target.value)}
                        placeholder={
                          view?.hasPassword && sameServer(form, view)
                            ? "•••••••• saved — leave blank to keep"
                            : preset === "gmail"
                              ? "16-letter App Password"
                              : "Password"
                        }
                        autoComplete="new-password"
                        spellCheck={false}
                        className={cn("pr-9", form.password && "font-mono text-xs", inputProps("password").className)}
                      />
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon-xs"
                        onClick={() => setShowPw((v) => !v)}
                        disabled={!form.password}
                        aria-label={showPw ? "Hide password" : "Show password"}
                        title={showPw ? "Hide password" : "Show password"}
                        className="absolute right-1.5 top-1/2 -translate-y-1/2 text-muted-foreground"
                      >
                        {showPw ? <EyeOff /> : <Eye />}
                      </Button>
                    </div>
                  </Field>
                </div>
              </fieldset>

              {/* Sender */}
              <fieldset className="space-y-3">
                <legend className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Sender</legend>
                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  <Field id="smtp-fromName" label="From name" error={err("fromName")} hint="Shown as the sender in the inbox.">
                    <Input {...inputProps("fromName")} value={form.fromName} onChange={(e) => set("fromName", e.target.value)} placeholder="TextSoft" maxLength={100} />
                  </Field>
                  <Field
                    id="smtp-fromEmail"
                    label="From email"
                    error={err("fromEmail")}
                    hint={preset === "gmail" ? "Usually the Gmail address itself." : "Must be allowed by the mail server."}
                  >
                    <Input
                      {...inputProps("fromEmail")}
                      type="email"
                      value={form.fromEmail}
                      onChange={(e) => set("fromEmail", e.target.value)}
                      placeholder="licences@example.com"
                      spellCheck={false}
                      autoCapitalize="none"
                    />
                  </Field>
                  <Field id="smtp-replyTo" label="Reply-to" error={err("replyTo")} optional hint="Where customer replies go.">
                    <Input
                      {...inputProps("replyTo")}
                      type="email"
                      value={form.replyTo}
                      onChange={(e) => set("replyTo", e.target.value)}
                      placeholder="support@example.com"
                      spellCheck={false}
                      autoCapitalize="none"
                    />
                  </Field>
                </div>
              </fieldset>

              {saveResult && (
                <Alert tone={saveResult.tone} title={saveResult.title}>
                  {saveResult.detail}
                </Alert>
              )}
            </>
          ) : null}
        </SectionCard>
      </form>

      {/* ---------------- right column ---------------- */}
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-1">
        <SectionCard
          title="Current status"
          icon={Activity}
          footer={
            <a
              href="/api/admin/mail-preview"
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 font-medium text-foreground hover:text-primary-strong"
            >
              Preview the purchase email <ExternalLink className="h-3 w-3" aria-hidden />
            </a>
          }
        >
          {!view ? (
            loadError ? (
              <p className="text-xs text-muted-foreground">Not available.</p>
            ) : (
              <div className="space-y-2.5">
                <Skeleton className="h-4 w-2/3" />
                <Skeleton className="h-4 w-5/6" />
                <Skeleton className="h-4 w-1/2" />
              </div>
            )
          ) : (
            <KvList
              labelWidth="5.5rem"
              items={[
                { label: "Source", value: <SourceBadge source={view.source} /> },
                {
                  label: "Status",
                  value: view.configured ? (
                    <span className="inline-flex items-center gap-1.5 font-medium text-success">
                      <span className="h-1.5 w-1.5 rounded-full bg-current" aria-hidden /> Ready to send
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1.5 font-medium text-warning">
                      <span className="h-1.5 w-1.5 rounded-full bg-current" aria-hidden /> Not ready
                    </span>
                  ),
                },
                {
                  label: "Sending as",
                  value: view.fromEmail ? (
                    <span className="block min-w-0">
                      {view.fromName && <span className="block truncate">{view.fromName}</span>}
                      <span className={cn("block break-all", view.fromName && "text-xs text-muted-foreground")}>{view.fromEmail}</span>
                    </span>
                  ) : null,
                },
                {
                  label: "Server",
                  value: view.host ? (
                    <span>
                      <span className="break-all font-mono text-xs">
                        {view.host}:{view.port}
                      </span>
                      <span className="ml-1.5 text-xs text-muted-foreground">{SECURITY_LABEL[view.security]}</span>
                    </span>
                  ) : null,
                },
                { label: "Reply-to", value: view.replyTo, hidden: !view.replyTo },
                {
                  label: "Updated",
                  hidden: !view.updatedUtc,
                  value: view.updatedUtc ? (
                    <span title={fmtDateTime(view.updatedUtc)}>
                      {fmtRelative(view.updatedUtc)}
                      {view.updatedBy && <span className="text-muted-foreground"> by {view.updatedBy}</span>}
                    </span>
                  ) : null,
                },
              ]}
            />
          )}
        </SectionCard>

        <SectionCard title="Send a test email" icon={Send} description="Uses the values in the form — saved or not.">
          <form onSubmit={sendTest} noValidate className="space-y-2">
            <Label htmlFor="smtp-to" className="text-xs">
              Recipient
            </Label>
            <div className="flex gap-2">
              <Input
                id="smtp-to"
                ref={(el) => {
                  refs.current.to = el;
                }}
                type="email"
                value={to}
                onChange={(e) => {
                  setTo(e.target.value);
                  setErrors((x) => (x.to ? { ...x, to: undefined } : x));
                }}
                placeholder="you@example.com"
                aria-invalid={err("to") ? true : undefined}
                aria-describedby={err("to") ? "smtp-to-msg" : undefined}
                spellCheck={false}
                autoCapitalize="none"
                className={cn("min-w-0", err("to") && "border-destructive focus-visible:border-destructive focus-visible:ring-destructive/20")}
              />
              <Button type="submit" variant="outline" disabled={busy || !form} className="shrink-0">
                {testing ? <Loader2 className="animate-spin" /> : <Send />} Send test
              </Button>
            </div>
            {err("to") && (
              <p id="smtp-to-msg" className="text-[11px] leading-4 text-destructive">
                {err("to")}
              </p>
            )}
            {testResult && (
              <Alert tone={testResult.tone} title={testResult.title} className="mt-1 break-words">
                {testResult.detail}
              </Alert>
            )}
          </form>
        </SectionCard>

        {aside}
      </div>

      <Dialog open={resetOpen} onOpenChange={(v) => !resetting && setResetOpen(v)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Reset to server defaults?</DialogTitle>
            <DialogDescription>
              This forgets the email settings saved in the panel, including the stored password. Mail will then be sent with
              the SMTP values in the server&apos;s .env file — or not at all if the .env has none.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => setResetOpen(false)} disabled={resetting}>
              Cancel
            </Button>
            <Button type="button" variant="destructive" onClick={() => void reset()} disabled={resetting}>
              {resetting ? <Loader2 className="animate-spin" /> : <RotateCcw />} Reset settings
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

/** Fields whose hint line is always rendered (for aria-describedby). */
const HINTS_FOR = new Set<FieldName>(["password", "fromName", "fromEmail", "replyTo"]);

function SourceBadge({ source }: { source: Source }) {
  if (source === "db") return <Badge variant="brand">Saved in panel</Badge>;
  if (source === "env") return <Badge variant="secondary">From server .env</Badge>;
  return <Badge variant="warning">Not configured</Badge>;
}

function Field({
  id,
  label,
  hint,
  hintWarn,
  error,
  optional,
  children,
  className,
}: {
  id: string;
  label: React.ReactNode;
  hint?: React.ReactNode;
  hintWarn?: boolean;
  error?: string;
  optional?: boolean;
  children: React.ReactNode;
  className?: string;
}) {
  const msg = error ?? hint;
  return (
    <div className={cn("min-w-0 space-y-1.5", className)}>
      <div className="flex items-baseline justify-between gap-2">
        <Label htmlFor={id} className="text-xs">
          {label}
        </Label>
        {optional && <span className="text-[11px] text-muted-foreground">Optional</span>}
      </div>
      {children}
      {msg && (
        <p
          id={`${id}-msg`}
          className={cn("text-[11px] leading-4", error ? "text-destructive" : hintWarn ? "text-warning" : "text-muted-foreground")}
        >
          {msg}
        </p>
      )}
    </div>
  );
}

function FormSkeleton() {
  return (
    <div className="space-y-5" aria-hidden>
      <div className="grid grid-cols-2 gap-2 md:grid-cols-4">
        {Array.from({ length: 4 }, (_, i) => (
          <Skeleton key={i} className="h-12" />
        ))}
      </div>
      <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_96px_190px]">
        <Skeleton className="h-9" />
        <Skeleton className="h-9" />
        <Skeleton className="h-9" />
      </div>
      <div className="grid gap-3 sm:grid-cols-2">
        <Skeleton className="h-9" />
        <Skeleton className="h-9" />
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        <Skeleton className="h-9" />
        <Skeleton className="h-9" />
        <Skeleton className="h-9" />
      </div>
    </div>
  );
}

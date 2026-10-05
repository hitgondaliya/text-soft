"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import {
  Ban,
  ExternalLink,
  KeyRound,
  Loader2,
  Mail,
  MoreHorizontal,
  Pencil,
  RefreshCw,
  RotateCcw,
  Send,
  Settings,
  ShieldCheck,
  Trash2,
  UserPlus,
  Users,
} from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { SectionCard } from "@/components/section-card";
import { EmptyState } from "@/components/empty-state";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { cn, fmtDate, fmtDateTime, fmtRelative } from "@/lib/utils";

// Team — who can sign in to the panel. Owners add/remove users, change roles,
// disable accounts and reset passwords; anyone can rename themselves and change
// their own password (with the current one). API: /api/admin/admins[/:id].

interface Admin {
  id: string;
  email: string;
  name: string;
  role: string;
  status: string;
  createdUtc: string;
  createdBy: string;
  lastLoginUtc: string | null;
}

interface MailStatus {
  configured: boolean;
  from: string;
  source?: "db" | "env" | "none";
}

const ERRORS: Record<string, string> = {
  wrong_password: "The current password is wrong.",
  last_owner: "You are an owner — make someone else owner first.",
  cannot_disable_self: "You cannot disable your own account.",
  cannot_delete_self: "You cannot remove your own account.",
  forbidden: "Only an owner can do that.",
  not_found: "That user no longer exists.",
  exists: "That email already has access.",
  bad_request: "Check the fields and try again.",
  unauthorized: "Your session has ended. Sign in again.",
};

async function errorText(res: Response | null, fallback: string): Promise<string> {
  if (!res) return "Could not reach the panel. Check the connection.";
  const d = (await res.json().catch(() => ({}))) as { error?: string };
  return (d.error && ERRORS[d.error]) || fallback;
}

async function send(url: string, method: string, body?: unknown): Promise<Response | null> {
  try {
    return await fetch(url, {
      method,
      headers: body ? { "content-type": "application/json" } : undefined,
      body: body ? JSON.stringify(body) : undefined,
    });
  } catch {
    return null;
  }
}

function initials(a: Admin): string {
  const src = a.name.trim() || a.email.split("@")[0];
  const parts = src.split(/[\s._-]+/).filter(Boolean);
  return ((parts[0]?.[0] ?? "") + (parts.length > 1 ? parts[parts.length - 1][0] : (parts[0]?.[1] ?? ""))).toUpperCase() || "?";
}

export default function TeamPage() {
  const [rows, setRows] = useState<Admin[] | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [me, setMe] = useState("");
  const [canManage, setCanManage] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [addOpen, setAddOpen] = useState(false);
  const [pwFor, setPwFor] = useState<Admin | null>(null);
  const [renameFor, setRenameFor] = useState<Admin | null>(null);
  const [removeFor, setRemoveFor] = useState<Admin | null>(null);
  const [mail, setMail] = useState<MailStatus | null>(null);
  const [mailFailed, setMailFailed] = useState(false);
  const [testing, setTesting] = useState(false);
  // A row menu that opens a dialog must not hand focus back to its trigger as it
  // closes — that would pull focus out of the dialog that just opened.
  const keepFocusInDialog = useRef(false);
  const openFromMenu = (open: () => void) => {
    keepFocusInDialog.current = true;
    open();
  };

  const load = useCallback(async () => {
    setLoadFailed(false);
    try {
      const r = await fetch("/api/admin/admins", { cache: "no-store" });
      if (!r.ok) throw new Error(String(r.status));
      const d = await r.json();
      setRows(d.admins ?? []);
      setMe(d.me ?? "");
      setCanManage(!!d.canManage);
    } catch {
      setLoadFailed(true);
      setRows((x) => x ?? []);
    }
  }, []);

  const loadMail = useCallback(async () => {
    setMailFailed(false);
    try {
      const r = await fetch("/api/admin/mail-test", { cache: "no-store" });
      if (!r.ok) throw new Error(String(r.status));
      setMail((await r.json()) as MailStatus);
    } catch {
      setMailFailed(true);
    }
  }, []);

  useEffect(() => {
    void load();
    void loadMail();
  }, [load, loadMail]);

  async function patch(a: Admin, body: Record<string, unknown>, success: string) {
    setBusyId(a.id);
    const res = await send(`/api/admin/admins/${a.id}`, "PATCH", body);
    setBusyId(null);
    if (res?.ok) toast.success(success);
    else toast.error(await errorText(res, "That didn't work."));
    void load();
  }

  async function testMail() {
    setTesting(true);
    const res = await send("/api/admin/mail-test", "POST");
    const d = res ? ((await res.json().catch(() => ({}))) as { to?: string; detail?: string; error?: string }) : {};
    setTesting(false);
    if (res?.ok) toast.success(`Test email sent to ${d.to ?? me}`);
    else toast.error(res ? `Email failed: ${d.detail ?? d.error ?? res.status}` : "Could not reach the panel.");
  }

  const owners = rows?.filter((a) => a.role === "owner").length ?? 0;
  const disabled = rows?.filter((a) => a.status === "disabled").length ?? 0;

  return (
    <div>
      <PageHeader
        title="Team"
        description="Who can sign in to this panel, and what they can do."
        actions={
          <>
            <Button
              variant="ghost"
              size="icon"
              onClick={() => {
                void load();
                void loadMail();
              }}
              aria-label="Refresh"
              title="Refresh"
              className="text-muted-foreground hover:text-foreground"
            >
              <RefreshCw />
            </Button>
            {canManage && (
              <Button size="sm" onClick={() => setAddOpen(true)}>
                <UserPlus /> Add user
              </Button>
            )}
          </>
        }
      />

      <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1fr)_340px]">
        <SectionCard
          title="Panel users"
          icon={Users}
          description="Owners manage users and settings. Staff do everything else."
          flush
          footer={
            rows && rows.length > 0 ? (
              <span>
                {rows.length} {rows.length === 1 ? "user" : "users"} · {owners} {owners === 1 ? "owner" : "owners"} ·{" "}
                {rows.length - owners} staff
                {disabled > 0 && <> · {disabled} disabled</>}
              </span>
            ) : undefined
          }
        >
          {loadFailed && (
            <div className="border-b p-3">
              <Alert
                tone="danger"
                title="Could not load the team"
                action={
                  <Button variant="outline" size="xs" onClick={() => void load()}>
                    <RefreshCw /> Retry
                  </Button>
                }
              />
            </div>
          )}
          {rows === null ? (
            <div className="space-y-2.5 p-4">
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-5/6" />
              <Skeleton className="h-4 w-2/3" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState compact icon={Users} title="No panel users" description="Run the seed-admin script on the server to create the first owner." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>User</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead className="hidden sm:table-cell">Status</TableHead>
                  <TableHead className="hidden md:table-cell">Last sign-in</TableHead>
                  <TableHead className="hidden 2xl:table-cell">Added</TableHead>
                  <TableHead className="w-10">
                    <span className="sr-only">Actions</span>
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((a) => {
                  const self = a.email === me;
                  const isDisabled = a.status === "disabled";
                  const busy = busyId === a.id;
                  const canPassword = canManage || self;
                  const canRename = canManage || self;
                  const canAdmin = canManage && !self;
                  return (
                    <TableRow key={a.id} className={cn(isDisabled && "text-muted-foreground")}>
                      <TableCell>
                        <div className="flex min-w-0 items-center gap-2.5">
                          <span
                            className={cn(
                              "flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-[11px] font-semibold",
                              isDisabled ? "bg-muted text-muted-foreground" : "bg-primary/10 text-primary-strong",
                            )}
                            aria-hidden
                          >
                            {initials(a)}
                          </span>
                          <div className="min-w-0 max-w-[144px] sm:max-w-none">
                            <div className="flex items-center gap-1.5">
                              <span className={cn("truncate font-medium", !isDisabled && "text-foreground")} title={a.name || a.email}>
                                {a.name || a.email}
                              </span>
                              {self && (
                                <Badge variant="outline" className="h-4 px-1 text-[10px]">
                                  You
                                </Badge>
                              )}
                              {isDisabled && (
                                <Badge variant="destructive" className="h-4 px-1 text-[10px] sm:hidden">
                                  Disabled
                                </Badge>
                              )}
                            </div>
                            {a.name && (
                              <div className="truncate text-xs text-muted-foreground" title={a.email}>
                                {a.email}
                              </div>
                            )}
                          </div>
                        </div>
                      </TableCell>
                      <TableCell className="whitespace-nowrap">
                        {canAdmin ? (
                          <Select
                            value={a.role}
                            disabled={busy}
                            onValueChange={(v) => void patch(a, { action: "setRole", role: v }, `${a.email} is now ${v === "owner" ? "an owner" : "staff"}`)}
                          >
                            <SelectTrigger className="h-7 w-[84px] px-2 text-xs sm:w-[92px]" aria-label={`Role for ${a.email}`}>
                              <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                              <SelectItem value="owner">Owner</SelectItem>
                              <SelectItem value="staff">Staff</SelectItem>
                            </SelectContent>
                          </Select>
                        ) : (
                          <Badge variant={a.role === "owner" ? "brand" : "secondary"}>
                            {a.role === "owner" && <ShieldCheck />}
                            {a.role === "owner" ? "Owner" : "Staff"}
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell className="hidden whitespace-nowrap sm:table-cell">
                        {isDisabled ? (
                          <Badge variant="destructive">
                            <span className="h-1.5 w-1.5 rounded-full bg-current" aria-hidden /> Disabled
                          </Badge>
                        ) : (
                          <Badge variant="success">
                            <span className="h-1.5 w-1.5 rounded-full bg-current" aria-hidden /> Active
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell className="hidden whitespace-nowrap text-muted-foreground md:table-cell">
                        <span title={a.lastLoginUtc ? fmtDateTime(a.lastLoginUtc) : undefined}>
                          {a.lastLoginUtc ? fmtRelative(a.lastLoginUtc) : "Never"}
                        </span>
                      </TableCell>
                      <TableCell className="hidden whitespace-nowrap text-muted-foreground 2xl:table-cell">
                        <span title={fmtDateTime(a.createdUtc)}>{fmtDate(a.createdUtc)}</span>
                        {a.createdBy && <span className="text-xs"> · by {a.createdBy}</span>}
                      </TableCell>
                      <TableCell className="text-right">
                        {(canPassword || canRename || canAdmin) && (
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button
                                variant="ghost"
                                size="icon-sm"
                                disabled={busy}
                                aria-label={`Actions for ${a.email}`}
                                title="Actions"
                                className="text-muted-foreground hover:text-foreground"
                              >
                                {busy ? <Loader2 className="animate-spin" /> : <MoreHorizontal />}
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent
                              align="end"
                              className="w-52"
                              onCloseAutoFocus={(e) => {
                                if (keepFocusInDialog.current) {
                                  e.preventDefault();
                                  keepFocusInDialog.current = false;
                                }
                              }}
                            >
                              <DropdownMenuLabel className="truncate font-normal text-muted-foreground">{a.email}</DropdownMenuLabel>
                              {canRename && (
                                <DropdownMenuItem onSelect={() => openFromMenu(() => setRenameFor(a))}>
                                  <Pencil /> {a.name ? "Edit name" : "Add a name"}
                                </DropdownMenuItem>
                              )}
                              {canPassword && (
                                <DropdownMenuItem onSelect={() => openFromMenu(() => setPwFor(a))}>
                                  <KeyRound /> {self ? "Change password" : "Reset password"}
                                </DropdownMenuItem>
                              )}
                              {canAdmin && (
                                <>
                                  <DropdownMenuSeparator />
                                  {isDisabled ? (
                                    <DropdownMenuItem onSelect={() => void patch(a, { action: "enable" }, `${a.email} can sign in again`)}>
                                      <RotateCcw /> Enable sign-in
                                    </DropdownMenuItem>
                                  ) : (
                                    <DropdownMenuItem onSelect={() => void patch(a, { action: "disable" }, `${a.email} disabled`)}>
                                      <Ban /> Disable sign-in
                                    </DropdownMenuItem>
                                  )}
                                  <DropdownMenuItem
                                    onSelect={() => openFromMenu(() => setRemoveFor(a))}
                                    className="text-destructive focus:bg-destructive/10 focus:text-destructive"
                                  >
                                    <Trash2 /> Remove from panel
                                  </DropdownMenuItem>
                                </>
                              )}
                            </DropdownMenuContent>
                          </DropdownMenu>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </SectionCard>

        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-1">
          <SectionCard
            title="Purchase emails"
            icon={Mail}
            iconClassName="text-primary"
            actions={
              canManage ? (
                <Button variant="ghost" size="xs" asChild className="text-muted-foreground hover:text-foreground">
                  <Link href="/admin/settings">
                    <Settings /> Settings
                  </Link>
                </Button>
              ) : undefined
            }
            bodyClassName="space-y-3"
          >
            {mail === null ? (
              mailFailed ? (
                <Alert
                  tone="danger"
                  title="Could not check email"
                  action={
                    <Button variant="outline" size="xs" onClick={() => void loadMail()}>
                      Retry
                    </Button>
                  }
                />
              ) : (
                <div className="space-y-2">
                  <Skeleton className="h-4 w-2/3" />
                  <Skeleton className="h-4 w-1/2" />
                </div>
              )
            ) : mail.configured ? (
              <div className="flex items-start gap-2.5 text-[13px]">
                <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-success" aria-hidden />
                <div className="min-w-0">
                  <div className="font-medium">
                    Ready to send
                    {mail.source && mail.source !== "none" && (
                      <span className="ml-1.5 text-[11px] font-normal text-muted-foreground">
                        {mail.source === "db" ? "panel settings" : "server .env"}
                      </span>
                    )}
                  </div>
                  <div className="break-words text-xs text-muted-foreground">From {mail.from}</div>
                </div>
              </div>
            ) : (
              <Alert tone="warning" title="Email is not set up">
                {canManage ? (
                  <>
                    Add a mailbox in{" "}
                    <Link href="/admin/settings" className="font-medium text-foreground underline-offset-2 hover:underline">
                      Settings → Email
                    </Link>{" "}
                    so new purchases email their keys.
                  </>
                ) : (
                  "Ask an owner to add a mailbox in Settings so new purchases email their keys."
                )}
              </Alert>
            )}
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" size="sm" disabled={testing || !mail?.configured} onClick={() => void testMail()}>
                {testing ? <Loader2 className="animate-spin" /> : <Send />} Send me a test
              </Button>
              <Button variant="ghost" size="sm" asChild>
                <a href="/api/admin/mail-preview" target="_blank" rel="noreferrer">
                  <ExternalLink /> Preview email
                </a>
              </Button>
            </div>
            <p className="text-xs leading-5 text-muted-foreground">
              Each new purchase emails its serial and keys to the customer — untick &quot;Email the customer&quot; to skip it.
              Keys stay recoverable: open a purchase to copy them again or re-send the email.
            </p>
          </SectionCard>

          <SectionCard title="Roles" icon={ShieldCheck}>
            <dl className="space-y-2.5 text-[13px]">
              <div>
                <dt className="flex items-center gap-1.5 font-medium">
                  <Badge variant="brand">Owner</Badge>
                </dt>
                <dd className="mt-1 text-xs leading-5 text-muted-foreground">
                  Everything, including adding and removing users, roles, passwords and Settings.
                </dd>
              </div>
              <div>
                <dt className="flex items-center gap-1.5 font-medium">
                  <Badge variant="secondary">Staff</Badge>
                </dt>
                <dd className="mt-1 text-xs leading-5 text-muted-foreground">
                  Purchases, licence keys, end users and security. Can change their own name and password.
                </dd>
              </div>
            </dl>
          </SectionCard>
        </div>
      </div>

      <AddUserDialog open={addOpen} onOpenChange={setAddOpen} onDone={() => void load()} />
      <PasswordDialog
        admin={pwFor}
        self={pwFor?.email === me}
        owner={canManage}
        onOpenChange={(v) => !v && setPwFor(null)}
        onDone={() => void load()}
      />
      <RenameDialog admin={renameFor} onOpenChange={(v) => !v && setRenameFor(null)} onDone={() => void load()} />
      <RemoveDialog admin={removeFor} onOpenChange={(v) => !v && setRemoveFor(null)} onDone={() => void load()} />
    </div>
  );
}

// ---- dialogs -------------------------------------------------------------------------

function FormField({ id, label, hint, children }: { id: string; label: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id} className="text-xs">
        {label}
      </Label>
      {children}
      {hint && <p className="text-[11px] text-muted-foreground">{hint}</p>}
    </div>
  );
}

function AddUserDialog({ open, onOpenChange, onDone }: { open: boolean; onOpenChange: (v: boolean) => void; onDone: () => void }) {
  const [email, setEmail] = useState("");
  const [name, setName] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState("staff");
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (password.length < 8) {
      toast.error("Password needs at least 8 characters.");
      return;
    }
    setBusy(true);
    const res = await send("/api/admin/admins", "POST", { email, name: name || undefined, password, role });
    setBusy(false);
    if (res?.ok) {
      toast.success(`${email} can now sign in`);
      onDone();
      onOpenChange(false);
      setEmail("");
      setName("");
      setPassword("");
      setRole("staff");
    } else {
      toast.error(await errorText(res, "Could not add the user. Check the fields."));
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <form onSubmit={submit} className="grid gap-4">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <UserPlus className="h-4 w-4 text-primary" /> Add a panel user
            </DialogTitle>
            <DialogDescription>They sign in with this email and password. Share the password with them directly.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-3 sm:grid-cols-2">
            <FormField id="u-name" label="Name">
              <Input id="u-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Priya Shah" autoFocus maxLength={80} />
            </FormField>
            <FormField id="u-email" label="Email">
              <Input
                id="u-email"
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="priya@text-soft.com"
                autoComplete="off"
              />
            </FormField>
          </div>
          <FormField id="u-pass" label="Password" hint="At least 8 characters. Shown here so you can pass it on.">
            <Input
              id="u-pass"
              type="text"
              required
              minLength={8}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="At least 8 characters"
              className="font-mono text-xs"
              autoComplete="off"
              spellCheck={false}
            />
          </FormField>
          <FormField id="u-role" label="Role">
            <Select value={role} onValueChange={setRole}>
              <SelectTrigger id="u-role">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="staff">Staff — purchases, keys, users, security</SelectItem>
                <SelectItem value="owner">Owner — everything, including Team and Settings</SelectItem>
              </SelectContent>
            </Select>
          </FormField>
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={busy}>
              {busy && <Loader2 className="animate-spin" />} Add user
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function PasswordDialog({
  admin,
  self,
  owner,
  onOpenChange,
  onDone,
}: {
  admin: Admin | null;
  self: boolean;
  owner: boolean;
  onOpenChange: (v: boolean) => void;
  onDone: () => void;
}) {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [busy, setBusy] = useState(false);
  const needCurrent = self || !owner;

  useEffect(() => {
    if (!admin) {
      setCurrent("");
      setNext("");
    }
  }, [admin]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!admin) return;
    if (next.length < 8) {
      toast.error("The new password needs at least 8 characters.");
      return;
    }
    setBusy(true);
    const res = await send(`/api/admin/admins/${admin.id}`, "PATCH", {
      action: "setPassword",
      password: next,
      currentPassword: needCurrent ? current : undefined,
    });
    setBusy(false);
    if (res?.ok) {
      toast.success(self ? "Your password was changed" : `Password reset for ${admin.email}`);
      onDone();
      onOpenChange(false);
    } else {
      toast.error(await errorText(res, "Could not change the password."));
    }
  }

  return (
    <Dialog open={!!admin} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <form onSubmit={submit} className="grid gap-4">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <KeyRound className="h-4 w-4 text-primary" />
              {self ? "Change your password" : "Reset password"}
            </DialogTitle>
            <DialogDescription>
              {self ? "You stay signed in on this device." : <>For {admin?.email}. Tell them the new password directly.</>}
            </DialogDescription>
          </DialogHeader>
          {needCurrent && (
            <FormField id="p-cur" label="Current password">
              <Input id="p-cur" type="password" required value={current} onChange={(e) => setCurrent(e.target.value)} autoFocus autoComplete="current-password" />
            </FormField>
          )}
          <FormField id="p-new" label="New password" hint="At least 8 characters.">
            <Input
              id="p-new"
              type="text"
              required
              minLength={8}
              value={next}
              onChange={(e) => setNext(e.target.value)}
              className="font-mono text-xs"
              placeholder="At least 8 characters"
              autoFocus={!needCurrent}
              autoComplete="new-password"
              spellCheck={false}
            />
          </FormField>
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={busy}>
              {busy && <Loader2 className="animate-spin" />} Save password
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function RenameDialog({ admin, onOpenChange, onDone }: { admin: Admin | null; onOpenChange: (v: boolean) => void; onDone: () => void }) {
  const [name, setName] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setName(admin?.name ?? "");
  }, [admin]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!admin) return;
    setBusy(true);
    const res = await send(`/api/admin/admins/${admin.id}`, "PATCH", { action: "rename", name: name.trim() });
    setBusy(false);
    if (res?.ok) {
      toast.success(name.trim() ? "Name saved" : "Name removed");
      onDone();
      onOpenChange(false);
    } else {
      toast.error(await errorText(res, "Could not save the name."));
    }
  }

  return (
    <Dialog open={!!admin} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-sm">
        <form onSubmit={submit} className="grid gap-4">
          <DialogHeader>
            <DialogTitle>Display name</DialogTitle>
            <DialogDescription>For {admin?.email}. Leave blank to show the email instead.</DialogDescription>
          </DialogHeader>
          <FormField id="r-name" label="Name">
            <Input id="r-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Priya Shah" maxLength={80} autoFocus />
          </FormField>
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={busy}>
              {busy && <Loader2 className="animate-spin" />} Save
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function RemoveDialog({ admin, onOpenChange, onDone }: { admin: Admin | null; onOpenChange: (v: boolean) => void; onDone: () => void }) {
  const [busy, setBusy] = useState(false);

  async function remove() {
    if (!admin) return;
    setBusy(true);
    const res = await send(`/api/admin/admins/${admin.id}`, "DELETE");
    setBusy(false);
    if (res?.ok) {
      toast.success(`${admin.email} removed`);
      onDone();
      onOpenChange(false);
    } else {
      toast.error(await errorText(res, "Could not remove that user."));
    }
  }

  return (
    <Dialog open={!!admin} onOpenChange={(v) => !busy && onOpenChange(v)}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Remove {admin?.email}?</DialogTitle>
          <DialogDescription>
            They will no longer be able to sign in to the panel. Their past actions stay in the audit log. To pause access
            instead, use Disable sign-in.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button type="button" variant="ghost" onClick={() => onOpenChange(false)} disabled={busy}>
            Cancel
          </Button>
          <Button type="button" variant="destructive" onClick={() => void remove()} disabled={busy}>
            {busy ? <Loader2 className="animate-spin" /> : <Trash2 />} Remove user
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

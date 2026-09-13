"use client";

import { useCallback, useEffect, useState } from "react";
import { toast } from "sonner";
import { Plus, UserPlus, KeyRound, Ban, RotateCcw, Trash2, Mail, Send, ShieldCheck, Loader2 } from "lucide-react";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { fmtDateTime, fmtRelative } from "@/lib/utils";

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

export default function TeamPage() {
  const [rows, setRows] = useState<Admin[] | null>(null);
  const [me, setMe] = useState("");
  const [canManage, setCanManage] = useState(false);
  const [busy, setBusy] = useState(false);
  const [addOpen, setAddOpen] = useState(false);
  const [pwFor, setPwFor] = useState<Admin | null>(null);
  const [mail, setMail] = useState<{ configured: boolean; from: string } | null>(null);

  const load = useCallback(() => {
    fetch("/api/admin/admins")
      .then((r) => r.json())
      .then((d) => {
        setRows(d.admins ?? []);
        setMe(d.me ?? "");
        setCanManage(!!d.canManage);
      })
      .catch(() => setRows([]));
    fetch("/api/admin/mail-test")
      .then((r) => r.json())
      .then(setMail)
      .catch(() => {});
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function patch(id: string, body: Record<string, unknown>, success: string) {
    setBusy(true);
    const res = await fetch(`/api/admin/admins/${id}`, {
      method: "PATCH",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body),
    });
    setBusy(false);
    if (res.ok) toast.success(success);
    else {
      const e = await res.json().catch(() => ({}));
      toast.error(
        e.error === "wrong_password"
          ? "Current password is wrong."
          : e.error === "last_owner"
            ? "You are the owner — make someone else owner first."
            : "That didn't work.",
      );
    }
    load();
  }

  async function remove(a: Admin) {
    if (!confirm(`Remove ${a.email} from the panel? They will not be able to sign in.`)) return;
    setBusy(true);
    const res = await fetch(`/api/admin/admins/${a.id}`, { method: "DELETE" });
    setBusy(false);
    if (res.ok) toast.success(`${a.email} removed`);
    else toast.error("Could not remove that user.");
    load();
  }

  async function testMail() {
    setBusy(true);
    const res = await fetch("/api/admin/mail-test", { method: "POST" });
    const d = await res.json().catch(() => ({}));
    setBusy(false);
    if (res.ok) toast.success(`Test email sent to ${d.to}`);
    else toast.error(`Email failed: ${d.detail ?? d.error}`);
  }

  return (
    <div>
      <PageHeader title="Team" description="Who can sign in to this panel, and the mailbox purchase emails go out from.">
        {canManage && (
          <Button onClick={() => setAddOpen(true)}>
            <Plus /> Add user
          </Button>
        )}
      </PageHeader>

      <div className="grid gap-6 lg:grid-cols-[1.6fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Panel users</CardTitle>
            <CardDescription>
              Owners can add or remove users and reset passwords. Staff can do everything else.
            </CardDescription>
          </CardHeader>
          <CardContent className="p-0">
            {rows === null ? (
              <div className="space-y-3 p-6">
                <Skeleton className="h-5 w-full" />
                <Skeleton className="h-5 w-2/3" />
              </div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>User</TableHead>
                    <TableHead>Role</TableHead>
                    <TableHead className="hidden whitespace-nowrap md:table-cell">Last sign-in</TableHead>
                    <TableHead />
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((a) => {
                    const self = a.email === me;
                    return (
                      <TableRow key={a.id}>
                        <TableCell>
                          <div className="font-medium">
                            {a.name || a.email}
                            {self && <span className="ml-2 text-xs text-muted-foreground">(you)</span>}
                          </div>
                          {a.name && <div className="text-xs text-muted-foreground">{a.email}</div>}
                          {a.status === "disabled" && (
                            <Badge variant="destructive" className="mt-1">
                              Disabled
                            </Badge>
                          )}
                        </TableCell>
                        <TableCell>
                          {canManage && !self ? (
                            <Select value={a.role} onValueChange={(v) => patch(a.id, { action: "setRole", role: v }, "Role updated")}>
                              <SelectTrigger className="h-8 w-28">
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="owner">Owner</SelectItem>
                                <SelectItem value="staff">Staff</SelectItem>
                              </SelectContent>
                            </Select>
                          ) : (
                            <Badge variant={a.role === "owner" ? "brand" : "secondary"}>{a.role === "owner" ? "Owner" : "Staff"}</Badge>
                          )}
                        </TableCell>
                        <TableCell className="hidden text-muted-foreground md:table-cell">
                          {a.lastLoginUtc ? fmtRelative(a.lastLoginUtc) : "never"}
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-1">
                            {(canManage || self) && (
                              <Button variant="ghost" size="sm" disabled={busy} onClick={() => setPwFor(a)} title="Change password">
                                <KeyRound /> Password
                              </Button>
                            )}
                            {canManage && !self && a.status !== "disabled" && (
                              <Button variant="ghost" size="sm" disabled={busy} onClick={() => patch(a.id, { action: "disable" }, `${a.email} disabled`)}>
                                <Ban /> Disable
                              </Button>
                            )}
                            {canManage && !self && a.status === "disabled" && (
                              <Button variant="ghost" size="sm" disabled={busy} onClick={() => patch(a.id, { action: "enable" }, `${a.email} enabled`)}>
                                <RotateCcw /> Enable
                              </Button>
                            )}
                            {canManage && !self && (
                              <Button variant="ghost" size="sm" className="text-destructive" disabled={busy} onClick={() => remove(a)}>
                                <Trash2 />
                              </Button>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Mail className="h-4 w-4 text-primary" /> Purchase emails
            </CardTitle>
            <CardDescription>Every new purchase emails the serial and keys to the customer.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 text-sm">
            {mail === null ? (
              <Skeleton className="h-5 w-2/3" />
            ) : mail.configured ? (
              <div className="flex items-start gap-2 rounded-lg bg-success/10 p-3 text-success">
                <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" />
                <div>
                  Sending as <b>{mail.from}</b>.
                </div>
              </div>
            ) : (
              <div className="rounded-lg border border-warning/40 bg-warning/10 p-3 text-warning">
                Email is not set up. Add <code>SMTP_USER</code> and <code>SMTP_PASS</code> (a Gmail app password) to the
                server&apos;s <code>.env</code> and restart.
              </div>
            )}
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" disabled={busy || !mail?.configured} onClick={testMail}>
                {busy ? <Loader2 className="animate-spin" /> : <Send />} Send me a test email
              </Button>
              <Button variant="ghost" asChild>
                <a href="/api/admin/mail-preview" target="_blank" rel="noreferrer">
                  <Mail /> Preview the purchase email
                </a>
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">
              Keys are shown once and never stored, so the email is sent at the moment a purchase is created. Untick
              &quot;Email the customer&quot; in the purchase dialog to skip it.
            </p>
          </CardContent>
        </Card>
      </div>

      <AddUserDialog open={addOpen} onOpenChange={setAddOpen} onDone={load} />
      <PasswordDialog admin={pwFor} self={pwFor?.email === me} owner={canManage} onOpenChange={(v) => !v && setPwFor(null)} onDone={load} />
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
    const res = await fetch("/api/admin/admins", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ email, name: name || undefined, password, role }),
    });
    setBusy(false);
    if (res.ok) {
      toast.success(`${email} can now sign in`);
      onDone();
      onOpenChange(false);
      setEmail("");
      setName("");
      setPassword("");
      setRole("staff");
    } else {
      const d = await res.json().catch(() => ({}));
      toast.error(d.error === "exists" ? "That email already has access." : "Could not add the user. Check the fields.");
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <UserPlus className="h-4 w-4" /> Add a panel user
            </DialogTitle>
            <DialogDescription>They sign in with this email and password. Share the password with them directly.</DialogDescription>
          </DialogHeader>
          <div className="mt-5 grid gap-4">
            <div className="space-y-2">
              <Label htmlFor="u-name">Name</Label>
              <Input id="u-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Priya Shah" autoFocus />
            </div>
            <div className="space-y-2">
              <Label htmlFor="u-email">Email</Label>
              <Input id="u-email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="priya@text-soft.com" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="u-pass">Password</Label>
              <Input id="u-pass" type="text" required minLength={8} value={password} onChange={(e) => setPassword(e.target.value)} placeholder="At least 8 characters" className="font-mono" />
            </div>
            <div className="space-y-2">
              <Label>Role</Label>
              <Select value={role} onValueChange={setRole}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="staff">Staff — purchases, keys, users, security</SelectItem>
                  <SelectItem value="owner">Owner — everything, including this page</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter className="mt-6">
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

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!admin) return;
    setBusy(true);
    const res = await fetch(`/api/admin/admins/${admin.id}`, {
      method: "PATCH",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ action: "setPassword", password: next, currentPassword: needCurrent ? current : undefined }),
    });
    setBusy(false);
    if (res.ok) {
      toast.success("Password changed");
      onDone();
      onOpenChange(false);
      setCurrent("");
      setNext("");
    } else {
      const d = await res.json().catch(() => ({}));
      toast.error(d.error === "wrong_password" ? "Current password is wrong." : "Could not change the password.");
    }
  }

  return (
    <Dialog open={!!admin} onOpenChange={onOpenChange}>
      <DialogContent>
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle>{self ? "Change your password" : `Reset password for ${admin?.email}`}</DialogTitle>
            <DialogDescription>{self ? "You stay signed in on this device." : "Tell them the new password directly."}</DialogDescription>
          </DialogHeader>
          <div className="mt-5 grid gap-4">
            {needCurrent && (
              <div className="space-y-2">
                <Label htmlFor="p-cur">Current password</Label>
                <Input id="p-cur" type="password" required value={current} onChange={(e) => setCurrent(e.target.value)} autoFocus />
              </div>
            )}
            <div className="space-y-2">
              <Label htmlFor="p-new">New password</Label>
              <Input id="p-new" type="text" required minLength={8} value={next} onChange={(e) => setNext(e.target.value)} className="font-mono" placeholder="At least 8 characters" />
            </div>
          </div>
          <DialogFooter className="mt-6">
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

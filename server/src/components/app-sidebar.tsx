"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  LayoutDashboard,
  ShoppingBag,
  KeyRound,
  Users,
  ShieldAlert,
  UserCog,
  Settings,
  LogOut,
  Menu,
  ChevronsUpDown,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetTrigger, SheetTitle } from "@/components/ui/sheet";
import { ThemeToggle } from "@/components/theme-toggle";
import { Logo } from "@/components/logo";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

/**
 * AppSidebar — fixed w-60 dark rail on desktop (lg+), a top bar + sheet on phones.
 * Props: email (shown in the user menu), role ("owner" | "staff"; Settings is owner-only).
 * The admin layout offsets its content with lg:pl-60 to match.
 */

interface NavItem {
  href: string;
  label: string;
  icon: React.ElementType;
  exact?: boolean;
  ownerOnly?: boolean;
}

const GROUPS: Array<{ label: string; items: NavItem[] }> = [
  { label: "Overview", items: [{ href: "/admin", label: "Dashboard", icon: LayoutDashboard, exact: true }] },
  {
    label: "Sales",
    items: [
      { href: "/admin/orders", label: "Purchases", icon: ShoppingBag },
      { href: "/admin/keys", label: "Licence keys", icon: KeyRound },
    ],
  },
  {
    label: "Customers",
    items: [
      { href: "/admin/users", label: "End users", icon: Users },
      { href: "/admin/security", label: "Security", icon: ShieldAlert },
    ],
  },
  {
    label: "Admin",
    items: [
      { href: "/admin/team", label: "Team", icon: UserCog },
      { href: "/admin/settings", label: "Settings", icon: Settings, ownerOnly: true },
    ],
  },
];

function isActive(pathname: string, item: NavItem): boolean {
  if (item.exact) return pathname === item.href;
  return pathname === item.href || pathname.startsWith(item.href + "/");
}

function NavLinks({ role, onNavigate }: { role: string; onNavigate?: () => void }) {
  const pathname = usePathname() ?? "";
  return (
    <nav className="flex flex-col gap-4" aria-label="Main">
      {GROUPS.map((group) => {
        const items = group.items.filter((i) => !i.ownerOnly || role === "owner");
        if (items.length === 0) return null;
        return (
          <div key={group.label}>
            <div className="px-3 pb-1 text-[10px] font-semibold uppercase tracking-[0.14em] text-sidebar-foreground/55">
              {group.label}
            </div>
            <ul className="flex flex-col gap-0.5">
              {items.map((item) => {
                const active = isActive(pathname, item);
                const Icon = item.icon;
                return (
                  <li key={item.href}>
                    <Link
                      href={item.href}
                      onClick={onNavigate}
                      aria-current={active ? "page" : undefined}
                      className={cn(
                        "group relative flex h-9 items-center gap-2.5 rounded-md px-3 text-[13px] font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-primary/70",
                        active
                          ? "bg-sidebar-accent text-sidebar-accent-foreground"
                          : "text-sidebar-foreground/65 hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground",
                      )}
                    >
                      {active && (
                        <span className="absolute left-0 top-1/2 h-4 w-[3px] -translate-y-1/2 rounded-r-full bg-sidebar-primary" aria-hidden />
                      )}
                      <Icon
                        className={cn(
                          "h-4 w-4 shrink-0 transition-colors",
                          active ? "text-sidebar-primary" : "text-sidebar-foreground/55 group-hover:text-sidebar-foreground/80",
                        )}
                        aria-hidden
                      />
                      <span className="truncate">{item.label}</span>
                    </Link>
                  </li>
                );
              })}
            </ul>
          </div>
        );
      })}
    </nav>
  );
}

function roleLabel(role: string): string {
  if (role === "owner") return "Owner";
  if (role === "staff") return "Staff";
  return role ? role.charAt(0).toUpperCase() + role.slice(1) : "Administrator";
}

function UserMenu({ email, role }: { email: string; role: string }) {
  const router = useRouter();
  async function logout() {
    await fetch("/api/logout", { method: "POST" });
    router.push("/login");
    router.refresh();
  }
  const initial = (email.charAt(0) || "?").toUpperCase();
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          className="flex h-11 w-full items-center gap-2.5 rounded-md px-2 text-left transition-colors hover:bg-sidebar-accent/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-primary/70"
          aria-label="Account menu"
        >
          <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-sidebar-primary text-xs font-semibold text-sidebar">
            {initial}
          </span>
          <span className="min-w-0 flex-1">
            <span className="block truncate text-[13px] font-medium text-sidebar-foreground">{email}</span>
            <span className="block text-[11px] text-sidebar-foreground/60">{roleLabel(role)}</span>
          </span>
          <ChevronsUpDown className="h-3.5 w-3.5 shrink-0 text-sidebar-foreground/40" aria-hidden />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" side="top" className="w-[var(--radix-dropdown-menu-trigger-width)] min-w-[13rem]">
        <DropdownMenuLabel className="font-normal">
          <div className="truncate text-[13px] font-medium">{email}</div>
          <div className="text-[11px] text-muted-foreground">{roleLabel(role)}</div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link href="/admin/team">
            <UserCog /> Team &amp; password
          </Link>
        </DropdownMenuItem>
        {role === "owner" && (
          <DropdownMenuItem asChild>
            <Link href="/admin/settings">
              <Settings /> Settings
            </Link>
          </DropdownMenuItem>
        )}
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={logout} className="text-destructive focus:text-destructive">
          <LogOut /> Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function SidebarBody({ email, role, onNavigate }: { email: string; role: string; onNavigate?: () => void }) {
  return (
    <div className="flex h-full flex-col bg-sidebar text-sidebar-foreground">
      <div className="flex h-14 shrink-0 items-center border-b border-sidebar-border px-4">
        <Link href="/admin" onClick={onNavigate} className="rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-primary/70">
          <Logo size={28} tone="sidebar" caption="Licence admin" />
        </Link>
      </div>
      <div className="flex-1 overflow-y-auto px-3 py-4">
        <NavLinks role={role} onNavigate={onNavigate} />
      </div>
      <div className="shrink-0 border-t border-sidebar-border p-2">
        <UserMenu email={email} role={role} />
      </div>
    </div>
  );
}

/** Desktop: fixed left rail. Mobile: top bar with a hamburger that opens a sheet. */
export function AppSidebar({ email, role = "staff" }: { email: string; role?: string }) {
  const [open, setOpen] = React.useState(false);
  return (
    <>
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-60 border-r border-sidebar-border lg:block">
        <SidebarBody email={email} role={role} />
      </aside>

      <header className="sticky top-0 z-30 flex h-12 items-center gap-2 border-b bg-background/85 px-3 backdrop-blur supports-[backdrop-filter]:bg-background/70 lg:hidden">
        <Sheet open={open} onOpenChange={setOpen}>
          <SheetTrigger asChild>
            <Button variant="ghost" size="icon" aria-label="Open menu" title="Open menu">
              <Menu className="h-5 w-5" />
            </Button>
          </SheetTrigger>
          <SheetContent side="left" className="w-60 border-sidebar-border bg-sidebar p-0 [&>button]:text-sidebar-foreground">
            <SheetTitle className="sr-only">Navigation</SheetTitle>
            <SidebarBody email={email} role={role} onNavigate={() => setOpen(false)} />
          </SheetContent>
        </Sheet>
        <Link href="/admin" className="rounded-md">
          <Logo size={24} caption={null} />
        </Link>
        <div className="ml-auto">
          <ThemeToggle />
        </div>
      </header>
    </>
  );
}

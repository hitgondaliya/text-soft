"use client";

import Link from "next/link";
import Image from "next/image";
import { usePathname, useRouter } from "next/navigation";
import { LayoutDashboard, ShoppingBag, KeyRound, LogOut, Menu, ChevronUp } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetTrigger, SheetTitle } from "@/components/ui/sheet";
import { ThemeToggle } from "@/components/theme-toggle";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

const NAV = [
  { href: "/admin", label: "Dashboard", icon: LayoutDashboard, exact: true },
  { href: "/admin/orders", label: "Purchases", icon: ShoppingBag },
  { href: "/admin/keys", label: "License keys", icon: KeyRound },
];

function NavLinks({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();
  return (
    <nav className="flex flex-col gap-1">
      {NAV.map(({ href, label, icon: Icon, exact }) => {
        const active = exact ? pathname === href : pathname.startsWith(href);
        return (
          <Link
            key={href}
            href={href}
            onClick={onNavigate}
            className={cn(
              "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
              active
                ? "bg-sidebar-accent text-sidebar-accent-foreground shadow-sm"
                : "text-sidebar-foreground/70 hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground",
            )}
          >
            <Icon className={cn("h-4 w-4", active && "text-primary")} />
            {label}
          </Link>
        );
      })}
    </nav>
  );
}

function UserMenu({ email }: { email: string }) {
  const router = useRouter();
  async function logout() {
    await fetch("/api/logout", { method: "POST" });
    router.push("/login");
    router.refresh();
  }
  const initial = email.charAt(0).toUpperCase();
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button className="flex w-full items-center gap-3 rounded-md px-2 py-2 text-left text-sm transition-colors hover:bg-sidebar-accent/60">
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary text-xs font-bold text-primary-foreground">
            {initial}
          </span>
          <span className="min-w-0 flex-1">
            <span className="block truncate text-sidebar-foreground">{email}</span>
            <span className="block text-[11px] text-sidebar-foreground/50">Administrator</span>
          </span>
          <ChevronUp className="h-4 w-4 text-sidebar-foreground/50" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" side="top" className="w-56">
        <DropdownMenuLabel className="truncate font-normal text-muted-foreground">{email}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={logout} className="text-destructive focus:text-destructive">
          <LogOut /> Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function SidebarBody({ email, onNavigate }: { email: string; onNavigate?: () => void }) {
  return (
    <div className="flex h-full flex-col bg-sidebar text-sidebar-foreground">
      <div className="flex items-center gap-3 px-5 py-5">
        <Image src="/logo.webp" alt="TextSoft" width={36} height={36} priority />
        <div className="leading-tight">
          <div className="text-[17px] font-bold tracking-tight text-white">
            Text<span className="text-primary">Soft</span>
          </div>
          <div className="text-[11px] font-medium uppercase tracking-[0.18em] text-sidebar-foreground/50">Licenses</div>
        </div>
      </div>
      <div className="px-3 pt-2">
        <NavLinks onNavigate={onNavigate} />
      </div>
      <div className="mt-auto border-t border-sidebar-border p-3">
        <UserMenu email={email} />
      </div>
    </div>
  );
}

/** Desktop: fixed left rail. Mobile: top bar with a hamburger that opens a sheet. */
export function AppSidebar({ email }: { email: string }) {
  return (
    <>
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-64 border-r border-sidebar-border lg:block">
        <SidebarBody email={email} />
      </aside>

      <header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b bg-background/80 px-4 backdrop-blur lg:hidden">
        <Sheet>
          <SheetTrigger asChild>
            <Button variant="ghost" size="icon" aria-label="Open menu">
              <Menu className="h-5 w-5" />
            </Button>
          </SheetTrigger>
          <SheetContent side="left" className="w-72 p-0">
            <SheetTitle className="sr-only">Navigation</SheetTitle>
            <SidebarBody email={email} />
          </SheetContent>
        </Sheet>
        <Image src="/logo.webp" alt="TextSoft" width={28} height={28} />
        <span className="font-bold tracking-tight">
          Text<span className="text-primary">Soft</span>
        </span>
        <div className="ml-auto">
          <ThemeToggle />
        </div>
      </header>
    </>
  );
}

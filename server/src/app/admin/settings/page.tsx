import type { Metadata } from "next";
import Link from "next/link";
import { ArrowLeft, Lock, Server } from "lucide-react";
import { getSession } from "@/lib/auth";
import { env } from "@/lib/env";
import { PageHeader } from "@/components/page-header";
import { SectionCard } from "@/components/section-card";
import { EmptyState } from "@/components/empty-state";
import { KvList } from "@/components/kv-list";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { SmtpSettings } from "./smtp-settings";
import { AuditLog } from "./audit-log";

// Settings — owner only. Email (SMTP) is edited in the client form; the "Server"
// card is read-only and rendered here, on the server, from the process env. The
// activity log below shows who revealed keys and changed the team or the settings.

export const metadata: Metadata = { title: "Settings" };
export const dynamic = "force-dynamic";

export default async function SettingsPage() {
  const session = await getSession();

  if (!session || session.role !== "owner") {
    return (
      <>
        <PageHeader title="Settings" description="Email delivery and server details for this panel." />
        <SectionCard>
          <EmptyState
            icon={Lock}
            tone="primary"
            title="Only the owner can change settings"
            description="Email delivery and server settings are managed by the panel owner. Ask an owner if purchase emails should go out from a different mailbox."
            action={
              <Button variant="outline" size="sm" asChild>
                <Link href="/admin">
                  <ArrowLeft /> Back to dashboard
                </Link>
              </Button>
            }
          />
        </SectionCard>
      </>
    );
  }

  return (
    <>
      <PageHeader
        title="Settings"
        description="Where purchase emails are sent from, how this server is set up, and who did what. Owners only."
      />
      <SmtpSettings email={session.email} aside={<ServerCard />} />
      <div className="mt-4">
        <AuditLog />
      </div>
    </>
  );
}

/** Read-only facts about this deployment. Values come from the server's .env. */
function ServerCard() {
  const base = env.appBaseUrl();
  const port = env.port();
  const ownVault = env.keyVaultSecret().trim() !== "";
  const production = process.env.NODE_ENV === "production";
  const localUrl = /\/\/(localhost|127\.0\.0\.1|\[::1\])(:|\/|$)/i.test(base);

  return (
    <SectionCard title="Server" icon={Server} description="Read-only. Change these in the server's .env and restart.">
      <KvList
        labelWidth="5.5rem"
        items={[
          { label: "Panel URL", value: base, mono: true, copy: base },
          {
            label: "Port",
            value: (
              <span>
                <span className="font-mono text-xs">{port}</span>
                <span className="ml-1.5 text-xs text-muted-foreground">PORT in .env</span>
              </span>
            ),
          },
          {
            label: "Key vault",
            value: ownVault ? (
              <Badge variant="success">KEY_VAULT_SECRET set</Badge>
            ) : (
              <Badge variant="warning">Derived from COOKIE_SECRET</Badge>
            ),
          },
          { label: "Mode", value: production ? "Production" : "Development" },
        ]}
      />
      {(!ownVault || (production && localUrl)) && (
        <ul className="mt-3 space-y-1.5 border-t pt-3 text-[11px] leading-4 text-muted-foreground">
          {production && localUrl && (
            <li>
              <span className="font-medium text-warning">APP_BASE_URL points at this machine.</span> Customers will not see
              the logo in purchase emails. Set it to the panel&apos;s public address.
            </li>
          )}
          {!ownVault && (
            <li>
              Set <code className="font-mono">KEY_VAULT_SECRET</code> in .env (<code className="whitespace-nowrap font-mono">openssl rand -base64 32</code>)
              so saved keys and the SMTP password stay readable if COOKIE_SECRET ever changes.
            </li>
          )}
        </ul>
      )}
    </SectionCard>
  );
}

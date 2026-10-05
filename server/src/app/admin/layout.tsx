import { redirect } from "next/navigation";
import { getSession } from "@/lib/auth";
import { AppSidebar } from "@/components/app-sidebar";

// Tab title for the dashboard (a client page). The root template is applied to
// `default`; `template` is passed on to every section below (a plain string title
// in a layout would drop the suffix for its children).
export const metadata = { title: { default: "Dashboard", template: "%s · TextSoft Licences" } };

// Shell: fixed w-60 sidebar on lg+, content offset to match. Minimal padding and a
// wide, left-aligned content column so tables and the stat strip get the room.
export default async function AdminLayout({ children }: { children: React.ReactNode }) {
  // The middleware only checks the cookie's signature. getSession() also checks the
  // account still exists and is enabled; if not, the middleware clears the cookie on
  // this login URL (otherwise it would bounce a signed cookie straight back here).
  const session = await getSession();
  if (!session) redirect("/login?reason=signed_out");
  return (
    <div className="min-h-screen">
      <AppSidebar email={session.email} role={session.role} />
      <main className="lg:pl-60">
        <div className="w-full max-w-[1600px] px-4 py-4 lg:px-6 lg:py-5">{children}</div>
      </main>
    </div>
  );
}

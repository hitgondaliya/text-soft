import { getSession } from "@/lib/auth";
import { AppSidebar } from "@/components/app-sidebar";

export default async function AdminLayout({ children }: { children: React.ReactNode }) {
  const session = await getSession();
  return (
    <div className="min-h-screen">
      <AppSidebar email={session?.email ?? ""} />
      <main className="lg:pl-64">
        <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8 animate-fade-up">{children}</div>
      </main>
    </div>
  );
}

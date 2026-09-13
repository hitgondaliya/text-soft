import Link from "next/link";
import { getSession } from "@/lib/auth";
import LogoutButton from "./LogoutButton";

export default async function AdminLayout({ children }: { children: React.ReactNode }) {
  const session = await getSession();

  return (
    <div style={{ display: "grid", gridTemplateColumns: "220px 1fr", minHeight: "100vh" }}>
      <aside
        style={{
          background: "var(--surface)",
          borderRight: "1px solid var(--border)",
          padding: "20px 16px",
          display: "flex",
          flexDirection: "column",
        }}
      >
        <div style={{ fontSize: 19, fontWeight: 700, marginBottom: 4 }}>
          Text<span style={{ color: "var(--brand)" }}>Soft</span>
        </div>
        <div className="muted" style={{ fontSize: 12, marginBottom: 24 }}>Licenses</div>

        <nav style={{ display: "flex", flexDirection: "column", gap: 4 }}>
          <Link href="/admin" style={{ color: "var(--text)", padding: "8px 10px", borderRadius: 8 }}>
            Dashboard
          </Link>
          <Link href="/admin/orders" style={{ color: "var(--text)", padding: "8px 10px", borderRadius: 8 }}>
            Purchases
          </Link>
          <Link href="/admin/keys" style={{ color: "var(--text)", padding: "8px 10px", borderRadius: 8 }}>
            License keys
          </Link>
        </nav>

        <div style={{ marginTop: "auto" }}>
          <div className="muted" style={{ fontSize: 12, marginBottom: 8 }}>{session?.email}</div>
          <LogoutButton />
        </div>
      </aside>

      <main style={{ padding: "28px 32px", maxWidth: 1100 }}>{children}</main>
    </div>
  );
}

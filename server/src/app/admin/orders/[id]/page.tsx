"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";

interface KeyRow {
  id: string;
  seq: number;
  prefix: string;
  status: string;
  expired: boolean;
  deviceLimit: number;
  devicesUsed: number;
  lastSeenUtc: string | null;
}

interface Order {
  id: string;
  serial: string;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  company: string;
  edition: string;
  deviceLimit: number;
  quantity: number;
  expiryUtc: string | null;
  expired: boolean;
  note: string;
  createdBy: string;
  createdUtc: string;
  keys: KeyRow[];
}

export default function OrderDetail({ params }: { params: { id: string } }) {
  const [o, setO] = useState<Order | null>(null);
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState("");

  const load = useCallback(() => {
    fetch(`/api/admin/orders/${params.id}`)
      .then((r) => (r.ok ? r.json() : null))
      .then(setO)
      .catch(() => {});
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  async function act(body: Record<string, unknown>, confirmText?: string) {
    if (confirmText && !confirm(confirmText)) return;
    setBusy(true);
    setMsg("");
    const res = await fetch(`/api/admin/orders/${params.id}`, {
      method: "PATCH",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body),
    });
    setBusy(false);
    setMsg(res.ok ? "Saved." : "That did not work.");
    load();
  }

  if (!o) return <div className="muted">Loading…</div>;

  const revokedCount = o.keys.filter((k) => k.status === "revoked").length;
  const inUse = o.keys.filter((k) => k.devicesUsed > 0).length;

  return (
    <div>
      <Link href="/admin/orders" className="muted">← Purchases</Link>
      <h1 style={{ marginTop: 8 }}>
        <span className="mono">{o.serial}</span>
      </h1>

      <div className="grid" style={{ gridTemplateColumns: "repeat(4, 1fr)", marginBottom: 20 }}>
        <div className="card">
          <div style={{ fontSize: 28, fontWeight: 700 }}>{o.keys.length}</div>
          <div className="muted">Keys issued</div>
        </div>
        <div className="card">
          <div style={{ fontSize: 28, fontWeight: 700, color: "var(--brand)" }}>{inUse}</div>
          <div className="muted">Keys in use</div>
        </div>
        <div className="card">
          <div style={{ fontSize: 28, fontWeight: 700, color: "var(--danger)" }}>{revokedCount}</div>
          <div className="muted">Revoked</div>
        </div>
        <div className="card">
          <div style={{ fontSize: 28, fontWeight: 700 }}>{o.deviceLimit}</div>
          <div className="muted">Devices per key</div>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 20 }}>
        <h3 style={{ marginTop: 0 }}>Customer</h3>
        <table>
          <tbody>
            <tr><td className="muted">Name</td><td>{o.customerName}</td></tr>
            <tr><td className="muted">Company</td><td>{o.company || "—"}</td></tr>
            <tr><td className="muted">Email</td><td>{o.customerEmail || "—"}</td></tr>
            <tr><td className="muted">Phone</td><td>{o.customerPhone || "—"}</td></tr>
            <tr><td className="muted">Note</td><td>{o.note || "—"}</td></tr>
            <tr><td className="muted">Edition</td><td><span className={`badge ${o.edition}`}>{o.edition}</span></td></tr>
            <tr>
              <td className="muted">Expiry</td>
              <td>
                {o.expiryUtc ? new Date(o.expiryUtc).toLocaleDateString() : "perpetual"}
                {o.expired && <span className="badge expired" style={{ marginLeft: 6 }}>expired</span>}
              </td>
            </tr>
            <tr>
              <td className="muted">Sold</td>
              <td>{new Date(o.createdUtc).toLocaleString()} by {o.createdBy}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div className="card" style={{ marginBottom: 20 }}>
        <h3 style={{ marginTop: 0 }}>Whole purchase</h3>
        <p className="muted" style={{ marginTop: 0 }}>
          These act on all {o.keys.length} keys under this serial at once.
        </p>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          <button disabled={busy} onClick={() => act({ action: "extendAll", days: 365 })}>
            Extend 1 year
          </button>
          <button disabled={busy} onClick={() => act({ action: "extendAll", days: 30 })}>
            Extend 30 days
          </button>
          <button
            className="secondary"
            disabled={busy}
            onClick={() => act({ action: "setExpiryAll", expiryUtc: null })}
          >
            Make perpetual
          </button>
          <button
            className="danger"
            disabled={busy}
            onClick={() =>
              act(
                { action: "revokeAll" },
                `Revoke all ${o.keys.length} keys of ${o.serial}? Every device using them stops working.`,
              )
            }
          >
            Revoke all
          </button>
          <button className="secondary" disabled={busy} onClick={() => act({ action: "unrevokeAll" })}>
            Un-revoke all
          </button>
          {msg && <span className="muted" style={{ alignSelf: "center" }}>{msg}</span>}
        </div>
      </div>

      <div className="card" style={{ padding: 0 }}>
        <table>
          <thead>
            <tr>
              <th>#</th>
              <th>Key</th>
              <th>Status</th>
              <th>Devices</th>
              <th>Last seen</th>
            </tr>
          </thead>
          <tbody>
            {o.keys.map((k) => (
              <tr key={k.id}>
                <td className="muted">{k.seq}</td>
                <td className="mono">
                  <Link href={`/admin/keys/${k.id}`}>{k.prefix}…</Link>
                </td>
                <td>
                  {k.status === "revoked" ? (
                    <span className="badge revoked">revoked</span>
                  ) : k.expired ? (
                    <span className="badge expired">expired</span>
                  ) : (
                    <span className="badge active">active</span>
                  )}
                </td>
                <td>
                  {k.devicesUsed}/{k.deviceLimit}
                </td>
                <td className="muted">
                  {k.lastSeenUtc ? new Date(k.lastSeenUtc).toLocaleString() : "never used"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

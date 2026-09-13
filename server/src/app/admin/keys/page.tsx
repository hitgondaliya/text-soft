"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";

interface KeyRow {
  id: string;
  prefix: string;
  serial: string;
  seq: number;
  edition: string;
  status: string;
  expired: boolean;
  expiryUtc: string | null;
  deviceLimit: number;
  devicesUsed: number;
  customerName: string;
  customerEmail: string;
  createdUtc: string;
  lastSeenUtc: string | null;
}

function statusBadge(k: KeyRow) {
  if (k.status === "revoked") return <span className="badge revoked">revoked</span>;
  if (k.expired) return <span className="badge expired">expired</span>;
  return <span className="badge active">active</span>;
}

export default function KeysPage() {
  const [rows, setRows] = useState<KeyRow[]>([]);
  const [status, setStatus] = useState("");
  const [q, setQ] = useState("");

  const load = useCallback(() => {
    const p = new URLSearchParams();
    if (status) p.set("status", status);
    if (q) p.set("q", q);
    fetch(`/api/admin/keys?${p}`)
      .then((r) => r.json())
      .then((d) => setRows(d.keys ?? []))
      .catch(() => {});
  }, [status, q]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <h1 style={{ marginTop: 0 }}>License keys</h1>
        <Link href="/admin/orders"><button>+ New purchase</button></Link>
      </div>

      <div className="card" style={{ display: "flex", gap: 12, marginBottom: 16 }}>
        <select value={status} onChange={(e) => setStatus(e.target.value)} style={{ maxWidth: 180 }}>
          <option value="">All statuses</option>
          <option value="active">Active</option>
          <option value="expired">Expired</option>
          <option value="revoked">Revoked</option>
          <option value="unused">Unused</option>
        </select>
        <input
          placeholder="Search key, serial, customer, email…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
      </div>

      <div className="card" style={{ padding: 0 }}>
        <table>
          <thead>
            <tr>
              <th>Key</th>
              <th>Serial</th>
              <th>Edition</th>
              <th>Status</th>
              <th>Devices</th>
              <th>Expiry</th>
              <th>Customer</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((k) => (
              <tr key={k.id}>
                <td className="mono">
                  <Link href={`/admin/keys/${k.id}`}>{k.prefix}…</Link>
                </td>
                <td className="mono">
                  {k.serial ? (
                    <Link href={`/admin/orders/${k.serial}`}>
                      {k.serial}
                      {k.seq ? <span className="muted"> #{k.seq}</span> : null}
                    </Link>
                  ) : (
                    <span className="muted">—</span>
                  )}
                </td>
                <td>
                  <span className={`badge ${k.edition}`}>{k.edition}</span>
                </td>
                <td>{statusBadge(k)}</td>
                <td>
                  {k.devicesUsed}/{k.deviceLimit}
                </td>
                <td className="muted">
                  {k.expiryUtc ? new Date(k.expiryUtc).toLocaleDateString() : "perpetual"}
                </td>
                <td>{k.customerName || <span className="muted">—</span>}</td>
                <td className="muted">{new Date(k.createdUtc).toLocaleDateString()}</td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={8} className="muted" style={{ textAlign: "center", padding: 28 }}>
                  No keys.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

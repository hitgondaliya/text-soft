"use client";

import { useEffect, useState } from "react";
import Link from "next/link";

interface Stats {
  total: number;
  active: number;
  revoked: number;
  expiringSoon: number;
  activations: number;
  recent: Array<{ id: string; prefix: string; edition: string; lastSeenUtc: string }>;
}

function Stat({ label, value, accent }: { label: string; value: number; accent?: string }) {
  return (
    <div className="card">
      <div style={{ fontSize: 30, fontWeight: 700, color: accent }}>{value}</div>
      <div className="muted">{label}</div>
    </div>
  );
}

export default function Dashboard() {
  const [s, setS] = useState<Stats | null>(null);

  useEffect(() => {
    fetch("/api/admin/stats")
      .then((r) => r.json())
      .then(setS)
      .catch(() => {});
  }, []);

  return (
    <div>
      <h1 style={{ marginTop: 0 }}>Dashboard</h1>
      <div className="grid" style={{ gridTemplateColumns: "repeat(5, 1fr)", marginBottom: 24 }}>
        <Stat label="Total keys" value={s?.total ?? 0} />
        <Stat label="Active" value={s?.active ?? 0} accent="var(--ok)" />
        <Stat label="Expiring ≤7d" value={s?.expiringSoon ?? 0} accent="var(--warn)" />
        <Stat label="Revoked" value={s?.revoked ?? 0} accent="var(--danger)" />
        <Stat label="Devices" value={s?.activations ?? 0} accent="var(--brand)" />
      </div>

      <div className="card">
        <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 12 }}>
          <h3 style={{ margin: 0 }}>Recently active</h3>
          <Link href="/admin/keys">All keys →</Link>
        </div>
        {!s?.recent?.length ? (
          <div className="muted">No activity yet.</div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Key</th>
                <th>Edition</th>
                <th>Last seen</th>
              </tr>
            </thead>
            <tbody>
              {s.recent.map((r) => (
                <tr key={r.id}>
                  <td className="mono">
                    <Link href={`/admin/keys/${r.id}`}>{r.prefix}…</Link>
                  </td>
                  <td>
                    <span className={`badge ${r.edition}`}>{r.edition}</span>
                  </td>
                  <td className="muted">{new Date(r.lastSeenUtc).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

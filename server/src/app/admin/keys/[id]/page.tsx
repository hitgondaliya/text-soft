"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";

interface Activation {
  id: string;
  deviceFp: string;
  deviceName: string;
  ip: string;
  appVersion: string;
  status: string;
  activatedUtc: string;
  lastSeenUtc: string;
}
interface Detail {
  id: string;
  prefix: string;
  edition: string;
  status: string;
  expired: boolean;
  expiryUtc: string | null;
  deviceLimit: number;
  customerName: string;
  customerEmail: string;
  note: string;
  createdBy: string;
  createdUtc: string;
  activations: Activation[];
}

export default function KeyDetail() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const [d, setD] = useState<Detail | null>(null);
  const [extendDays, setExtendDays] = useState(30);
  const [limit, setLimit] = useState(1);

  const load = useCallback(() => {
    fetch(`/api/admin/keys/${id}`)
      .then((r) => r.json())
      .then((x) => {
        setD(x);
        if (x?.deviceLimit) setLimit(x.deviceLimit);
      })
      .catch(() => {});
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  async function patch(body: Record<string, unknown>) {
    await fetch(`/api/admin/keys/${id}`, {
      method: "PATCH",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body),
    });
    load();
  }

  if (!d) return <div className="muted">Loading…</div>;

  return (
    <div>
      <button className="secondary" onClick={() => router.push("/admin/keys")} style={{ marginBottom: 16 }}>
        ← Back
      </button>

      <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
        <h1 className="mono" style={{ margin: 0 }}>{d.prefix}…</h1>
        <span className={`badge ${d.edition}`}>{d.edition}</span>
        {d.status === "revoked" ? (
          <span className="badge revoked">revoked</span>
        ) : d.expired ? (
          <span className="badge expired">expired</span>
        ) : (
          <span className="badge active">active</span>
        )}
      </div>

      <div className="grid" style={{ gridTemplateColumns: "1fr 1fr", marginTop: 20 }}>
        <div className="card">
          <h3 style={{ marginTop: 0 }}>Details</h3>
          <Row label="Expiry" value={d.expiryUtc ? new Date(d.expiryUtc).toLocaleString() : "perpetual"} />
          <Row label="Device limit" value={String(d.deviceLimit)} />
          <Row label="Customer" value={d.customerName || "—"} />
          <Row label="Email" value={d.customerEmail || "—"} />
          <Row label="Note" value={d.note || "—"} />
          <Row label="Created by" value={d.createdBy} />
          <Row label="Created" value={new Date(d.createdUtc).toLocaleString()} />
        </div>

        <div className="card">
          <h3 style={{ marginTop: 0 }}>Actions</h3>

          <div style={{ display: "flex", gap: 8, alignItems: "flex-end", marginBottom: 14 }}>
            <div style={{ flex: 1 }}>
              <label>Extend by days</label>
              <input type="number" min={1} value={extendDays} onChange={(e) => setExtendDays(+e.target.value)} />
            </div>
            <button onClick={() => patch({ action: "extend", days: extendDays })}>Extend</button>
          </div>

          <div style={{ display: "flex", gap: 8, alignItems: "flex-end", marginBottom: 14 }}>
            <div style={{ flex: 1 }}>
              <label>Device limit</label>
              <input type="number" min={1} value={limit} onChange={(e) => setLimit(+e.target.value)} />
            </div>
            <button className="secondary" onClick={() => patch({ action: "setDeviceLimit", deviceLimit: limit })}>
              Set
            </button>
          </div>

          {d.status === "revoked" ? (
            <button onClick={() => patch({ action: "unrevoke" })}>Un-revoke key</button>
          ) : (
            <button className="danger" onClick={() => patch({ action: "revoke" })}>
              Revoke key
            </button>
          )}
        </div>
      </div>

      <div className="card" style={{ marginTop: 16 }}>
        <h3 style={{ marginTop: 0 }}>
          Activations ({d.activations.filter((a) => a.status === "active").length}/{d.deviceLimit})
        </h3>
        <table>
          <thead>
            <tr>
              <th>Device</th>
              <th>Fingerprint</th>
              <th>IP</th>
              <th>Version</th>
              <th>Last seen</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {d.activations.map((a) => (
              <tr key={a.id}>
                <td>{a.deviceName || "—"}</td>
                <td className="mono muted">{a.deviceFp.slice(0, 16)}…</td>
                <td className="muted">{a.ip}</td>
                <td className="muted">{a.appVersion || "—"}</td>
                <td className="muted">{new Date(a.lastSeenUtc).toLocaleString()}</td>
                <td>
                  {a.status === "active" ? (
                    <span className="badge active">active</span>
                  ) : (
                    <span className="badge revoked">off</span>
                  )}
                </td>
                <td>
                  {a.status === "active" && (
                    <button
                      className="secondary"
                      onClick={() => patch({ action: "deactivateDevice", deviceFp: a.deviceFp })}
                    >
                      Free slot
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {d.activations.length === 0 && (
              <tr>
                <td colSpan={7} className="muted" style={{ textAlign: "center", padding: 20 }}>
                  Not activated on any device yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: "flex", justifyContent: "space-between", padding: "6px 0", borderBottom: "1px solid var(--border)" }}>
      <span className="muted">{label}</span>
      <span>{value}</span>
    </div>
  );
}

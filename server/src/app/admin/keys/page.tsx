"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";

interface KeyRow {
  id: string;
  prefix: string;
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
  const [showGen, setShowGen] = useState(false);

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
        <button onClick={() => setShowGen((v) => !v)}>{showGen ? "Close" : "+ Generate keys"}</button>
      </div>

      {showGen && <GenerateForm onDone={load} />}

      <div className="card" style={{ display: "flex", gap: 12, marginBottom: 16 }}>
        <select value={status} onChange={(e) => setStatus(e.target.value)} style={{ maxWidth: 180 }}>
          <option value="">All statuses</option>
          <option value="active">Active</option>
          <option value="expired">Expired</option>
          <option value="revoked">Revoked</option>
          <option value="unused">Unused</option>
        </select>
        <input
          placeholder="Search prefix, customer, email…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
      </div>

      <div className="card" style={{ padding: 0 }}>
        <table>
          <thead>
            <tr>
              <th>Key</th>
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
                <td colSpan={7} className="muted" style={{ textAlign: "center", padding: 28 }}>
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

function GenerateForm({ onDone }: { onDone: () => void }) {
  const [edition, setEdition] = useState("full");
  const [mode, setMode] = useState<"days" | "date" | "perpetual">("days");
  const [days, setDays] = useState(365);
  const [expiryDate, setExpiryDate] = useState("");
  const [deviceLimit, setDeviceLimit] = useState(1);
  const [count, setCount] = useState(1);
  const [customerName, setCustomerName] = useState("");
  const [customerEmail, setCustomerEmail] = useState("");
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [created, setCreated] = useState<Array<{ key: string }>>([]);
  const [err, setErr] = useState("");

  async function submit() {
    setBusy(true);
    setErr("");
    const body: Record<string, unknown> = {
      edition,
      deviceLimit,
      count,
      customerName: customerName || undefined,
      customerEmail: customerEmail || undefined,
      note: note || undefined,
    };
    if (mode === "days") body.durationDays = days;
    else if (mode === "date" && expiryDate)
      body.expiryUtc = new Date(expiryDate + "T23:59:59Z").toISOString();
    else if (mode === "perpetual") body.perpetual = true;

    const res = await fetch("/api/admin/keys", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body),
    });
    setBusy(false);
    if (res.ok) {
      const d = await res.json();
      setCreated(d.created);
      onDone();
    } else {
      setErr("Could not generate. Check the fields.");
    }
  }

  if (created.length) {
    const text = created.map((c) => c.key).join("\n");
    return (
      <div className="card" style={{ marginBottom: 16, borderColor: "var(--brand)" }}>
        <h3 style={{ marginTop: 0 }}>
          {created.length} key{created.length > 1 ? "s" : ""} generated
        </h3>
        <p className="muted" style={{ marginTop: 0 }}>
          Copy these now — the raw keys are shown only once and are not stored on the server.
        </p>
        <textarea readOnly value={text} rows={Math.min(created.length + 1, 8)} className="mono" />
        <div style={{ display: "flex", gap: 8, marginTop: 12 }}>
          <button onClick={() => navigator.clipboard.writeText(text)}>Copy all</button>
          <button className="secondary" onClick={() => setCreated([])}>
            Generate more
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="card grid" style={{ marginBottom: 16, gridTemplateColumns: "repeat(3, 1fr)" }}>
      <div>
        <label>Edition</label>
        <select value={edition} onChange={(e) => setEdition(e.target.value)}>
          <option value="full">Full</option>
          <option value="demo">Demo</option>
        </select>
      </div>
      <div>
        <label>Validity</label>
        <select value={mode} onChange={(e) => setMode(e.target.value as typeof mode)}>
          <option value="days">For N days</option>
          <option value="date">Until date</option>
          <option value="perpetual">Perpetual</option>
        </select>
      </div>
      <div>
        {mode === "days" && (
          <>
            <label>Days</label>
            <input type="number" min={1} value={days} onChange={(e) => setDays(+e.target.value)} />
          </>
        )}
        {mode === "date" && (
          <>
            <label>Expiry date</label>
            <input type="date" value={expiryDate} onChange={(e) => setExpiryDate(e.target.value)} />
          </>
        )}
        {mode === "perpetual" && (
          <>
            <label>&nbsp;</label>
            <div className="muted" style={{ padding: "9px 0" }}>Never expires</div>
          </>
        )}
      </div>
      <div>
        <label>Device limit</label>
        <input type="number" min={1} value={deviceLimit} onChange={(e) => setDeviceLimit(+e.target.value)} />
      </div>
      <div>
        <label>How many keys</label>
        <input type="number" min={1} max={100} value={count} onChange={(e) => setCount(+e.target.value)} />
      </div>
      <div />
      <div>
        <label>Customer name</label>
        <input value={customerName} onChange={(e) => setCustomerName(e.target.value)} />
      </div>
      <div>
        <label>Customer email</label>
        <input type="email" value={customerEmail} onChange={(e) => setCustomerEmail(e.target.value)} />
      </div>
      <div>
        <label>Note</label>
        <input value={note} onChange={(e) => setNote(e.target.value)} />
      </div>
      <div style={{ gridColumn: "1 / -1", display: "flex", gap: 12, alignItems: "center" }}>
        <button onClick={submit} disabled={busy}>
          {busy ? "Generating…" : "Generate"}
        </button>
        {err && <span style={{ color: "var(--danger)" }}>{err}</span>}
      </div>
    </div>
  );
}

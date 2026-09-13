"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";

interface OrderRow {
  id: string;
  serial: string;
  customerName: string;
  customerEmail: string;
  company: string;
  edition: string;
  deviceLimit: number;
  quantity: number;
  issued: number;
  revoked: number;
  activated: number;
  expiryUtc: string | null;
  expired: boolean;
  createdUtc: string;
}

export default function OrdersPage() {
  const [rows, setRows] = useState<OrderRow[]>([]);
  const [q, setQ] = useState("");
  const [showNew, setShowNew] = useState(false);

  const load = useCallback(() => {
    const p = new URLSearchParams();
    if (q) p.set("q", q);
    fetch(`/api/admin/orders?${p}`)
      .then((r) => r.json())
      .then((d) => setRows(d.orders ?? []))
      .catch(() => {});
  }, [q]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <h1 style={{ marginTop: 0 }}>Purchases</h1>
        <button onClick={() => setShowNew((v) => !v)}>{showNew ? "Close" : "+ New purchase"}</button>
      </div>
      <p className="muted" style={{ marginTop: -8 }}>
        One serial number per sale. Buying 40 keys creates one serial with 40 keys under it.
      </p>

      {showNew && <NewPurchase onDone={load} />}

      <div className="card" style={{ marginBottom: 16 }}>
        <input
          placeholder="Search serial, customer, email, company…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
      </div>

      <div className="card" style={{ padding: 0 }}>
        <table>
          <thead>
            <tr>
              <th>Serial</th>
              <th>Customer</th>
              <th>Edition</th>
              <th>Keys</th>
              <th>In use</th>
              <th>Expiry</th>
              <th>Date</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((o) => (
              <tr key={o.id}>
                <td className="mono">
                  <Link href={`/admin/orders/${o.serial}`}>{o.serial}</Link>
                </td>
                <td>
                  {o.customerName}
                  {o.company && <div className="muted" style={{ fontSize: 12 }}>{o.company}</div>}
                </td>
                <td>
                  <span className={`badge ${o.edition}`}>{o.edition}</span>
                </td>
                <td>
                  {o.issued}
                  {o.revoked > 0 && (
                    <span className="muted" style={{ fontSize: 12 }}> ({o.revoked} revoked)</span>
                  )}
                </td>
                <td>{o.activated}</td>
                <td className="muted">
                  {o.expiryUtc ? new Date(o.expiryUtc).toLocaleDateString() : "perpetual"}
                  {o.expired && <span className="badge expired" style={{ marginLeft: 6 }}>expired</span>}
                </td>
                <td className="muted">{new Date(o.createdUtc).toLocaleDateString()}</td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={7} className="muted" style={{ textAlign: "center", padding: 28 }}>
                  No purchases yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function NewPurchase({ onDone }: { onDone: () => void }) {
  const [customerName, setCustomerName] = useState("");
  const [customerEmail, setCustomerEmail] = useState("");
  const [customerPhone, setCustomerPhone] = useState("");
  const [company, setCompany] = useState("");
  const [edition, setEdition] = useState("full");
  const [mode, setMode] = useState<"days" | "date" | "perpetual">("days");
  const [days, setDays] = useState(365);
  const [expiryDate, setExpiryDate] = useState("");
  const [deviceLimit, setDeviceLimit] = useState(1);
  const [quantity, setQuantity] = useState(1);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState("");
  const [done, setDone] = useState<{ serial: string; created: Array<{ seq: number; key: string }> } | null>(null);

  async function submit() {
    if (!customerName.trim()) {
      setErr("Customer name is required — the serial is filed under it.");
      return;
    }
    setBusy(true);
    setErr("");
    const body: Record<string, unknown> = {
      customerName,
      customerEmail: customerEmail || undefined,
      customerPhone: customerPhone || undefined,
      company: company || undefined,
      edition,
      deviceLimit,
      quantity,
      note: note || undefined,
    };
    if (mode === "days") body.durationDays = days;
    else if (mode === "date" && expiryDate)
      body.expiryUtc = new Date(expiryDate + "T23:59:59Z").toISOString();
    else if (mode === "perpetual") body.perpetual = true;

    const res = await fetch("/api/admin/orders", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body),
    });
    setBusy(false);
    if (res.ok) {
      setDone(await res.json());
      onDone();
    } else {
      setErr("Could not create the purchase. Check the fields.");
    }
  }

  if (done) {
    // The raw keys exist only in this response — the server keeps hashes.
    const lines = done.created.map((c) => `${done.serial}-${String(c.seq).padStart(3, "0")}\t${c.key}`);
    const text = lines.join("\n");
    return (
      <div className="card" style={{ marginBottom: 16, borderColor: "var(--brand)" }}>
        <h3 style={{ marginTop: 0 }}>
          Serial <span className="mono">{done.serial}</span> — {done.created.length} key
          {done.created.length > 1 ? "s" : ""}
        </h3>
        <p className="muted" style={{ marginTop: 0 }}>
          Copy these now. The raw keys are shown only once; the server stores only their hashes.
          The serial stays on record, so you can always see which keys belong to this sale.
        </p>
        <textarea readOnly value={text} rows={Math.min(done.created.length + 1, 12)} className="mono" />
        <div style={{ display: "flex", gap: 8, marginTop: 12 }}>
          <button onClick={() => navigator.clipboard.writeText(text)}>Copy all</button>
          <button
            className="secondary"
            onClick={() => {
              const blob = new Blob([`serial\tkey\n${text}\n`], { type: "text/tab-separated-values" });
              const a = document.createElement("a");
              a.href = URL.createObjectURL(blob);
              a.download = `${done.serial}.tsv`;
              a.click();
              URL.revokeObjectURL(a.href);
            }}
          >
            Download .tsv
          </button>
          <Link href={`/admin/orders/${done.serial}`} style={{ alignSelf: "center" }}>
            Open purchase →
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="card grid" style={{ marginBottom: 16, gridTemplateColumns: "repeat(3, 1fr)" }}>
      <div>
        <label>Customer name *</label>
        <input value={customerName} onChange={(e) => setCustomerName(e.target.value)} />
      </div>
      <div>
        <label>Email</label>
        <input type="email" value={customerEmail} onChange={(e) => setCustomerEmail(e.target.value)} />
      </div>
      <div>
        <label>Phone</label>
        <input value={customerPhone} onChange={(e) => setCustomerPhone(e.target.value)} />
      </div>
      <div>
        <label>Company</label>
        <input value={company} onChange={(e) => setCompany(e.target.value)} />
      </div>
      <div>
        <label>Edition</label>
        <select value={edition} onChange={(e) => setEdition(e.target.value)}>
          <option value="full">Full</option>
          <option value="demo">Demo</option>
        </select>
      </div>
      <div>
        <label>How many keys</label>
        <input type="number" min={1} max={500} value={quantity} onChange={(e) => setQuantity(+e.target.value)} />
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
        <label>Devices per key</label>
        <input type="number" min={1} value={deviceLimit} onChange={(e) => setDeviceLimit(+e.target.value)} />
      </div>
      <div style={{ gridColumn: "1 / -1" }}>
        <label>Note (invoice no., reseller, anything)</label>
        <input value={note} onChange={(e) => setNote(e.target.value)} />
      </div>
      <div style={{ gridColumn: "1 / -1", display: "flex", gap: 12, alignItems: "center" }}>
        <button onClick={submit} disabled={busy}>
          {busy ? "Creating…" : `Create purchase & issue ${quantity} key${quantity > 1 ? "s" : ""}`}
        </button>
        {err && <span style={{ color: "var(--danger)" }}>{err}</span>}
      </div>
    </div>
  );
}

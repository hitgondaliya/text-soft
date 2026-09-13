// End-to-end check of the one-key-one-device flow against a running panel:
//   npm run dev:db -- 3100   (in one terminal)
//   node scripts/e2e-license.mjs http://localhost:3100
// Needs the seeded admin (admin@textsoft.local / change-me-now) and creates test purchases.
const BASE = process.argv[2] ?? "http://localhost:3100";
let cookie = "";
const step = (n, ok, extra = "") => console.log(`${ok ? "PASS" : "FAIL"}  ${n}${extra ? "  " + extra : ""}`);
let failures = 0;
const expect = (n, cond, extra) => { step(n, cond, extra); if (!cond) failures++; };

async function api(path, body, opts = {}) {
  const r = await fetch(BASE + path, {
    method: opts.method ?? (body ? "POST" : "GET"),
    headers: { "content-type": "application/json", cookie, ...(opts.headers ?? {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const sc = r.headers.get("set-cookie");
  if (sc) cookie = sc.split(";")[0];
  let j = null;
  try { j = await r.json(); } catch {}
  return { status: r.status, j };
}

// ---- admin login + purchase ----
let r = await api("/api/login", { email: "admin@textsoft.local", password: "change-me-now" });
expect("admin login", r.status === 200, String(r.status));

r = await api("/api/admin/orders", {
  customerName: "E2E Traders", customerEmail: "e2e@example.com", customerPhone: "98250 00000", customerPhoneCountry: "IN",
  edition: "full", quantity: 2, durationDays: 30, note: "e2e",
});
expect("create purchase with IN phone", r.status === 201, JSON.stringify(r.j).slice(0, 120));
const serial = r.j.serial, key1 = r.j.created[0].key, key2 = r.j.created[1].key;

r = await api("/api/admin/orders", { customerName: "Bad Phone", customerPhone: "1234", customerPhoneCountry: "IN", edition: "full", quantity: 1, durationDays: 30 });
expect("purchase rejects invalid IN phone", r.status === 400 && r.j.field === "customerPhone", JSON.stringify(r.j));

// ---- installer precheck ----
const PC1 = { deviceFp: "FP-PC1-" + Date.now(), machineHint: "guid-pc1", deviceName: "OFFICE-PC1", appVersion: "1.0.0" };
const PC2 = { deviceFp: "FP-PC2-" + Date.now(), machineHint: "guid-pc2", deviceName: "HOME-LAPTOP", appVersion: "1.0.0" };

r = await api("/api/precheck", { serial, key: key1, machineHint: PC1.machineHint, deviceName: PC1.deviceName });
expect("precheck ok on fresh key", r.status === 200 && r.j.ok === true && r.j.reinstall === false, JSON.stringify(r.j));
r = await api("/api/precheck", { serial: "TXS-2020-9999", key: key1 });
expect("precheck wrong serial → serial_mismatch", r.status === 403 && r.j.error === "serial_mismatch", JSON.stringify(r.j));
r = await api("/api/precheck", { serial, key: "TXSF-0000-0000-0000" });
expect("precheck unknown key → invalid", r.status === 404 && r.j.error === "invalid");

// ---- activate on PC1 ----
r = await api("/api/activate", { serial, key: key1, ...PC1 });
expect("activate PC1", r.status === 200 && r.j.registered === false && r.j.serial === serial, JSON.stringify(r.j).slice(0, 100));
const tokenPc1 = r.j.token;
const payload = JSON.parse(Buffer.from(tokenPc1.split(".")[0].replace(/-/g, "+").replace(/_/g, "/"), "base64").toString());
expect("token v2 has serial/registered/validUntil", payload.v === 2 && payload.serial === serial && payload.registered === false && !!payload.validUntilUtc);

// ---- PC2 tries same key ----
r = await api("/api/activate", { serial, key: key1, ...PC2 });
expect("PC2 blocked → already_bound with bound device name", r.status === 403 && r.j.error === "already_bound" && r.j.boundDeviceName === "OFFICE-PC1", JSON.stringify(r.j));
r = await api("/api/precheck", { serial, key: key1, machineHint: PC2.machineHint, deviceName: PC2.deviceName });
expect("installer on PC2 blocked", r.status === 403 && r.j.error === "already_bound");
r = await api("/api/precheck", { serial, key: key1, machineHint: PC1.machineHint, deviceName: PC1.deviceName });
expect("installer reinstall on PC1 allowed", r.status === 200 && r.j.reinstall === true, JSON.stringify(r.j));

// ---- validate before registration ----
r = await api("/api/validate", { token: tokenPc1, appVersion: "1.0.0" });
expect("validate PC1 ok, still unregistered", r.status === 200 && r.j.registered === false);

// ---- register ----
r = await api("/api/register", { token: tokenPc1, name: "Nishant Patel", email: "Nishant@Example.com", phoneCountry: "IN", phone: "12345" });
expect("register rejects bad phone", r.status === 400 && r.j.field === "phone", JSON.stringify(r.j));
r = await api("/api/register", { token: tokenPc1, name: "Nishant Patel", email: "Nishant@Example.com", phoneCountry: "IN", phone: "98250 00000" });
expect("register ok", r.status === 200 && r.j.registered === true, JSON.stringify(r.j).slice(0, 80));
const tokenReg = r.j.token;
r = await api("/api/validate", { token: tokenReg });
expect("validate after register → registered:true", r.status === 200 && r.j.registered === true);

// ---- admin sees it ----
r = await api(`/api/admin/orders/${serial}`);
const k1 = r.j.keys.find((k) => k.seq === 1);
expect("purchase page: key1 bound+registered+user+attempts", k1.bound && k1.registered && k1.userName === "Nishant Patel" && k1.userPhone === "+91 98250 00000" && k1.attempts === 3, JSON.stringify(k1));
expect("purchase page: customer phone with flag data", r.j.customerPhoneCountry === "IN" && r.j.customerPhoneDisplay === "+91 98250 00000", r.j.customerPhoneDisplay);
r = await api(`/api/admin/keys/${k1.id}`);
expect("key page: device + user + attempts", r.j.device?.deviceName === "OFFICE-PC1" && r.j.device.user?.email === "nishant@example.com" && r.j.attempts.length === 3 && r.j.attempts.filter((a) => a.reason === "already_bound").length === 2, JSON.stringify(r.j.attempts.map(a=>[a.reason,a.source,a.deviceName])));
r = await api(`/api/admin/attempts?q=${serial}`);
expect("security list shows attempts for serial", r.j.attempts.length >= 3, String(r.j.attempts.length));
r = await api(`/api/admin/activations?registered=yes&q=nishant`);
expect("end users list finds Nishant", r.j.activations.some((a) => a.user?.name === "Nishant Patel"));
r = await api(`/api/admin/stats`);
expect("stats has registered + blocked7d", r.j.registered >= 1 && r.j.blocked7d >= 3, JSON.stringify({reg:r.j.registered, b:r.j.blocked7d}));

// ---- move to another device ----
r = await api(`/api/admin/keys/${k1.id}`, { action: "releaseDevice", reason: "e2e move" }, { method: "PATCH" });
expect("admin releases device", r.status === 200);
r = await api("/api/validate", { token: tokenReg });
expect("old PC validate → released", r.status === 403 && r.j.error === "released", JSON.stringify(r.j));
r = await api("/api/activate", { serial, key: key1, ...PC1 });
expect("old PC re-activate → released (blocked)", r.status === 403 && r.j.error === "released");
r = await api("/api/activate", { serial, key: key1, ...PC2 });
expect("new PC activates", r.status === 200 && r.j.registered === false);
const tokenPc2 = r.j.token;
r = await api(`/api/admin/keys/${k1.id}`);
expect("key page: holder is PC2, history has PC1 released by admin", r.j.device?.deviceName === "HOME-LAPTOP" && r.j.history.length === 1 && r.j.history[0].status === "released" && r.j.history[0].releasedBy === "admin@textsoft.local", JSON.stringify(r.j.history.map(h=>[h.deviceName,h.status,h.releasedBy])));

// ---- one PC, one key: PC2 activates key2 → key1 binding replaced ----
r = await api("/api/activate", { serial, key: key2, ...PC2 });
expect("PC2 switches to key2", r.status === 200);
r = await api("/api/validate", { token: tokenPc2 });
expect("PC2's key1 token now released (replaced)", r.status === 403 && r.j.error === "released" && r.j.reason === "replaced", JSON.stringify(r.j));
r = await api("/api/activate", { serial, key: key1, ...PC1 });
expect("key1 free again? no — PC1 was released, stays blocked", r.status === 403 && r.j.error === "released");
const PC3 = { deviceFp: "FP-PC3-" + Date.now(), machineHint: "guid-pc3", deviceName: "PC3" };
r = await api("/api/activate", { serial, key: key1, ...PC3 });
expect("key1 taken by a third PC", r.status === 200);

// ---- revoke/expiry ----
r = await api(`/api/admin/keys/${k1.id}`, { action: "revoke" }, { method: "PATCH" });
r = await api("/api/activate", { serial, key: key1, ...PC3 });
expect("revoked → 403 revoked + logged", r.status === 403 && r.j.error === "revoked");
r = await api(`/api/admin/keys/${k1.id}`);
expect("attempt log has revoked entry", r.j.attempts.some((a) => a.reason === "revoked"));
r = await api("/api/deactivate", { token: tokenPc2 });
expect("client deactivate is gone (410)", r.status === 410);

// ---- rate limit ----
let limited = false;
for (let i = 0; i < 40; i++) {
  const x = await api("/api/activate", { serial, key: "TXSF-ZZZZ-ZZZZ-ZZZZ", deviceFp: "rl" + i });
  if (x.status === 429) { limited = true; break; }
}
expect("rate limit kicks in on repeated bad keys", limited);

console.log(failures === 0 ? "\nALL PASS" : `\n${failures} FAILED`);
process.exit(failures ? 1 : 0);

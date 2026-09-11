# Licensing System Plan — server (Next.js admin + API) + WPF client

Goal: issue/sell/demo license keys, bind them to a device, and **stop the app from
working once a key is expired or revoked** — with a Next.js admin panel to generate
and manage keys.

## 0. Honest reality about "non-crackable"

No offline desktop app is 100% uncrackable. A determined attacker who owns the binary
can patch out any local check. So the goal is not "impossible" — it is **"not worth
cracking + can't be shared + expiry can't be bypassed."** We achieve that with a few
layers that are each strong:

1. **Asymmetric signatures** — the server signs licenses with a PRIVATE key that never
   ships. The app only has the PUBLIC key. → *Nobody can forge a key even with full
   access to the app* (this fixes today's flaw where the HMAC secret ships in the exe).
2. **Online activation + periodic re-validation** — a cracked copy that skips the
   server gets no valid token and loses access after the offline grace window; the
   server can revoke instantly.
3. **Device binding** — one key activates a limited number of machines.
4. **Clock-tamper defense** — trusted server time + monotonic last-seen check stops
   "set the clock back" expiry bypass.
5. **Binary obfuscation** — raises the effort to reverse-engineer/patch.

The one thing we cannot prevent is someone patching a *pirated* build to ignore
licensing entirely — but that build still can't get server tokens, so online features
and revocation make it a dead end. This is the same bar commercial products (incl.
Shree-Lipi) actually ship at.

## 1. Big change vs current code

Today `FontSoftware.Licensing` uses an **HMAC** (symmetric) secret baked into the app
— so anyone who extracts it can mint keys. We replace the signature with **Ed25519
(or RSA-2048) asymmetric signing**:

- Server holds the **private** key (in a secret manager / env var). Only it can sign.
- App embeds the **public** key. It can only *verify*, never *create*.

Everything else in the existing `LicenseManager` / `LicenseStore` / device-binding
structure is reused and extended.

## 2. Architecture

```
┌──────────────── Next.js (Vercel) ────────────────┐        ┌──────── WPF app (client) ────────┐
│  Admin panel (auth)   REST API                    │        │  Licensing module                 │
│  - generate keys      /api/activate               │◄──────►│  - activate(key, deviceFp)        │
│  - list/search        /api/validate (heartbeat)   │  HTTPS │  - validate on launch + heartbeat │
│  - revoke/extend      /api/deactivate             │        │  - verify token w/ PUBLIC key     │
│  - dashboard          (admin CRUD, protected)     │        │  - offline grace + clock guard    │
│         │                                          │        │  - store signed token locally     │
│         ▼                                          │        └──────────────────────────────────┘
│   Postgres DB  (keys, activations, admins, audit) │
│   Ed25519 PRIVATE key (env/secret)                │
└───────────────────────────────────────────────────┘
```

## 3. License lifecycle

1. **Generate** (admin panel): create a key row — edition (demo/full), expiry, device
   limit, customer note. Key string is a short Base32 code (e.g. `TXSF-XXXX-XXXX-XXXX`).
   The DB stores its hash + metadata. Demo keys = edition `demo`, expiry e.g. +7/15 days,
   device limit 1.
2. **Distribute**: send the key string to the user (email/WhatsApp). Manual for now.
3. **Activate** (client, needs internet once): user pastes key → app sends
   `{key, deviceFingerprint, appVersion}` to `/api/activate`. Server checks the key
   exists, not expired, not revoked, activation slots free → records the device →
   returns a **signed license token** `{keyId, edition, expiryUtc, deviceFp, issuedUtc, nonce}`
   signed with the private key. App verifies with the public key and stores it (bound to device).
4. **Validate** (client, every launch + periodic heartbeat): app checks the stored
   token's signature, device match, and expiry locally; and calls `/api/validate` to
   confirm the server hasn't revoked/expired it (refreshing the token + server time).
5. **Expire**: past `expiryUtc`, or when the server says revoked/expired, the app drops
   to a **locked state** — the editor/typing/converter are disabled and only the
   "enter a new key" screen shows. (Decide: fully lock, or fall back to a limited Free
   mode. Recommend full lock for a paid product with demo keys.)
6. **Renew / revoke** (admin): extend expiry or revoke; next heartbeat enforces it.

## 4. Stopping use after expiry (the core requirement) + anti-tamper

- **Local check every launch**: token expiry vs current time → lock if past.
- **Online heartbeat**: on launch and every e.g. 24h, call `/api/validate`. Server is
  the source of truth (handles revoke + real expiry). Refreshes the token and returns
  authoritative UTC time.
- **Offline grace**: cached token works offline for a bounded window (e.g. 3–7 days)
  so no-internet users aren't blocked; after that, must re-validate online. Prevents
  "activate once then stay offline forever."
- **Clock-rollback defense**: store the last-seen server UTC (in a signed/DPAPI-protected
  local blob). If the OS clock is earlier than last-seen, treat as tampered → require
  online re-validation. Use server time, not local time, as truth on each heartbeat.
- **Device binding**: token carries the device fingerprint; app refuses a token whose
  fingerprint ≠ this machine → copying `license.dat` to another PC fails.
- **Tamper-evident local store**: keep today's DPAPI/`ProtectedData` + device-MAC blob
  so the file can't be hand-edited or moved.
- **Obfuscation**: run the release exe through a .NET obfuscator (e.g. Obfuscar (free),
  or a commercial one) so the checks are hard to locate/patch.
- **Device fingerprint**: hash of stable hardware ids (Windows `MachineGuid` +
  volume serial + CPU id). Already have a `MachineGuid`-based provider hook.

## 5. Admin panel (Next.js) — features

- **Auth**: admin login (NextAuth credentials, or Clerk). Only you can access.
- **Generate keys**: single or batch; fields = edition (Demo/Full), duration (days) or
  fixed expiry date, device limit, customer name/email, internal note. Returns the key
  string(s) to copy.
- **Keys list**: search/filter by status (active / expired / revoked / unused), customer,
  date. Columns: key (masked), edition, expiry, devices used/limit, created, last seen.
- **Key detail**: activations (device fingerprints, IP, timestamps), extend expiry,
  change device limit, revoke / un-revoke, resend.
- **Dashboard**: totals (active, expiring soon, revoked), recent activations.
- **Audit log**: who created/revoked what, when.

## 6. Data model (Postgres)

- `admins(id, email, password_hash, role)`
- `license_keys(id, key_hash, key_prefix, edition, expiry_utc, device_limit,
   status[active|revoked], customer_name, customer_email, note, created_by, created_utc)`
- `activations(id, key_id→license_keys, device_fp, device_name, ip, app_version,
   activated_utc, last_seen_utc, status[active|deactivated])`
- `audit_log(id, admin_id, action, target, detail, created_utc)`

Store only the **hash** of the key string (like a password), plus a short prefix for
display. Never store raw keys.

## 7. API endpoints

Public (client):
- `POST /api/activate` → `{key, deviceFp, deviceName, appVersion}` ⇒ signed token or error
  (`invalid | expired | revoked | device_limit_reached`).
- `POST /api/validate` (heartbeat) → `{token}` ⇒ refreshed token + serverTimeUtc, or
  `revoked|expired`.
- `POST /api/deactivate` → `{token}` ⇒ frees a device slot (for legit machine changes).

Admin (auth-protected):
- `POST /api/admin/keys` (generate), `GET /api/admin/keys` (list),
  `GET/PATCH /api/admin/keys/:id` (detail, extend, revoke), `GET /api/admin/stats`.

All responses that the client trusts (activate/validate) are **signed**; the app
verifies the signature before acting.

## 8. Tech stack + hosting  — SELF-HOSTED VPS (chosen)

- **Admin + API**: Next.js (App Router, API routes), run in production mode on your VPS.
- **Process manager**: **PM2** (keeps Next.js running, restarts on crash/reboot).
- **Reverse proxy + HTTPS**: **nginx** in front, TLS via **Let's Encrypt (certbot)** —
  the client MUST talk to the API over HTTPS (so tokens/keys can't be sniffed/MITM'd).
- **DB**: **Postgres installed on the VPS** (or a managed Postgres if you prefer less ops).
  Daily automated backup (pg_dump cron) — the key/customer data lives here.
- **Auth**: NextAuth (credentials) — admin login only; no public signup.
- **Crypto**: Ed25519 via Node `crypto`; the **private key stays in a VPS-only env file
  (`.env`, chmod 600), never in git**. Generate the keypair once, offline; give me the
  PUBLIC key to embed in the app.
- **Client**: .NET 8; `System.Security.Cryptography` verifies Ed25519; `HttpClient` for
  activate/heartbeat; keep existing DPAPI store + device binding.
- **VPS setup checklist**: Ubuntu + Node LTS, `ufw` firewall (allow 80/443/ssh only),
  Postgres, nginx, certbot, PM2, a domain/subdomain (e.g. `api.textsoft…`) pointed at
  the VPS IP. Cost: one small VPS (~$5–6/mo) covers it.
- **Backups & keep-safe**: back up the DB **and** the Ed25519 private key securely — if
  the private key is lost, no new/renewed licenses can be signed (existing ones keep
  working until expiry).

## 9. Phased build plan

**Phase A — Server foundation**
1. Next.js project + Postgres + schema + admin auth.
2. Ed25519 keypair (private → server secret, public → give me for the app).
3. Admin: generate keys + list (minimum to distribute demo keys manually).

**Phase B — Activation API**
4. `/api/activate` + `/api/validate` + signing.
5. Device-limit + expiry + revoke logic.

**Phase C — Client integration (I do this in the WPF app)**
6. Swap HMAC → Ed25519 public-key verification in `FontSoftware.Licensing`.
7. Online activation UI + token store + heartbeat + offline grace + clock guard.
8. Lock the app (disable typing/converter) when unlicensed/expired.

**Phase D — Hardening**
9. Obfuscation in the release/installer build.
10. Admin polish (revoke, extend, dashboard, audit), rate-limiting, logging.

**Phase E — Go live**
11. Deploy, issue a real demo key end-to-end, test expiry + revoke + offline + clock-back.

## 10. Split of work

- **You / Next.js side** (with my step-by-step instructions): the admin panel + API +
  DB + hosting. I will hand you exact schema, endpoint contracts, signing code, and the
  token format so the two sides match.
- **Me / app side**: replace the licensing crypto, add activation + heartbeat + lock
  screen in the WPF app, and the obfuscated installer build.

## 11. Decisions

Made:
- **Expired behaviour**: **full lock** — app shows only the "enter a new key" screen;
  typing, converter, everything else disabled.
- **Hosting**: **self-hosted VPS** (Next.js + PM2 + nginx + Let's Encrypt + Postgres).
- **Key terms**: **not fixed** — the admin sets **duration and device-limit per key**
  at generation time (the generate form has "valid for N days / until date" and
  "device limit" fields). Demo vs full is just a preset the admin can pick or override.

Still to decide (can default now, change later):
- **Offline grace window** (suggest 3–7 days).
- **Heartbeat frequency** (suggest every 24h).
- **Domain/subdomain** for the API (e.g. `api.yourdomain.com`).

## 12. Immediate next steps (when you say go)

1. I hand you: the exact DB schema (SQL), the token JSON format + signing snippet, the
   3 client API contracts, and a VPS setup runbook (install → nginx → certbot → PM2 → env).
2. You (or I, guiding you) scaffold the Next.js project: admin auth, generate-key form
   (with per-key duration + device limit), keys list, and the activate/validate/deactivate
   API. Generate the Ed25519 keypair; give me the public key.
3. I integrate the app side: Ed25519 verification, activation UI, heartbeat, offline
   grace, clock guard, and the full-lock screen; then the obfuscated installer build.
4. End-to-end test: issue a demo key → activate → run → expire/revoke → confirm lock →
   offline + clock-back tests.

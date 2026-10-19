---
name: licensing-plan
description: Licensing V2 direction — self-hosted Next.js admin/API + Ed25519-signed online activation; full plan in docs/LICENSING-PLAN.md.
metadata: 
  node_type: memory
  type: project
  originSessionId: 88e64e9c-3041-4d61-bf89-b0d12b6267e0
  modified: 2026-09-08T18:34:31.694Z
---

Building a real licensing system to replace the current offline HMAC scheme in `FontSoftware.Licensing` (whose secret ships in the binary → forgeable, noted in its own code comments). Full plan: `docs/LICENSING-PLAN.md`.

**Core upgrade:** HMAC → **Ed25519 asymmetric signing**. Server holds the PRIVATE key; the app embeds only the PUBLIC key → the app cannot forge keys even fully reverse-engineered.

**Decisions made (2026-09-04):**
- Expired behaviour = **FULL LOCK** (app shows only "enter new key"; typing/converter disabled).
- Hosting = **self-hosted VPS** (Next.js + PM2 + nginx + Let's Encrypt HTTPS on the VPS; NOT Vercel).
- Key terms = **not fixed** — admin sets duration + device-limit **per key** at generation time (demo = a preset).
- **DB = MongoDB, NOT Postgres** (changed 2026-09-08). Collections: `admins`, `licenseKeys` (keyHash+prefix, edition, expiryUtc, deviceLimit, status, customer, note), `activations` (keyId+deviceFp unique, status), `auditLog`.

**Server BUILT 2026-09-08 (Phase A+B), lives in `server/` in this repo** — Next.js 14 App Router + TS + `mongodb` driver + `jose`/`bcryptjs` admin auth (signed HttpOnly cookie, middleware-gated) + Ed25519 via Node `crypto`. Routes: `/api/activate|validate|deactivate` (public, signed token), `/api/login|logout`, `/api/admin/keys[/:id]|stats` (protected). Admin UI: login, dashboard, keys list+generate (raw key shown once), key detail (revoke/extend/limit/free-slot). Scripts: `npm run genkeys`, `seed-admin`. NOT yet: `npm install`/build verified (no Node on dev box), obfuscation, real deploy.
- **Ed25519 keypair generated.** Dev keypair sits in `server/.env.local` (gitignored). Client PUBLIC key (base64, 32 raw bytes) = `4+s4/OseeXn6dCpVDXrRcM8BgDJ9BFoLak8hFs7if0U=` — but REGENERATE for production via `npm run genkeys` before shipping; do not embed the dev key.
- Token wire format: `base64url(payloadJson).base64url(ed25519sig)`; payload `{v,keyId,edition,expiryUtc,deviceFp,issuedUtc,serverTimeUtc,graceDays,heartbeatHours,nonce}`. Full contract in `server/README.md`.

**Architecture:** Next.js admin panel + REST API (`/api/activate`, `/api/validate` heartbeat, `/api/deactivate`, admin CRUD) + Postgres (license_keys, activations, admins, audit_log; store key HASH not raw). Client: online activation → server returns Ed25519-signed token (expiry + deviceFp bound) → launch check + 24h heartbeat + offline grace (3–7d) + clock-rollback guard (server time + last-seen) + device binding + DPAPI store + obfuscated release build.

**Honesty framing given to user:** no offline app is 100% uncrackable; goal is "not worth cracking + can't share + can't bypass expiry"; a patched pirate build still can't get server tokens (online validation + revoke = dead end). Same bar commercial products ship at.

**Work split:** user builds the Next.js/VPS side with my exact schema/contracts/runbook + gives me the Ed25519 public key; I do the WPF client side (crypto swap, activation UI, heartbeat, lock screen, obfuscated installer). Still-to-decide: offline grace window, heartbeat freq, API domain. See [[converter-encoding-findings]] for the rest of the app state.

**Phase C DONE 2026-09-08 (WPF client) — solution builds clean, 9 licensing unit tests pass.**
- HMAC gone: deleted `LicenseKeyService.cs` + `Base32.cs`; KeyGen tool stubbed (keys now from server admin panel).
- New in `FontSoftware.Licensing` (net8.0): `Ed25519Verifier` (BouncyCastle.Cryptography 2.4.0 — .NET has no native Ed25519; embedded raw 32-byte pubkey, DEV key = the one in [[licensing-plan]] above, REPLACE for prod), `TokenReader` (verify+parse token), `LicenseApiClient`/`ILicenseApi` (activate/validate/deactivate over HttpClient), reworked `LicenseManager` (async online: activate→store→offline eval with clock-rollback guard + offline grace + expiry + device-fp + heartbeat), reworked `LicenseModels` (LicenseStatus Locked/Active, editions Demo/Full, no Free tier). Kept device-bound HMAC-MAC local store envelope.
- App (net8.0-windows): `WindowsDeviceIdProvider` (SHA256 of MachineGuid + system-volume serial via P/Invoke + CPU count/arch + machine name); `LicenseViewModel` reworked async; `MainViewModel` wires api+reader+device+store, 6h heartbeat DispatcherTimer, full-lock gating (typing engine hard-off + `IsAppLocked`); `AppSettings.LicenseApiUrl` (default `http://localhost:3000` — set HTTPS prod URL); **full-window lock overlay** in MainWindow.xaml (ColumnSpan=2, shown when `License.IsLocked`, key box + Activate + Retry).
- Tooling note: dev box now has Node 24 (winget) + BouncyCastle; server verified build+boot earlier.

**Phase D DONE 2026-09-09 (obfuscation + prod-config swap) — obfuscation verified at assembly level.**
- `installer/obfuscar.xml`: Obfuscar config scoped to `FontSoftware.Licensing.dll` ONLY (KeepPublicApi=true so App still binds + XAML unaffected; HidePrivateApi, rename fields/props, HideStrings). App/Core/Engine NOT obfuscated (XAML binds VM members by name).
- `installer/build-release.ps1`: publishes App as FOLDER (not single-file, so DLLs are loose) → runs `obfuscar.console` on Licensing.dll → copies obfuscated dll back → Inno `setup.iss`. Kept `build-installer.ps1` (single-file, no obf) for dev/portable.
- `scripts/set-prod-config.ps1 -PublicKey <b64> -ApiUrl <https>`: rewrites the embedded key in `Ed25519Verifier.cs` (`DefaultPublicKeyBase64`) + `AppSettings.LicenseApiUrl`; enforces HTTPS + 32-byte key. Run before build-release for real ship.
- VERIFIED: installed Obfuscar.GlobalTool (cmd = `obfuscar.console`); published+obfuscated Licensing.dll; a net8 reflection harness loaded the obfuscated dll → public types kept, verified a real dev-signed token (edition=Full), rejected tampered. So obfuscation preserves crypto + public API.
- STILL NOT done: live end-to-end vs running server (needs Mongo up), GUI launch/screenshot of the lock overlay + a full obfuscated-app UI test, code-signing cert (client-supplied), embedding the actual PROD pubkey + prod HTTPS URL (run set-prod-config with real values).

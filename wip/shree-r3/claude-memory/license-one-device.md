---
name: license-one-device
description: "v3 licensing rules (one key = one PC, serial+key, registration gate, admin-only move, attempt log) and how to test them end to end"
metadata: 
  node_type: memory
  type: project
  originSessionId: 69d8b2a2-ed7d-447f-9ed1-ba585383f6f5
  modified: 2026-09-13T14:31:40.847Z
---

Licensing v3 (Sept 2026, commit after 1f7ae5b): a key binds to the first PC; other PCs
are refused and logged (`activationAttempts`, shown on Security page / key page /
purchase page). Activation needs serial + key. After activation the app holds a
registration form (name, email, mobile with country flag, India default) until
`/api/register` flips `registered: true` in the token. Only the admin moves a key
(key page → Move to another device → `releaseDevice`). Client `/api/deactivate` is
410. Plan + anti-crack layers: docs/LICENSING-V3-PLAN.md.

**Why:** the customer asked to block a key on a second device "all the way", to see
who tried, to collect name/email/mobile, and to move keys only via the panel.

**How to apply:**
- Installer page prechecks via `/api/precheck` and drops
  `%ProgramData%\TextSoft\pending-activation.json`; the app consumes it on first run.
- API URL is `LicenseEndpoints.BaseUrl` (Licensing) + `#define ApiUrl` in setup.iss;
  `scripts/set-prod-config.ps1` stamps both. No URL in settings.json any more.
- Test: `npm run dev:db -- 3100` then `npm run e2e` (server/scripts/e2e-license.mjs).
  WPF UI can be driven with the scratch `uia.ps1` pattern (UIA on overlay controls;
  content inside the headerless TabControl is NOT exposed to UIA — click by coords).
- Flags: 243 PNGs in `src/FontSoftware.App/Assets/flags` (from flag-icons via sharp);
  web uses `flag-icons` CSS. Countries generated from libphonenumber-js metadata.
- Dev unlock (`TEXTSOFT_DEV_UNLOCK`) exists only in Debug builds.

See [[license-server-purchases]], [[license-panel-design]].

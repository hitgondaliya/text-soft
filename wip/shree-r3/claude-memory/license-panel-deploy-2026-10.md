---
name: license-panel-deploy-2026-10
description: Panel v2 (Oct 2026) server facts — keys stored encrypted (KEY_VAULT_SECRET), SMTP from DB, port 3100 on loopback, .env.production renamed, update zip and VPS steps.
metadata:
  type: project
---

State after commit 38505a7 (2026-10-06):
- Keys: `licenseKeys.keyEnc` = AES-256-GCM "v1.<k|c>.iv.tag.ct" (k = KEY_VAULT_SECRET,
  c = derived from COOKIE_SECRET). Keys sold before 2026-10-06 have only keyHash and can never
  be shown; unused ones can be reissued (same serial/seq). Endpoints: keys/:id/reveal|reissue,
  orders/:id/reveal|resend (contract in server/README.md "Copying a key again").
- SMTP: `settings` doc _id "smtp" overrides SMTP_* in .env; password sealed. Owner-only.
- Production port **3100**, bound to 127.0.0.1 (ecosystem.config.cjs, npm start, deploy.sh
  reads PORT from env → .env → 3100). nginx must proxy_pass 127.0.0.1:3100 in EVERY server
  block (certbot added a 443 one).
- `server/.env.production` (tracked template) is now `.env.production.example`: Next reads
  .env.production BEFORE .env and the first file naming a variable wins even when blank.
  deploy.sh refuses to run while .env.production/.env.local exist and runs
  scripts/vault-check.mjs before restarting. seed-admin no longer resets the owner password
  (ADMIN_RESET_PASSWORD=1 forces it).
- Update zip for the VPS: Desktop `textsoft-admin-update-2026-10-06.zip`, built with
  `git -c core.autocrlf=false archive --prefix=textsoft-admin/ HEAD:server` (autocrlf made
  deploy.sh CRLF once; server/.gitattributes now forces LF for *.sh). It has no .env.
  The old `textsoft-admin-deploy.zip` holds the production .env (both LICENSE_PRIVATE_KEY
  lines in it are the production key; PORT=3000) — keep it as the secrets backup.
- VPS update steps are in server/README.md "Updating a VPS that runs the old build on port
  3000": unzip -o … -x .env, rm .env.production, set PORT=3100 + KEY_VAULT_SECRET in .env,
  ./deploy.sh, fix nginx proxy_pass, reload.
- Local testing: `npm run dev:db` now defaults to 3100; never let a test send through
  .env.local's real Gmail — point SMTP at scratchpad smtp-sink.mjs (127.0.0.1:2525, none).

See [[license-panel-design]] [[production-release-2026-09]] [[license-server-purchases]].

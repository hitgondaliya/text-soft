---
name: production-release-2026-09
description: "Where the shipped build, deploy zip, production keypair and Gmail SMTP live (13 Sep 2026 release); converter rebuilt on Converter nxt model"
metadata: 
  node_type: memory
  type: project
  originSessionId: 69d8b2a2-ed7d-447f-9ed1-ba585383f6f5
  modified: 2026-09-27T05:46:08.087Z
---

**Checked 27 Sep 2026: the server is live.** `https://admin.text-soft.com/login`
answers 200 and `/api/validate`, `/api/activate`, `/api/precheck` all answer 405
to a GET, so the routes are up. Activation works for a client today — check
before repeating that it is down.

**29 Sep 2026 build** (installer/output and Desktop, SHA-256 48e71d78e87c0576…): Ctrl+Space with Ctrl held, Shree keyboard-file import, 354 Converter-nxt pairs, 105 Ctrl+Space keys from Shree files, clipboard kept across pastes. Built with installer/build-release.ps1.

13 Sep 2026 release state:
- Admin panel deploy zip: `C:\Users\harde\Desktop\textsoft-admin-deploy.zip` (server/ minus
  node_modules, with a filled `.env`: Mongo 127.0.0.1:27017, Gmail SMTP textsoft.office@gmail.com
  app password, production Ed25519 keypair, ADMIN_EMAIL textsoft.office@gmail.com). Deploy:
  unzip → `./deploy.sh` (pnpm + pm2 "textsoft-admin", port 3000) + nginx.conf.example + certbot
  for admin.text-soft.com. Owner password was generated in that session (told to user; they should
  change it in Team).
- Installer: `C:\Users\harde\Desktop\TextSoft-Setup-1.0.0.exe` (also installer/output/), built by
  installer/build-release.ps1 with LicenseEndpoints.BaseUrl = https://admin.text-soft.com and the
  production public key `OvrGren3lqZss8fLcfI5M2/otEqL4TtHbXaYrlUqeqc=` (committed). The private key
  is ONLY in the zip's .env and the scratchpad of that session — regenerating it means rebuilding
  the app.
- Panel: purchase creation emails keys via nodemailer (lib/mail.ts); Team page manages admins.
- Converter now mirrors Shree's Converter nxt: script → input/output layout, any↔any via the
  Unicode hub (ConversionPipeline), text/clipboard with RTF formatting, Files mode (.txt/.rtf/.docx),
  numerals (Language/English/Hindi), persistent dictionary, plain text. 8-bit fonts (7.0 series,
  ISFOC…) write .txt as Windows-1252; PUA fonts (ShreeLipi-Ex) as UTF-8 (`IsByteEncoding`).

**Why:** the client launches with these exact artefacts; anyone touching licensing must keep the
public key / URL in sync with the deployed .env.

**How to apply:** a new keypair → `npm run genkeys` → update VPS .env → `scripts/set-prod-config.ps1`
→ `installer/build-release.ps1`. Test-launch the published exe after obfuscation.

See [[license-one-device]], [[license-server-purchases]]. Panel v2 (port 3100, recoverable keys): [[license-panel-deploy-2026-10]].

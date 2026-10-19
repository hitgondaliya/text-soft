# TextSoft License Server

Next.js (App Router) admin panel + activation API for TextSoft licenses.
MongoDB for storage, Ed25519 for signing license tokens.

> Built by Claude Code. Phase A (server foundation) + Phase B (activation API) of
> `docs/LICENSING-PLAN.md`. The WPF client integration is Phase C.

## What's here

```
server/
  src/lib/          mongo, env, crypto (Ed25519), token format, license-key gen, auth, audit,
                    vault (AES-256-GCM for recoverable keys + SMTP password), settings (SMTP), mail
  src/app/api/      precheck · activate · register · validate  (public, signed, rate-limited)
                    login · logout                    (admin session)
                    admin/orders · admin/keys · admin/activations · admin/attempts · admin/stats
                    admin/settings/smtp · admin/audit (owner only)
  src/app/admin/    dashboard · purchases · keys · end users · security · team · settings
  src/app/login/    admin sign-in
  src/middleware.ts guards /admin and /api/admin
  scripts/          genkeys.mjs · seed-admin.mjs · dev-with-db.mjs · e2e-license.mjs
                    vault-check.mjs (deploy canary) · vault-reseal.mjs (COOKIE_SECRET → KEY_VAULT_SECRET)
```

## Ports

The panel listens on **3100** everywhere — production and local:

| Where | How the port is set |
|---|---|
| VPS (pm2) | `PORT` in the environment, else `PORT=` in `.env`, else 3100 (`ecosystem.config.cjs`, `deploy.sh`) |
| nginx | `proxy_pass http://127.0.0.1:3100;` (`nginx.conf.example`) |
| local | `npm run dev:db` → http://localhost:3100 (`npm run dev:db -- 3200` for another) |

## Local setup

Prereqs: Node 20+. A MongoDB too, unless you use the throwaway one below.

```bash
cd server
npm install
npm run dev:db                 # http://localhost:3100  → /admin
```

`dev:db` starts a temporary MongoDB, seeds the admin from `.env.local` and runs
the panel against it. Nothing is installed system-wide and the data is discarded
on exit — for trying the panel out, or testing a change, on a machine that has no
database. Pass a port to move it: `npm run dev:db -- 3200`.

`.env.local` may hold a real SMTP account. To try purchase emails without mailing
anyone, point **Settings → Email** at a local SMTP sink (host `127.0.0.1`, security
*None*) first; `npm run e2e` never sends mail.

Against a real MongoDB instead:

```bash
cp .env.example .env.local     # .env.local already has a dev keypair filled in
# edit MONGODB_URI + ADMIN_PASSWORD
node --env-file=.env.local scripts/seed-admin.mjs
npm run dev -- -p 3100
```

Sign in with `ADMIN_EMAIL` / `ADMIN_PASSWORD`, then **Purchases → New purchase**.

## Purchases and serial numbers

A key is never issued on its own. Every sale is a **purchase** with a serial number
— `TXS-2026-0001` — and the keys are issued under it: sell 40 keys and you get one
serial with 40 keys numbered 1..40 beneath it. That is what makes a sale findable
afterwards, and lets the whole thing be extended, revoked or corrected in one go
rather than forty times.

The serial belongs to the **sale**, not the person. A repeat order from the same
customer gets its own serial; their history stays together because searching their
email lists every purchase they have made.

Numbers come from an atomic `$inc` on a `counters` document, so two admins
creating a purchase in the same second cannot be handed the same one.

`POST /api/admin/keys` is closed (410) — use `POST /api/admin/orders`.

## Crypto keys

- `.env.local` ships with a **dev** Ed25519 keypair. For production, run
  `npm run genkeys`, put the printed `LICENSE_PRIVATE_KEY` / `LICENSE_PUBLIC_KEY`
  into the server env, and give the printed `PublicKeyBase64` to the WPF client.
- The **private key never leaves the server**. If lost, no new/renewed licenses can
  be signed (existing tokens keep working until they expire). Back it up securely.
- `KEY_VAULT_SECRET` (`openssl rand -base64 32`) encrypts the recoverable copy of each
  licence key and the SMTP password saved in the panel (AES-256-GCM, key derived with
  HKDF per purpose — `src/lib/vault.ts`). Unset, a key derived from `COOKIE_SECRET` is
  used. Each ciphertext records which secret sealed it, so adding `KEY_VAULT_SECRET`
  later is safe: old values still open. **Never change or lose the secret that sealed
  them** — keys would then show as *not recoverable* (they still activate; reissue
  them if needed) and the SMTP password would have to be typed again. Back it up with
  the private key.

## Copying a key again (recoverable keys)

Every key is stored twice: the SHA-256 hash that activation looks up, and a sealed
copy (`licenseKeys.keyEnc`) so an admin can show it again later. Keys issued before
this existed have no sealed copy — the panel marks them *not recoverable*.

- **Reveal one key** — `POST /api/admin/keys/:id/reveal` → `{ key }`
  (`409 not_recoverable` for old keys). POST so nothing caches it; every reveal is
  audited (`key.reveal`, never the key itself).
- **Copy all keys of a purchase** — `POST /api/admin/orders/:serial/reveal` →
  `{ serial, keys: [{ id, seq, prefix, key | null, status, expired }], missing }`.
  Audited (`order.reveal`). Both reveals answer `429 rate_limited` past the hourly
  allowance (audited as `*.reveal_refused`).
- **Re-send the purchase email** — `POST /api/admin/orders/:serial/resend` with
  optional `{ to }` (defaults to the customer email) → `{ sent, to, toCustomer, included, missing, revoked }`.
  Revoked keys are left out. Only a send to the customer's own address marks the
  purchase *emailed*; a copy elsewhere is kept as `lastSentTo` / `lastSentUtc`.
  The body must be JSON (`415` otherwise). Errors: `400 no_recipient`, `409 no_recoverable_keys`,
  `503 mail_not_configured`, `502 mail_failed { detail }`.
- **Reissue an old key** — `POST /api/admin/keys/:id/reissue` → `{ key, prefix }`.
  Gives the key a new string in the same place (same serial, same number, same terms);
  the old string stops working at once. Refused while a PC holds the key
  (`409 in_use { deviceName }` — move it first) and for revoked keys (`409 revoked`).
  For an old key whose email was lost and that was never used.

List and detail responses carry `recoverable: boolean` per key
(`GET /api/admin/keys`, `GET /api/admin/keys/:id`, `GET /api/admin/orders/:serial`).

## One key, one device (v3)

- A key binds to the **first PC** that activates it. Any other PC is refused and the
  attempt is recorded (device name, IP, app version, time) — see **Security** in the
  panel, the key page and the purchase page.
- Only an admin can move a key: key page → **Move to another device**. The old PC is
  locked at its next check-in; the next PC to activate takes the key.
- Activation needs the **serial and the key**. The installer asks for both and
  checks them with `/api/precheck` before copying files.
- After activation the app asks for **name, email and mobile** (country code +
  number, validated with libphonenumber on both sides). Until `/api/register`
  succeeds the token carries `registered: false` and the app stays locked.
- Rate limits: 30 activation-type calls per IP / 10 min, 20 per key / hour. The client
  IP is taken from `X-Real-IP`, which nginx sets to the real peer (a client-sent
  `X-Forwarded-For` is ignored). Admin sign-in: 10 failed attempts per IP and 30 per
  account / 10 min (`429 rate_limited`). Key reveals: 1000 keys per admin / hour (each key of a *copy all* counts).

Run the whole flow against a dev panel with `npm run e2e` (creates test purchases).

## Client API contract

All are `POST` with a JSON body. Trusted responses carry a signed `token`.

### `POST /api/precheck`  (installer, before install)
```json
{ "serial": "TXS-2026-0001", "key": "TXSF-9F2K-7QH4-M3TP", "machineHint": "<MachineGuid>", "deviceName": "PC-01", "installerVersion": "1.0.0" }
// 200 { "ok": true, "serial": "...", "edition": "full", "expiryUtc": "...", "reinstall": false }
// errors: 404 invalid · 403 serial_mismatch|revoked|expired|already_bound (+ boundDeviceName) · 429 rate_limited
```

### `POST /api/activate`
```json
{ "serial": "TXS-2026-0001", "key": "TXSF-9F2K-7QH4-M3TP", "deviceFp": "<hex>", "machineHint": "<MachineGuid>", "deviceName": "PC-01", "appVersion": "1.0.0" }
// 200 { "token": "...", "serial": "...", "edition": "full", "expiryUtc": "...", "registered": false, "user": null, "serverTimeUtc": "..." }
// errors: 404 invalid · 403 serial_mismatch|revoked|expired|already_bound (+ boundDeviceName)|released · 429 rate_limited
```

### `POST /api/register`  (the person on the device)
```json
{ "token": "<current token>", "name": "Nishant Patel", "email": "n@x.com", "phoneCountry": "IN", "phone": "9825000000" }
// 200: token with registered:true + user  · 400 bad_request { field: "phone" } · 403 revoked|expired|released
```

### `POST /api/validate`  (heartbeat)
```json
{ "token": "<current token>", "appVersion": "1.0.0" }
// 200: fresh token + registered + user + serverTimeUtc · errors: 403 revoked|expired|released · 400 invalid
```

`POST /api/deactivate` answers **410** — devices are moved from the panel only.

### Token payload (v2)
Wire format `base64url(payloadJson).base64url(ed25519sig)`. Verify the signature over
the decoded JSON bytes with the embedded public key, then read:
```json
{
  "v": 2, "keyId": "<hex>", "serial": "TXS-2026-0001", "edition": "full", "expiryUtc": "..."|null,
  "deviceFp": "<hex>", "registered": true,
  "issuedUtc": "...", "serverTimeUtc": "...", "validUntilUtc": "...",
  "graceDays": 5, "heartbeatHours": 24, "nonce": "<hex>"
}
```
Client rules: reject if `deviceFp` ≠ this machine; lock past `expiryUtc`; lock past
`validUntilUtc` (the token is stale — talk to the server); stay on the registration
screen while `registered` is false; store `serverTimeUtc` as monotonic last-seen for
the clock-rollback guard; re-validate online every `heartbeatHours`; allow offline
use up to `graceDays` past last contact.

## Emails and the team

- **Purchase email.** Creating a purchase emails the serial, every key, validity and
  the install steps to the customer (branded HTML + plain text); the dialog reports
  whether it went. If it did not, the purchase page can re-send it later (keys are
  recoverable — see above). *Preview the purchase email* shows the template.
- **SMTP from the panel (Settings → Email, owners only).** Server, port, security
  (*SSL/TLS* = 465, *STARTTLS* = 587, *None* = local relay only), username,
  password, sender name and address, reply-to. Saved settings **override** the
  `SMTP_*` / `MAIL_*` lines in `.env`; *Reset* deletes them and mail falls back to
  `.env` exactly as before. The password is write-only: it is stored encrypted
  (vault), never returned — leave it blank to keep the stored one (only while the
  server and username stay the same; a new server needs its password typed in). *Send test email*
  tries the form's values before saving. Gmail needs an **app password** (Google
  Account → Security → App passwords); its spaces are removed automatically.
  API: `GET|PUT|DELETE /api/admin/settings/smtp`, `POST /api/admin/settings/smtp/test`
  `{ to, config? }` (staff → `403 forbidden`). Every change and test is audited.
  A host on the server's own network (loopback, private, link-local) is accepted
  only on a mail port — 25, 465, 587 or 2525 — so the test cannot probe other local
  services (`400 { field: "port" | "host", detail }`; `SMTP_ALLOW_PRIVATE_HOSTS=1` lifts it).
- **Team.** Team page: add panel users (owner / staff), reset passwords, disable or
  remove. Owners manage users; staff do everything else. Anyone can change their
  own password (current password required). The bootstrap owner comes from
  `ADMIN_EMAIL` / `ADMIN_PASSWORD` in `.env`. deploy.sh creates that owner if it is missing but never overwrites a password changed in the panel; to force the `.env` password back, run `ADMIN_RESET_PASSWORD=1 node --env-file=.env scripts/seed-admin.mjs` once.
  Every request re-reads the account, so disabling, removing or demoting someone
  takes effect within seconds, and a password change or disable also ends the
  sessions signed in before it (the 12-hour cookie alone is not enough).
- **Activity log (Settings, owners only).** Every reveal, copy-all, re-send, reissue,
  purchase change, team change and email-settings change, newest first, filterable
  (*Key access*, *Sales*, *Team*, *Settings*) and searchable by admin, serial or action.
  API: `GET /api/admin/audit?group=&q=&limit=` (owner only). Keys are never logged.
- **Same-origin only.** POST/PATCH/DELETE to `/api/admin/*`, `/api/login` and
  `/api/logout` must come from the panel's own pages (`Sec-Fetch-Site` / `Origin`
  checked in `middleware.ts`), so another `*.text-soft.com` page cannot act with an
  admin's cookie. Scripts that send no `Origin` (curl, `npm run e2e`) are unaffected.

## VPS deployment (admin.text-soft.com)

MongoDB is already on the VPS at 127.0.0.1:27017; nginx + pm2 + Node 20 are expected.

```bash
# 1. Upload the zip and unpack
sudo mkdir -p /var/www && cd /var/www
unzip textsoft-admin.zip            # → /var/www/textsoft-admin
cd textsoft-admin

# 2. Secrets (first install only): fill in the template as .env — never as
#    .env.production, which Next.js reads BEFORE .env (its blanks would win).
[ -f .env ] || cp .env.production.example .env
nano .env
#    (PORT=3100, and KEY_VAULT_SECRET from: openssl rand -base64 32)

# 3. Build + start (installs pnpm if missing, builds, checks the vault secrets,
#    seeds the owner, pm2 start on 127.0.0.1:$PORT)
chmod +x deploy.sh && ./deploy.sh
pm2 startup            # once, so pm2 comes back after a reboot

# 4. nginx + HTTPS (once) — nginx.conf.example proxies to 127.0.0.1:3100
sudo cp nginx.conf.example /etc/nginx/sites-available/textsoft-admin
sudo ln -s /etc/nginx/sites-available/textsoft-admin /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d admin.text-soft.com

# 5. Updates: unzip the new build over the folder WITHOUT its .env files, then ./deploy.sh
unzip -o ~/textsoft-admin.zip -x 'textsoft-admin/.env' 'textsoft-admin/.env.*' -d /var/www
./deploy.sh
# 6. Backups:  0 3 * * *  mongodump --uri="mongodb://127.0.0.1:27017/textsoft_licenses" --archive=/backups/txsf-$(date +%F).gz --gzip
```

Build the zip from the `server/` folder **without** `node_modules`, `.next*`, `.env`,
`.env.local` or any other `.env*` except the two `*.example` templates. `unzip -o`
overwrites without asking, so a zip that carried a `.env` would replace the VPS one —
and with it `KEY_VAULT_SECRET`, which only exists on the VPS. The `-x` above keeps the
VPS `.env` even if a zip does carry one.

`deploy.sh`:
- refuses to run while `.env.production`, `.env.local` or `.env.production.local` is in
  the folder (`next start` reads them before `.env`, and the first file that names a
  variable wins, even with an empty value);
- runs `scripts/vault-check.mjs` before touching the running app: it opens the newest
  stored key of each kind and the saved SMTP password with this `.env`'s secrets and
  checks the key against its hash. If `KEY_VAULT_SECRET` / `COOKIE_SECRET` no longer
  match, it stops with *"differs from the one that sealed existing data"* and the old
  process keeps running — restore the secret from backup instead of letting the panel
  show every key as *not recoverable* (and offer to reissue keys customers hold);
- reads `PORT` from the environment, else from `.env`, else 3100. Because
  `pm2 restart` keeps the port the app was first started with, it deletes the pm2 app
  and starts it fresh (listening on 127.0.0.1 only), waits for
  `http://127.0.0.1:$PORT/login` to answer, and checks that the nginx site proxies to
  the same port.

Production is started by pm2 (`ecosystem.config.cjs`) — `npm start` also defaults to
port 3100 and loopback, for a quick manual run.

### Updating a VPS that runs the old build on port 3000

```bash
cd /var/www/textsoft-admin
unzip -o ~/textsoft-admin.zip -x 'textsoft-admin/.env' 'textsoft-admin/.env.*' -d /var/www
diff .env.production .env                     # the old template, if there: copy anything only it has into .env
rm -f .env.production                         # it overrides .env under next start (deploy.sh insists)
nano .env                                     # set PORT=3100 (replace PORT=3000) and add
                                              # KEY_VAULT_SECRET=<output of: openssl rand -base64 32>
./deploy.sh                                   # rebuilds, re-creates the pm2 app on 3100
sudo nano /etc/nginx/sites-available/textsoft-admin
                                              # proxy_pass http://127.0.0.1:3100;  in EVERY
                                              # server block (certbot added a 443 one)
sudo nginx -t && sudo systemctl reload nginx
curl -I https://admin.text-soft.com/login     # expect 200
```

Keys sold before this update stay *not recoverable* (only their hash was ever kept);
keys sold after it can be copied again from the panel, and old unused keys can be
reissued. Back up `KEY_VAULT_SECRET` together with `LICENSE_PRIVATE_KEY`.

If keys were sealed with `COOKIE_SECRET` (sold while `KEY_VAULT_SECRET` was unset),
move them under the vault secret once it is set and the panel has been redeployed:
`node --env-file=.env scripts/vault-reseal.mjs` (dry run) then `--apply`. After that,
`COOKIE_SECRET` can be rotated without losing anything (everyone signs in again).

The Windows app and installer are built against `https://admin.text-soft.com`
and the public half of `LICENSE_PRIVATE_KEY`. Regenerate the keypair only together
with a new app build (`scripts\set-prod-config.ps1` → `installer\build-release.ps1`).
The port change is invisible to them: they talk to nginx over HTTPS.

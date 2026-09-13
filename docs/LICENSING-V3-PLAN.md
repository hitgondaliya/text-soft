# TextSoft licensing v3 — one key, one device

Status: implemented (see "What changed" at the end). Supersedes the parts of
LICENSING-PLAN.md that talk about device limits and client-side deactivation.

## 1. Rules

1. **One key ↔ one device.** A key binds to the first device that activates it.
   Every later activation attempt from a different device is refused and
   recorded. There is no "device limit" knob any more — it is always 1.
2. **Only the admin can move a key.** The panel has *Move to another device*
   on the key page. It releases the binding, notes who/why, and the next device
   to activate takes the key. The old device is locked at its next check-in.
3. **Serial + key, not key alone.** The installer and the app both ask for the
   purchase serial (`TXS-2026-0001`) and the key. A key only works with the
   serial it was issued under, so a leaked key without its serial is useless.
4. **Registration is mandatory.** After the key is accepted the app asks for
   name, email and mobile (country code + number, India +91 default, every
   country with its flag). The licence is not usable until this is submitted;
   the server marks the token `registered` and the panel shows the person.
5. **Blocked attempts are visible.** Every refused activation (already bound to
   another device, wrong serial, revoked, expired, unknown key) is stored with
   device name, IP, app version and time, and shown on the key page, the
   purchase page, the dashboard and the *Security* page.
6. **Expiry stops everything.** When the key expires (or is revoked, or moved)
   the typing hook, the converter and the whole window are disabled behind the
   lock screen. The app shows how long the key lasts right after activation and
   warns in the title bar during the last 7 days.

## 2. Flow

```
Installer (Inno Setup)                Server                        App (first run)
──────────────────────                ──────                        ───────────────
Serial + key page ──POST /api/precheck──▶ key ∈ serial? active?
                                         bound to another PC? ──▶ refuse + log attempt
                  ◀── ok ─────────────
writes ProgramData\TextSoft\pending-activation.json
                                                                  reads pending file
                                                                  POST /api/activate {serial,key,deviceFp,machineHint}
                                         bind (or refuse + log)
                                                                  token (registered:false) → registration form
                                                                  POST /api/register {token,name,email,phoneCountry,phone}
                                         libphonenumber validation, store on activation
                                                                  token (registered:true) → "Activated, valid until …"
                                                                  every 24 h: POST /api/validate (heartbeat)
```

The installer sends the Windows `MachineGuid` as a hint. If the key is already
bound and the hint matches the bound device, it is the same PC reinstalling and
the install continues; if it differs, the install is refused and the attempt is
logged as *installer*.

## 3. Anti-crack — what is real and what is not

No desktop program can be made impossible to crack: whoever owns the machine
owns the bytes. What we can do is make the *licence* worthless to share and
make patching the binary the only route, which means every pirated copy is a
modified EXE that never talks to our server and never gets updates. The layers:

| Layer | What it stops | Where |
|---|---|---|
| Ed25519-signed tokens, private key only on the server | forged licences, fake servers | `server/src/lib/crypto.ts`, `Ed25519Verifier.cs` |
| Serial + key + device fingerprint checked online | key sharing, key generators | `/api/activate` |
| One device per key, admin-only move | installing on a second PC | `/api/activate`, panel |
| Token `validUntilUtc` (issued + grace) | replaying an old token forever | `TokenReader`, `LicenseManager.Evaluate` |
| 24 h heartbeat + 5-day offline grace + clock-rollback guard | staying offline to dodge revoke/expiry, turning the clock back | `LicenseManager` |
| DPAPI (machine scope) + device-keyed HMAC on `license.dat` | copying the licence file to another PC, hand-editing it | `FileLicenseStore`, `LicenseManager` |
| Fingerprint from MachineGuid + system volume + BIOS + CPU (no machine name) | renaming the PC to break/duplicate bindings | `WindowsDeviceIdProvider` |
| Licence gate inside the typing engine (`TypingEngine.Gate`) and `IsEnabled` on the whole window | patching only the view-model | `TypingEngine`, `MainWindow.xaml` |
| Dev unlock compiled out of Release (`#if DEBUG`) | the env-var backdoor shipping | `LicenseViewModel` |
| API base URL is a compile-time constant (not settings.json) | pointing the app at a fake server | `LicenseEndpoints.cs` |
| Obfuscar on Licensing + Engine, strings hidden | reading the checks in a decompiler | `installer/obfuscar.xml` |
| Rate limits per IP and per key on activate/precheck/register | brute-forcing keys | `server/src/lib/ratelimit.ts` |
| Attempt log | knowing when someone tries | `activationAttempts` |

Still to do outside code before launch: production keypair (`npm run genkeys`),
HTTPS on the API, code-signing certificate for the EXE and installer.

## 4. Data

- `activations` gains `machineHint` (sha256 of MachineGuid), `user {name,
  email, phoneCountry, phone, phoneE164, registeredUtc}`, `releasedUtc`,
  `releasedBy`, `releaseReason`, status `active | released`.
- `activationAttempts` (new): `keyId, serial, seq, keyPrefix, deviceFp,
  deviceName, machineHint, ip, appVersion, reason, source (app|installer),
  boundDeviceName, createdUtc`.
- `orders` gains `customerPhoneCountry`, `customerPhoneE164`.
- Token payload v2 adds `serial`, `registered`, `validUntilUtc`.

## 5. What changed

Server: `/api/precheck`, `/api/register` (new); `/api/activate` requires serial,
enforces one device, logs attempts; `/api/validate` carries `registered`;
admin: `/api/admin/attempts`, `/api/admin/activations`, key `releaseDevice`,
stats. Panel: country-code phone field, end-user and device info on key and
purchase pages, *Move to another device*, *End users* and *Security* pages.

Client: installer serial/key page with online precheck; app reads the pending
file and activates on first run; registration form with country flags;
"valid until" notice; expiry banner; hard lock; DPAPI store; new fingerprint;
engine gate; dev unlock removed from Release.

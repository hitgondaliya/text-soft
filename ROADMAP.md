# Roadmap — what's left to ship V1.0

Current state: all 7 FRs are coded, the whole solution builds clean, and FR-4
(converter) + FR-5 (licensing) pass unit checks. But "builds" is not "shipped".
The remaining work is runtime debugging on Windows, real client data, hardening,
and signing/packaging. This file splits every remaining item by **who does it**.

Legend: ☐ = to do · Owner = who.

---

## A. YOU do on Windows (needs a real Windows machine)

These cannot be done on macOS — the keyboard hook, injection, real font
rendering, installer compile and signing are all Windows-only.

### A1. Get it running & smoke test
- ☐ Install .NET 8 SDK on Windows.
- ☐ `dotnet build FontSoftware.sln -c Release` — confirm clean.
- ☐ `dotnet run --project src/FontSoftware.App -c Release` — app opens.

### A2. Runtime-test the typing engine (FR-3) — the big one
- ☐ Toggle key turns typing ON/OFF; on-screen notification shows.
- ☐ Type in **Notepad**, **WordPad**, **MS Word**, **Chrome/Edge address bar + textboxes**.
- ☐ Shift layer produces the shifted characters.
- ☐ Backspace / space / Enter behave correctly while ON.
- ☐ Toggle OFF cleanly restores English — no stuck keys, no lost input.
- ☐ Note every app or case where it misbehaves → send me the list to fix.

### A3. Real assets (from client) integrated & tested
- ☐ Install the client's real font files on Windows.
- ☐ Drop real `languages.json`, layout files, and mapping tables into `assets/config/`.
- ☐ Confirm the on-screen keyboard (FR-2) shows correct characters.
- ☐ Convert known sample text both directions (FR-4) and eyeball correctness.

### A4. Licensing on a real device (FR-5)
- ☐ Generate a key (I'll give you a tiny key-gen tool / command).
- ☐ Activate → premium unlocks; restart → still premium.
- ☐ Copy the license file to another PC → stays Free (device binding works).

### A5. Installer & signing (NFR)
- ☐ Install **Inno Setup 6**.
- ☐ Run `installer/build-installer.ps1` → produces setup `.exe`.
- ☐ Test install → shortcuts, optional auto-start, launch.
- ☐ Test **uninstall** is clean.
- ☐ Apply the client's **code-signing certificate** to the app exe + installer.
- ☐ Check against common antivirus for false positives.
- ☐ Test on both **Windows 10** and **Windows 11** (64-bit).

### A6. Website (FR-7) publish
- ☐ Client provides domain + hosting → upload `website/` files.
- ☐ Fill placeholders (URL, price, download link, contact email, favicon).

---

## B. Code work — DONE on macOS ✅ (still needs your Windows runtime test in A2)

All built and compiling; unit-tested where possible. Runtime behaviour of the
typing ones is confirmed only when you test on Windows (A2).

- ☑ **Dead-key combining** — layout `deadKeyCombinations` tables; engine combines
  dead key + next char (falls back to concatenation).
- ☑ **Backspace on multi-char output** — one Backspace deletes the whole last
  injected cluster; a pending dead key is cancelled cleanly.
- ☑ **Per-app exclusions** — configurable process list (Settings tab); typing
  passes through untouched in those apps. Defaults: mstsc, keepass, 1password.
- ☑ **Diagnostic logging** — `FileLog` writes to `%AppData%\MultilingualTyping\logs\`;
  logs hook install, toggles, profile changes, errors. Send me these after A2.
- ☑ **xUnit test project** (`tests/FontSoftware.Tests`) — 17 tests, all pass
  (`dotnet test`). Covers converter, licensing, config loading, dead keys.
- ☑ **Config validator** (`tools/ConfigCheck`) — `configcheck assets/config`
  reports bad mapping tables / missing files / bad regex before shipping.
- ☑ **Key-generator** (`tools/KeyGen`) — `keygen --edition premium --count 5`
  mints unique signed license keys (for A4).
- ☑ **Notification polish** — toast fades in/out.
- ☑ **Premium scaffolding** — real gated feature wired (converter "Auto-copy
  result", enabled only with a premium license). Add the client's real premium
  features here once defined.
- ☑ **App icon** — branded `app.ico` (blue→purple, अ); set as exe + window icon
  and installer icon.

### B-next (optional, still mac-doable) — tell me if you want these
- ☐ More sample layouts/mappings for testing more scripts.
- ☐ In-app "open logs folder" button + a diagnostics view.
- ☐ Settings UI for the toggle-notification duration/position.
- ☐ CI workflow (GitHub Actions) to build + test on every push.

## C. CLIENT supplies (not code — blocks parts of A & B)

- ☐ Font files (licensed) + keyboard layout definitions + **tested mapping tables**.
- ☐ Premium feature list (FRD says by Week 5).
- ☐ Domain, website hosting, and **code-signing certificate**.
- ☐ Final premium price.

---

## Realistic timeline (why ~30–40 days is right)

The code scaffold came together fast, but calendar time is dominated by:

1. **Hook debugging across real apps (A2)** — the fiddliest part; injection
   behaves differently in Word vs browsers vs editors. Several iteration rounds
   with me. *~1–2 weeks.*
2. **Real assets + mapping-table correctness (A3, C)** — depends on the client
   delivering data, then testing/fixing conjuncts, half-letters, reordered
   matras. *~1–2 weeks, gated on client.*
3. **Hardening (B)** — dead keys, exclusions, edge cases. *~few days–1 week.*
4. **Packaging, signing, AV, Win10+Win11 testing (A5)** — plus cert from client.
   *~few days.*
5. **Website publish + polish (A6, B).** *~1–2 days.*

Much of this runs in parallel and is gated on **client deliverables (C)** and on
**your Windows testing feedback (A2)**, not on how fast code is written. So yes —
a few working weeks end-to-end is realistic, even though the code exists today.

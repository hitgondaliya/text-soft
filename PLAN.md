# Build Plan — Multilingual Typing & Font Conversion Software (V1.0)

Windows desktop app. Stack: **C# .NET 8 + WPF**. Dev on macOS (code), build/test on Windows.

Maps to `FRD.docx`. Each FR listed with status.

## Architecture

```
FontSoftware.sln
├─ src/FontSoftware.Core        (net8.0)          — no OS deps, unit-testable
│   ├─ Models                   language/font/layout/mapping data
│   ├─ Config                   load client assets from JSON
│   └─ Conversion               FR-4 converter engine
├─ src/FontSoftware.Engine      (net8.0-windows)  — user32 P/Invoke, no WPF
│   ├─ KeyboardHook             FR-3 low-level global hook (WH_KEYBOARD_LL)
│   ├─ InputInjector            SendInput unicode injection
│   └─ TypingEngine             FR-3 toggle + profile → char mapping
├─ src/FontSoftware.Licensing   (net8.0)          — FR-5 keys + device binding
├─ src/FontSoftware.App         (net8.0-windows, WPF) — FR-1,2,6 UI shell
└─ website/                     FR-7 marketing site
```

Assets (client-supplied, loaded at runtime — never hardcoded):
```
assets/config/languages.json        catalog: languages → fonts (Unicode/Legacy) + layouts
assets/config/layouts/*.json        keyboard layouts (key → char, shift/altgr)
assets/config/mappings/*.json       font-pair conversion tables
```

## Status

| FR | Feature | Layer | Status |
|----|---------|-------|--------|
| FR-1 | Language/Font/Keyboard selection | Core + App | done (builds) |
| FR-2 | On-screen keyboard viewer | App | done (builds) |
| FR-3 | System-wide typing engine (hook) | Engine + App | code done · needs Windows runtime test |
| FR-4 | Font converter (Legacy↔Unicode) | Core | done · 6/6 unit checks pass |
| FR-5 | Licensing (free/premium) | Licensing + App | done · 13/13 unit checks pass |
| FR-6 | Settings (toggle key, defaults) | App | done (builds) |
| FR-7 | Marketing website | website | done · responsive, verified desktop + mobile |

**Build status:** whole solution compiles clean on this machine (WPF via `EnableWindowsTargeting`). Runtime-testable only on Windows for the hook/injection (FR-3) and UI.

**Installer (NFR):** Inno Setup script at `installer/setup.iss` + `build-installer.ps1`. Self-contained publish verified (exe + config bundled). Compile the `.iss` on Windows; client adds code-signing cert. See `installer/README.md`.

**Hardening + tooling (all built + verified on macOS):**
- Engine: dead-key combine tables, backspace-deletes-cluster, per-app exclusions, diagnostic file logging.
- Premium gating wired to a real feature (converter auto-copy).
- Branded `app.ico` (exe + window + installer icon).
- `tests/FontSoftware.Tests` — 17 xUnit tests, all pass (`dotnet test`).
- `tools/KeyGen` (`keygen`) — mint unique signed license keys.
- `tools/ConfigCheck` (`configcheck`) — validate client assets before shipping.

See `ROADMAP.md` for the full remaining-work split (Windows tasks vs client deliverables).

## Order (agreed: core engine first)
1. Core: models + config loader + converter  ← **now**
2. Engine: keyboard hook + injection + typing
3. Licensing
4. WPF UI (FR-1,2,6) wired to core+engine
5. Installer (signed) + auto-start
6. Website (FR-7)

## Client dependencies (from FRD §4)
- Font files (licensed), keyboard layout defs, tested mapping tables
- Premium feature list (by Week 5)
- Domain, hosting, code-signing cert

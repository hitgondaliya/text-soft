# Multilingual Typing & Font Conversion Software

Windows desktop app (V1.0). Select a language/font/keyboard, type in that script
system-wide via a toggle key, and convert text between legacy and Unicode fonts.
See [`FRD.docx`](FRD.docx) for the full spec and [`PLAN.md`](PLAN.md) for the build roadmap.

## Requirements
- Windows 10/11 (64-bit) to run and to build the Windows-targeted projects
- .NET 8 SDK

## Solution layout
| Project | Target | Purpose |
|---|---|---|
| `src/FontSoftware.Core` | `net8.0` | Models, config/asset loading, FR-4 converter. No OS deps — unit-testable anywhere. |
| `src/FontSoftware.Engine` | `net8.0-windows` | FR-3 global keyboard hook, Unicode injection, typing engine. |
| `src/FontSoftware.Licensing` | `net8.0` | FR-5 licensing (pending). |
| `src/FontSoftware.App` | `net8.0-windows` (WPF) | FR-1/2/6 UI (pending). |

## Build
```bash
dotnet build FontSoftware.sln -c Release
```

## Run
```bash
dotnet run --project src/FontSoftware.App -c Release
```

## Package (Windows installer)
See [`installer/README.md`](installer/README.md). In short, on Windows:
```powershell
powershell -ExecutionPolicy Bypass -File installer/build-installer.ps1
```
Produces a self-contained setup `.exe` (end users need no .NET install).

## Tests
```bash
dotnet test tests/FontSoftware.Tests
```
17 tests covering the converter, licensing, config loading, and dead-key combining.

## Tools
```bash
# Mint license keys (admin):
dotnet run --project tools/KeyGen -- --edition premium --count 5

# Validate a client's asset config before shipping:
dotnet run --project tools/ConfigCheck -- assets/config
```

## Diagnostics
The app logs to `%AppData%\MultilingualTyping\logs\app-YYYYMMDD.log` — check it when
debugging hook/injection issues on Windows.

## Client-supplied assets
All language data lives in `assets/config/` and is loaded at runtime — nothing is
hardcoded. The files shipped there are **samples** to show the format; the client
replaces them with real, licensed, tested assets (FRD §4).

```
assets/config/languages.json        catalog: languages → fonts (unicode/legacy) + layouts
assets/config/layouts/*.json        keyboard layouts (vk → base/shift/altGr chars)
assets/config/mappings/*.json       font-pair conversion tables (preRules → pairs → postRules)
```

### Converter model (FR-4)
Each mapping table runs a three-stage pipeline:
1. `preRules` — regex rewrites on the input (e.g. normalize).
2. `pairs` — greedy **longest-match** substitution (so multi-char conjuncts win over single chars).
3. `postRules` — regex rewrites on the output (e.g. reorder i-matra from legacy to Unicode order).

Unmapped characters are passed through and reported (never dropped silently).
Table correctness is the client's responsibility; the engine only applies it.

### Typing engine (FR-3)
`LowLevelKeyboardHook` (WH_KEYBOARD_LL) intercepts keys; `TypingEngine` maps the
active layout's characters and `InputInjector` sends them as Unicode via SendInput.
Injected events are tagged so the hook ignores its own output. The toggle key
(default CapsLock, configurable per FR-6) flips typing ON/OFF and raises a
notification event. Must be hosted on a thread with a Windows message pump (the WPF UI thread).
```

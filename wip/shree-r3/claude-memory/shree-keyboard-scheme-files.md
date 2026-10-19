---
name: shree-keyboard-scheme-files
description: "Shree's 406-byte .GUJ/.DEV/... keyboard files (the Keyboard Generator's output too) — full format, and how the importer reads them"
metadata:
  node_type: memory
  type: reference
  originSessionId: 69d8b2a2-ed7d-447f-9ed1-ba585383f6f5
  modified: 2026-09-29T00:00:00.000Z
---

`C:\Shree-Lipi nxt\*.DEV|.GUJ|.PUN|.BAN|.ASS|.ORI|.TAM|.TEL|.KAN|.MAL` (and `.SAN`, which KBD32 can save) — one **406-byte** file per keyboard. Shree's Keyboard Generator (KBD32.exe, SLNXT Home menu → miKeyGen) saves exactly this format ("Gujarati Kbd (*.GUJ)" in its save dialog), so a client's own keyboard imports the same way.

**Format (decoded 2026-09-28 by writing files and asking the composer):**
- 0–127: one byte per ASCII char = code that key types (normal+shift, since k/K are different chars). 0xFF = nothing.
- 128–165: Ctrl+Space layer, keyboard order `1234567890-=qwertyuiopasdfghjklzxcvbnm`.
- 166–405: 48 conjuncts × 5 bytes: `04`, up to 4 codes padded 0xFF, **stored last piece first**. A key reaches slot n via code 0xB0+n (n<32) or 0x90+n−32. **Slot 31 (0xCF) reaches nothing on Shree itself** (DEVYANI.DEV).
- Codes are Shree's internal, positional-ish code; "Lk"/"Hf" in composer answers = Link (virama) / Half (virama+ZWJ).

**Importer:** `src/FontSoftware.Core/Keyboards/ShreeKeyboardFile.cs` + `assets/config/shree-kbd/<SCRIPT>.json` (code → text, plus "pairs": joins like अ ा→आ, ी ર્→ર્ી, and joins of code-less letters/half letters "U0A86 6C"→ઓ, "U0B130B4D 71"→ୱ). UI: Keyboards → "Import Shree-Lipi file…". Tables come from `tools/KbdFileProbe --table`; `--verify` scores the importer against every shipped keyboard (98.8–99.95% per script; rest = composer carrier quirks); `--compare-layouts` sets Shree's files beside our layouts (105 missing Ctrl+Space keys were added 2026-09-29).

**Composer answers depend on SLNXT's live settings** — the Tamil e/ai signs came back as a bare "." on one run; the table generator never trades a sign it once read for a carrier. The Kannada reph comes back on a carrier ಕ (ರ್ಕ) — cleaned to ರ್.

The CHM help files (`C:\Shree-Lipi nxt\data\<script>\*.chm`, `tools/ChmMap`) and the Tutor API ([[shree-tutor-api]]) remain the other sources. Related: [[shree-escape-layers]], [[textsoft-coverage-status]].

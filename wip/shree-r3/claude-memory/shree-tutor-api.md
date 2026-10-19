---
name: shree-tutor-api
description: "Shree's composer DLLs answer \"what does this key type\" directly — the exact call shapes that work"
metadata: 
  node_type: memory
  type: reference
  originSessionId: 69d8b2a2-ed7d-447f-9ed1-ba585383f6f5
  modified: 2026-09-27T05:07:17.814Z
---

Shree-Lipi's per-script composer DLLs (`C:\Shree-Lipi nxt\S8<Script><Family>64.dll`,
e.g. `S8GujU64.dll`) export a Tutor API that answers, key by key, what a keyboard
types. **Load standalone** — no SLNXT, no hook, no window — so it runs in the
background while the machine is in use.

Families: `U` = Unicode, `X` = Ex fonts, `7` = Shree-Lipi 7 fonts (its export drops
the W: `TutorGetMappedCode`), `G` = ?. There are `San` (Sanskrit) and `Dia`
(Diacritical) composers too; Sanskrit answers exactly as Devanagari does.

**Call shapes that work** (all stdcall; give every other argument its own zeroed
4096-byte page — a null crashes the process):

- `TutorGetKbdName(byte[] buf, int len)` → writes e.g. "Modular.Guj". **It lies**:
  it reports that name whatever is loaded, and the table a composer starts with is
  InScript (49/50 against the published Gujarati InScript chart), not Modular.
  Score against the charts instead.
- `TutorGetMappedCodeW(a1..a8)`: **a1 = the ASCII key char**, **a6 = the layer
  (0 = printed keyboard, 1 = the Ctrl+Space third layer)**, **a7 = out buffer
  (UTF-16)**, return = unit count. Getting a6 wrong corrupts every answer — that
  is what made an earlier sweep read 2% everywhere.
- `LoadKbdOverlay(name)`: **ANSI**, and the **full path with extension**
  (`C:\Shree-Lipi nxt\INSCRIPT.GUJ`). A bare name or a wide string silently
  corrupts the table instead of switching.

**Verified:** 194 keyboards captured across nine scripts plus the fifteen Assamese
`.ASS` files, and 323 of the product's 327 keyboards now agree with both this and
the published charts.

**Quirks** — the composer answers about a key in isolation, so it answers with the
state that key leaves the text in:
- a lone matra comes back on a carrier (a leading space or `.`), and a mark that
  cannot stand alone comes back on a carrier letter (the Kannada reph as ರ್ಕ on
  eight keyboards, always the same ಕ);
- a phonetic keyboard's vowel key answers with the matra, a consonant with its
  half form, where the chart prints the letter;
- a few characters come back as the tutor's placeholder (`Lk`) — treat two bare
  Latin letters as "no answer", never as "types nothing";
- an answer it gives to three or more keys at once (Bengali Swarna's ৻) is the
  composer having nothing to say.

**So the chart wins where it prints a key** — except where the chart's own row
repeats itself (one character on two keystrokes, or one keystroke with two
characters). Then the composer is the one that still knows: that tell put the
nukta back on the Kannada `]`, the subjoined consonants on the Punjabi keyboards,
the reph on Kannada `@`, the half forms on Punjabi Eng. See [[shree-keyboards]].

**Rosetta:** load U and X (or 7) in one process, press the same key on both → the
same character in two encodings. The 7/456 family answers in RAW BYTES; the X
family answers with the composer’s own code and the font draws it a fixed amount
higher, DIFFERENT PER SCRIPT (guj +0xE200 is not the general rule), and codes
under 0x100 are plain characters that do not move. The per-script offsets, the
Windows-1252 trap for bytes 0x80-0x9F, and the four ways this evidence can
mislead you are written down in docs/CONVERTER.md.

Tools: `tools/KbdCapture` (capture, `--rosetta`, `--score <chart>`),
`tools/KbdFromCapture` (apply), `tools/KbdAudit` (the ledger), and the rule itself
in `tools/Shared/Vendor.cs`. See [[shree-escape-layers]],
[[shree-keyboard-scheme-files]], [[converter-encoding-findings]].

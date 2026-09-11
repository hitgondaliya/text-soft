# Shree-Lipi Parity Plan — fonts, font-layouts (encodings), keyboard layouts

Goal: match what Shree-Lipi NXT offers across the **three** things the user asked for:

1. **Fonts** — the `.ttf` typefaces (render glyphs).
2. **Font-layouts / encodings** — a legacy font's byte→glyph map; the CONVERTER's
   source list (Kruti Dev, DevLys, Walkman Chanakya, Bhasha Bharati, ISFOC, Akruti,
   Bijoy, Shree-X/W/0700…). One conversion **table per encoding**.
3. **Keyboard layouts** — typing schemes (InScript, Remington, Phonetic, Bhasha
   Bharati, Atisha…). One keymap per layout.

## Scope measured (2026-09-04)

- Shree font files: **~457** across scripts (`D:\font software\FONTS\{Default,ShreeLipi-Ex,Unicode}`).
- Distinct legacy encodings in the `Default` set alone: **25** (design/italic variants collapse).
- Keyboard/encoding family names embedded in `KBD32.exe`: **~76** (Akruti ×many,
  ISFOC ×many, Kruti Dev, DevLys, Chanakya, Shivaji, Shusha, Yogesh, Kiran, Bhasha
  Bharati ×3, Atisha, Bijoy/Sutonny, Indica, Modular, Webel, …).
- Shree keyboard help files (CHM) already parsed: **188** (authoritative keystroke→Unicode).

## Where the data legitimately comes from

- **Shree's OWN fonts/keyboards** (X/W/0700, InScript, Remington, Akruti-Shree, …):
  derive from the **unencrypted 406-byte scheme files + CHM** we already use. ✅ dependency-free.
- **Third-party encodings** (Kruti Dev, DevLys, Chanakya, Shusha, Yogesh, Bhasha
  Bharati, ISFOC, Akruti, Bijoy…): these are OTHER vendors' fonts Shree merely
  *reads*. Shree's maps are in its **encrypted `.enc`** — we will NOT decrypt those.
  Instead use the **publicly documented mapping tables** for these well-known
  encodings (open-source converters exist for all the popular ones), plus targeted
  **GUI capture** from the licensed Shree converter for any that lack a public map.
- **Reph / conjunct / storage-order** correctness: validate against a real legacy
  document or a Shree GUI conversion sample.

## Current state (done)

- Encoding-based converter engine; one table serves a whole font family. ✅
- Tools: `FontScan` (group fonts→encodings), `ChmMap` (CHM→keystroke→Unicode + layout emit),
  `MapGen` (scheme⋈CHM→byte↔Unicode, both directions, matra/halant/reorder), `ConvTest`. ✅
- 9 Shree-X encodings × both directions; 38/38 round-trip, zero unmapped; 59 tests. ✅
- 160 keyboard layouts wired (Shree scheme set). ✅ (⚠ from KbdGen rosetta — re-issue from CHM)
- 8 Shree-X legacy fonts bundled + private-loaded in-app. ✅

## Phased plan to full parity

### Phase 1 — Complete Shree's own set (dependency-free; we have the data)
- [ ] Regenerate **all keyboard layouts from the authoritative CHM** (replace the
      KbdGen-rosetta ones); extend `ChmMap --layouts` to emit every scheme, not just 4.
- [ ] Generate converter tables for **every Shree encoding** the scheme+CHM covers:
      remaining scripts (Assamese, Diacritical, Symbol, Manipuri…) and the W/0700
      families where scheme files exist.
- [ ] `FontScan` the full tree → catalog every distinct encoding → bundle one
      representative font per encoding; register in `languages.json`.

### Phase 2 — Third-party legacy encodings via PUBLIC maps (high value, legal)
Priority by popularity (Devanagari first, then per script):
- [ ] Devanagari: **Kruti Dev** (have), **DevLys**, **Walkman Chanakya**, **Shivaji**,
      **Shusha**, **Yogesh**, **Kiran**, **APS**, **Bhasha Bharati**.
- [ ] Bengali: **Bijoy / SutonnyMJ**.
- [ ] Gujarati / Tamil / Telugu / Kannada / Malayalam / Punjabi / Odia: the common
      legacy encodings per script.
- Method: import each public byte→Unicode map into a `mappings/*.json`, add reph/matra
      reorder, round-trip test with `ConvTest`. Reuse the `MappingTable` format.

### Phase 3 — Keyboards not in the .DEV set (Bhasha Bharati family, Atisha, …)
- [ ] Locate/extract from `KBD32.exe` (native) OR capture via the Shree GUI OR use a
      published keymap chart; emit as layout JSON.

### Phase 4 — Ground-truth validation (needs licensed Shree GUI)
- [ ] Reph + conjunct + storage-order verification per encoding (one sample each).
- [ ] Native-speaker QA of auto-generated layouts (esp. Tamil InScript).
- [ ] Optional: system-wide font install (Font-Manager equivalent).

## Realistic note

Full 1:1 parity with a mature commercial product is a multi-phase effort measured in
weeks, dominated by (a) sourcing/validating each third-party encoding map and (b) QA —
not by engine work, which is done. The framework + tools make each new encoding a
data task (add a table, test), so coverage grows incrementally. Recommend shipping in
waves by popularity rather than blocking on 100%.

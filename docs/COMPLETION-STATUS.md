# Completion status — V1.0

Last updated: 2026-08-26. Tests: 42 passing. Build: clean.

## Definition of "done"
A feature is **code-complete** when it works and is verified in the app.
Some items are **client-dependent** — per FRD §4 the client supplies licensed fonts and *tested*
mapping/layout tables; those cannot be finished by us alone.

## Feature requirements (FRD)

| FR | Feature | Status |
|----|---------|--------|
| FR-1 | Language / font / layout selection | ✅ done |
| FR-2 | On-screen keyboard viewer | ✅ done |
| FR-3 | System-wide typing engine | ✅ done (SendInput bug fixed; live phonetic mode added) |
| FR-4 | Legacy ↔ Unicode converter | ✅ engine done; ✅ Kruti Dev + Shree-*X (9 scripts) both ways; ⬜ reph + long-tail conjuncts need a real legacy sample to verify |
| FR-5 | Licensing (free/premium) | ✅ done |
| FR-6 | Settings (toggle key, defaults, auto-start) | ✅ done |
| FR-7 | Marketing website | ✅ done |
| NFR | Installer (~74 MB single-file), auto-start, uninstall | ✅ done |
| Fonts | Shree legacy fonts bundled + private-loaded (render in-app, no system install) | ✅ 8 X-series scripts; system-wide install (Font-Manager style) optional/pending |
| — | Modern UI design | ✅ done |

## Typing coverage

| Language | InScript (layout) | Phonetic (type by sound) | Unicode font |
|----------|-------------------|--------------------------|--------------|
| Hindi | ✅ | ✅ | Mangal |
| Marathi | ✅ | ✅ | Mangal |
| Sanskrit | ✅ | ✅ | Mangal |
| Gujarati | ✅ | ✅ | Shruti |
| Punjabi | ✅ | ⬜ scheme pending | Raavi |
| Bangla | ✅ | ⬜ scheme pending | Vrinda |
| Telugu | ✅ | ⬜ scheme pending | Gautami |
| Kannada | ⬜ layout pending | ⬜ | Tunga |
| Malayalam | ⬜ | ⬜ | Kartika |
| Odia | ⬜ | ⬜ | Kalinga |
| Tamil | ⬜ (needs care — Tamil InScript differs structurally) | ⬜ | Latha |
| Assamese | ⬜ (≈ Bangla) | ⬜ | — |

## Converter (FR-4) coverage

Tables are **encoding-based**: one table per byte-layout serves a whole font
family (all Shree-DevX weights/italics share `shree-devx`). Fonts declare an
`encoding`; the converter matches on it, not on font id. Tables are auto-generated
by `tools/MapGen` from Shree ground truth (scheme byte-map ⋈ CHM keystroke→Unicode),
both directions, with i-matra reorder. `tools/ChmMap` extracts the CHM keymaps;
`tools/FontScan` groups fonts into distinct layouts.

| Pair | Status |
|------|--------|
| Kruti Dev ↔ Unicode (Hindi) | ✅ both directions, round-trip verified |
| Shree-*X ↔ Unicode — dev, guj, ban, tel, pun, kan, mal, ori, tam (9) | ✅ both directions generated; base + matras + i-matra reorder; dev round-trip tested |
| Reph (र्) reordering | ⬜ needs a real legacy sample to author/verify (visual-order float) |
| Rare conjuncts / storage-order confirmation | ⬜ verify against a real legacy document |
| Old bundled 0700/1120 series | ⬜ different byte layout than Shree-*X; table pending |

## Keyboard-layout parity (auto-generated)
`tools/KbdGen` decodes Shree-Lipi's 406-byte scheme files and generates Unicode
layouts using INSCRIPT as a Rosetta (Shree glyph byte → Unicode). Run:
`dotnet run --project tools/KbdGen -- "C:\Shree-Lipi nxt Plus"`.

| Script | Schemes generated |
|--------|-------------------|
| Devanagari (Hindi/Marathi) | 40 each |
| Sanskrit | 38 |
| Bangla | 26 |
| Gujarati | 20 |
| Telugu | 16 |
| Punjabi | 11 |
| **Total layouts in app** | **156** (198 refs incl. reuse), all load clean |

## Remaining work — finite list
1. Verify InScript for **Kannada, Malayalam, Odia, Tamil, Assamese**, then batch-generate
   their schemes (~98 more) with the same tool.
2. Native-speaker QA of the auto-generated layouts before shipping.
3. Converter tables via the same decode approach (reph + Gujarati + others).

## Blocked on the client (per FRD §4)
- **Licensed font files** for any legacy font shipped (Shree, Kruti Dev, etc.).
- **Tested** conversion tables + native-speaker QA of each script's layout.
- Final **V1 scope** decision: which scripts × which schemes actually ship.

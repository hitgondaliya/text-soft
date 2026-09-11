# Language build-out — method & status

Every language is data only (no code changes). Adding one = author its JSON in `assets/config/`,
validate with ConfigCheck, test, done.

## The method (repeat per language + scheme)

1. **Font**
   - Unicode: prefer a font already on Windows (Mangal=Hindi, Shruti=Gujarati, …). Nothing to ship.
   - Legacy: bundle the client's licensed `.ttf` under `config/fonts/`.
2. **Keyboard layout** → `layouts/<lang>-<scheme>.json`
   - Read the scheme's key-chart (Shree-Lipi PDFs under `D:\font software\Keyboard layouts\<SCRIPT>\`).
   - For **Unicode/InScript**: map each key → the script's Unicode codepoint. Conjuncts compose automatically in the target app (no dead-key table needed).
   - For **legacy**: map each key → the legacy font's glyph byte; conjuncts via `deadKeyCombinations`.
3. **Register** the font + layout in `languages.json`.
4. **Converter table** (only for FR-4) → `mappings/<pair>.json`: legacy↔Unicode character pairs, both directions.
5. **Validate**: `dotnet run --project tools/ConfigCheck -- assets/config` → 0 errors.
6. **Verify typing**: simulate a few real words and confirm the Unicode output (see scratch harness).

## Status

| Language | Unicode InScript (type) | Legacy layout (type) | Legacy↔Unicode converter |
|---|---|---|---|
| Hindi / Devanagari | ✅ done + verified (Mangal) | ⬜ pending (Shree-Dev / Kruti) | 🟡 sample only (Kruti↔Mangal) |
| Marathi | ✅ done (reuses Devanagari InScript) | ⬜ | ⬜ |
| Sanskrit | ✅ done (reuses Devanagari InScript) | ⬜ | ⬜ |
| Punjabi (Gurmukhi) | ✅ done + verified (Raavi) — nasal marks to review | ⬜ | ⬜ |
| Gujarati | ✅ done + verified (Shruti) | ⬜ pending (Shree-Guj) | ⬜ pending |
| Bangla | ✅ done + verified (Vrinda) | ⬜ | ⬜ |
| Tamil | ⬜ | ⬜ | ⬜ |
| Telugu | ⬜ | ⬜ | ⬜ |
| Kannada | ⬜ | ⬜ | ⬜ |
| Malayalam | ⬜ | ⬜ | ⬜ |
| Odia | ⬜ | ⬜ | ⬜ |
| Assamese | ⬜ | ⬜ | ⬜ |

Verified Unicode fonts per language (must be installed on Windows): Mangal (Hindi/Marathi/Sanskrit), Shruti (Gujarati), Raavi (Punjabi) — all ship with Windows.

## Notes / decisions
- **Scope first**: confirm with the client *which* scripts × *which* schemes × *which* fonts are in V1. Shree-Lipi has 12 scripts × ~20–38 schemes each; we ship only what's agreed.
- **Licensing**: InScript + Unicode fonts are IP-clean. Any Shree/legacy font needs the client's license before shipping (FRD §4).
- **Other schemes** (Phonetic, Remington, typewriter, publisher layouts) follow the same steps — one JSON each from the corresponding PDF.

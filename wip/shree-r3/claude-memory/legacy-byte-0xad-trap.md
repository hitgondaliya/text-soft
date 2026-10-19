---
name: legacy-byte-0xad-trap
description: "Legacy fonts put letters on byte 0xAD (U+00AD soft hyphen); WPF draws nothing for it, Word does — render legacy text by glyph index."
metadata: 
  node_type: memory
  type: project
  originSessionId: 69d8b2a2-ed7d-447f-9ed1-ba585383f6f5
  modified: 2026-09-13T11:31:31.723Z
---

Legacy 8-bit Indic fonts place letters on bytes that Unicode has already given a
meaning to. **ISFOC Gujarati puts ર on 0xAD = U+00AD SOFT HYPHEN.** WPF honours
the Unicode meaning and draws nothing, so "ભારત" appeared on screen as "ભાત"
even though the mapping table was correct.

Measured, so don't re-derive it: `A`+0xAD+`A` and `AA` have **identical width**
in `FormattedText` (both `TextFormattingMode` values) and in a `TextBox`. The
glyph is reachable from **no other code point** in the font, so no substitution
exists. **Word renders it correctly** (measured via `Range.Information(5)`), so
the customer's real output was never wrong — only our preview.

Fix: draw legacy text by **glyph index** (`GlyphTypeface.CharacterToGlyphMap` →
`GlyphRun`), which skips character semantics entirely. See
`src/FontSoftware.App/Controls/LegacyTextView.cs` and the same treatment in
`tools/GlyphJoin/Proof.cs`. Use `glyphjoin bytes <font> <hex,hex,…> --out png`
to check any suspect byte — it prints widths both ways and the glyph aliases.

Affected wired tables: isfoc-guj (25 pairs, ર), shree-dev456 (5, rare Sindhi
implosives), bhasha-bharati-dev (1). isfoc-dev has 1441 and is not wired yet.

Related: [[textsoft-coverage-status]], [[glyphjoin-shape-join]].

---
name: client-fonts-2026-09
description: Client fonts at D:\fonts — which work (Unicode) and the legacy-encoding wall reconfirmed.
metadata: 
  node_type: memory
  type: project
  originSessionId: 950e994d-182b-4119-bc3b-a8bb7f55e31a
  modified: 2026-09-10T18:01:34.161Z
---

Client dropped fonts at `D:\fonts` (2026-09). Identified via TTF family-name table (glyph names are junk "mac##", useless for mapping):

**Unicode — WORK, now bundled** under `assets/config/fonts/client/` and registered by `tools/ClientFonts` into Gujarati (Aakar, Shruti) and Devanagari hi/mr (Amita, Biryani, Mukta). Proven rendering correct Gujarati/Devanagari.

**Bhasha Bharati confirmed** (Gujarati legacy): BAMBAR=Abmar, BASWAD=Aswad, BKA/BKAB=Kautilya, BSHARMI=Sharmila. B_BHAR_1 = only 23 glyphs (broken file). Plus other legacy: AabadTwo, AakarTwo(legacy≠the Unicode aakar), AjantaTwo, ITRFK-Guj-Brush, Hitarth Guj Neena. Shree: S0700(DevX-0700), S0756/57/58/59/93/1120 (GujX).

**Legacy wall reconfirmed (important):** our `shree-gujx`/`shree-devx` maps round-trip (ConvTest 38/38) but the bytes do NOT match how the real TTFs render — rendering the map's legacy bytes in shree-gujx-0001 / S1120 / S0756 shows Latin/symbols, not Gujarati (byte A-Z → "ABC", high bytes → "¡¢£"). Matches earlier [[converter-encoding-findings]] ("Shree scheme bytes ≠ TTF encoding, FR-4 dead"). So live legacy typing built this session ([[unicode-font-fix]]) is NOT actually correct against real fonts.
- Glyph-name auto-map: fails (junk names). Shape-match (`tools/GlyphMatch`): unreliable + was validated against the wrong synthetic map.
**Takeaway:** Unicode is the reliable path (works with any Unicode font, all languages). Real legacy-font encoding isn't recoverable from the fonts alone; it lives in Shree's encrypted data. Don't re-promise legacy typing as working. To build a real per-font legacy map, need known-text↔legacy-bytes sample pairs from the client.

---
name: textsoft-coverage-status
description: "TextSoft font/layout coverage as of 2026-09-13 — what works, what is deliberately unmapped, what is still missing."
metadata: 
  node_type: memory
  type: project
  originSessionId: 69d8b2a2-ed7d-447f-9ed1-ba585383f6f5
  modified: 2026-09-27T05:07:58.762Z
---

**2026-10-01:** FullTest **1279/1475** with `--fonts "Gujarati font" --fonts D:/fonts`
(combined Bhasha Bharati; remaining BB fails = faces with no '*', '[ ]' or nukta glyph),
ConvTest 173/173, 232 unit tests, ConfigCheck 0 errors (73 documented drops as warnings).

Full-matrix state on **2026-09-27** (`dotnet run --project tools/FullTest`):
**1302/1454**. The total grew and the pass rate moved because the keyboards were
rebuilt from the vendor's own sources and now type several thousand more keys
(see [[shree-keyboards]]); every character they type is expressible, and what is
left is font files with holes — the Assamese SL7 fonts have no glyph for "%" or
":", the Sanskrit ones none for a soft hyphen — plus 43 dropdown rows with no
font wired. ConvTest 161/161, 131 unit tests, ConfigCheck 0 errors.

(It read 1404/1404 on 2026-09-13, before the keyboards could type those
characters at all.)

**Deliberately unmapped, not bugs** (verified against Shree's own Converter nxt,
which drops them identically): U+090E ऎ, U+0946 ॆ, U+0A3C ਼ standalone. InScript
is one layout across all Indic scripts, so its short-e key types a character
Devanagari never prints. Listed in `NotInAnyLegacyFont` in tools/FullTest.

**Shree's converter returns nothing on this install** for: DIACRITICAL (any
layout) and PUNJABI "Shree-Lipi 7.0". Punjabi's non-X encoding is really the
4.0/5.0/6.0 series; use that layout name when capturing. So the 19 DIAX fonts
stay unsupported.

**Assamese keyboards: closed on 2026-09-27.** The vendor does ship Assamese key
assignments — fifteen `*.ASS` files in the install root, with no help files of
their own. Captured through the Bengali composer and applied; the ৱ/ৰ keys and
the runs that press them now differ from Bangla. See [[shree-keyboards]].

**Third-party families still without a font to verify against**: Akruti, ISFOC,
Anu, Terafont, Shivaji, Indica, STM, BIJOY, Bamini, Vanavil, Tamil99, KGP/Nudi.
45 dropdown rows are empty for these; the app labels them "font not provided"
and types Unicode instead. See [[glyphjoin-shape-join]].

Tools: `tools/GapFill` merges Converter-nxt answers into an existing table;
`tools/shree/gapunits.ps1` writes the small probe for a handful of characters.

**Gujarati third-party (2026-09-13):** Akruti (AkrutiGujArchana), ISFOC (GJ-TTChitra,
GJ-TTDwarika) and Terafont (TERAFONT-CHANDAN) captured, render-verified and wired.
**LMG-Arun is NOT identified** (Gopika now is, see [[gopika-font-support]]) - garbage through all five Gujarati
encodings, and Shree lists no layout under either name. Re-checked 2026-09-14
against all six Gujarati encodings (shree-gujx, shree-guj7, terafont-guj,
isfoc-guj, akruti-guj, bhasha-bharti-2-guj): every one renders garbage in
Gopika. Gopika is a legacy 8-bit face (186 codes in 0x20-0xFF, zero Gujarati
Unicode, zero PUA) and its byte layout differs from LMG-Arun's too (byte 'A' is
છ in Gopika, બ in LMG-Arun), so one table will not cover both. Deriving a table
by shape alone fails: `tools/GlyphMatch Gopika.ttf SHRUTI.TTF` gives only
34/183 bytes above 0.55 and many of those are wrong. Supporting it needs a
typed sample or the vendor's key chart. Untried Shree Gujarati
layouts: Chitralekha, Prakashak, Sulekh, EKL, Hari, AshNew, Indica, APS.
Use convdrive.ps1 -MaxWaitSeconds 90 for sweeps: a layout Shree cannot produce
never fills the output box and otherwise burns 5 minutes per miss.

---
name: bhasha-bharati-combined
description: One "Bhasha Bharati" font layout for Gopika + BB-1 + BB-2 (Gujarati) and Hindi/Marathi; which Oasis faces use which table, how the tables were made, Sarika excluded.
metadata:
  type: project
---

Done 2026-10-01 on the client's request ("sab ek me"). Gujarati's three Shree layouts
(Bhasha Bharti-2, Bhasha Bharati-1, Bhasha Bharati) plus the old separate "Gopika" row are ONE
font layout `gu-fl-bhasha-bharati` with `encodings: [gopika-guj, bhasha-bharati-1-guj,
bhasha-bharti-2-guj]` and `replaces:` the old ids (saved settings migrate). Every font keeps its
own table; typing picks the first INSTALLED member font; the converter recognises the input
table from the text (`ConverterViewModel.Detect`, whole text / whole file) and uses the output
font's table.

Which face is which (proved by rendering every file through each table, proof sheets):
- The client's Oasis set in `Gujarati font/` (1996-2000 "…Two", Akshar, Aswad, GopikaTwo,
  Maruti, Title, Swastik… 56 families) = **Bhasha Bharati-1**. NOT BB-2, despite names.
- The client's older "B Bharati AbmarTwo"/"B Bharati Aswad" (D:\fonts) = **BB-2**.
- **Gopika.TTF** = Shree's "Bhasha Bharati" layout with fixes: aa-stroke/anusvara swapped
  (0x74/0x6B), na=0x6C, pa=0x76, whole ta=0x3B (0xED is ta-ta), pna/shna from half forms
  (0xA1/0xA7 are a 2nd pra/shra). Built by `scratchpad/bb/buildgop.js` from Shree's capture.
- ~24 "Gujarati font" files are Devanagari (Kautilya, Sharmila, Sahaj, Shyam, Vedik…) →
  hi/mr `bhasha-bharati-dev` (recaptured in full: old table lost reph/conjunct+i-matra).
- **Excluded (font files, not tables):** Sarika (letters on other bytes), ArjunTwo (own
  layout: ા→ં, ત→દ…), AshishTwo Regular/Italic (no સ glyph), GujaratiNew (no ણુ piece, BoldItalic
  no સ), SurajTwo Regular (no anusvara). VedikTwo kept: only its Bold file lacks the stem 'U'.
  Proof: one regular face per family through its table (scratchpad/proofsheet, EM env = size).
- B_Bharati01–25 are symbol/dingbat fonts, not text.

New faces are `optional: true` (listed only when installed).

**Candra is byte 0x70 'p' in every family** (Gopika, Oasis BB-1, B Bharati BB-2): 0xEC is a
short e-matra stroke. Shree's converter writes 0xEC (0x70 only after જા) - wrong in the real
fonts; the ITR keyboard types candra on P. Tables write 'p', read 'p' and still read 0xEC.
**Punctuation rule:** a mark is dropped (to "") only when its own byte is a letter AND the face
draws it nowhere else; dropped marks are listed in encoding-limits, and ConfigCheck accepts an
empty 'to' only when listed there. Gopika: ’→¥, ”→ª, “→‘‘, –/—→_, ':'→'&', …→"...".
**Reading:** l2u preRules put the aa-stem first (Gopika ut/it/pt→tu/ti/tp, BB uk/ik/pk→ku/ki/kp);
lone half-forms and post-signs that Shree only produced inside clusters got single-byte
fallbacks derived from the table itself (scratchpad/atoms.js, unanimous votes only).
**TextSoft 1.x's rule-based Gopika table (before 2026-10-01) was visually wrong** for S, M, @,
^, ³, ÷ and the half forms B/G/L/À (rendered જનમ્, સતય્, દૃરેક) - only 218/284 of its words
read back with today's table; HEAD's own l2u reads them if old documents ever need it.
See [[shree-keyboard-scheme-files]] [[textsoft-coverage-status]] [[gopika-font-support]].

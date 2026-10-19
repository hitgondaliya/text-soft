# The converter tables: where they come from and what is still missing

A legacy Indian font is a font whose bytes are letters. `L$` is ક, `H$` is क,
`EB15` is ಕ. A conversion table is the dictionary between those bytes and Unicode,
one file per direction:

```
assets/config/mappings/<encoding>-to-unicode.json   reading a legacy document
assets/config/mappings/unicode-to-<encoding>.json   writing one
```

The converter is a greedy longest match on the `from` side, keyed by the first
character. **A legacy code with no pair keyed on it falls straight through the
converter unchanged**, and the customer sees a Latin character - a `$`, a `>`, a
`½` - sitting in the middle of a word. That is the failure this document is about.

## Where the pairs come from

In order of how much they can be trusted.

1. **Shree's own two composers.** Every script ships a Unicode composer DLL and a
   legacy one. Load both in one process, press the same key, and the two answers
   are the same character in two encodings - the vendor telling us what a legacy
   code means, in its own words.

   ```
   dotnet run --project tools/KbdCapture -- --script dev --kbd INSCRIPT.DEV \
       --rosetta --legacy 7 --out <file>.json
   ```

   The `7`/`456` family answers in raw bytes. The Ex family answers with the
   composer's own code, which the font draws a fixed amount higher in the
   private-use area: dev +0xDF00, guj +0xE200, ban +0xE500, tel +0xEB00,
   kan +0xEA00, mal +0xF000, ori +0xE700, tam +0xE900, pun +0xE380. Codes under
   0x100 are plain characters and are not shifted.

2. **The table read against itself.** A code with no pair of its own is often used
   inside the table's own clusters. Read those clusters with the rest of the table,
   take the reading away, and what is left is what the code means. Only accepted
   when every cluster agrees.

3. **The font's own blocks.** The Ex fonts lay a script out in blocks: the plain
   consonants, then the same consonants with each vowel sign, then the subjoined
   forms, each stepping through the alphabet in the same order and skipping the
   same letters. Where a block is half transcribed, the transcribed cells prove
   the arithmetic and the rest follows.

4. **The printed chart** (`tools/ChmMap`), which is the weakest source: a chart
   draws a mark on a dummy base, and the dummy base can end up in the pair. Thirty
   five Telugu subjoined consonants carried a leading ఴ that way.

## What can mislead you

- **Half-letter keyboards.** Some layouts type consonant + virama on one key while
  the legacy side answers with the plain base. A thin vote for a reading that ends
  in a virama, against a table that says the plain letter, is this artifact.
- **Carriers.** A combining mark is drawn on a space or a dotted circle and the
  carrier comes back with the answer.
- **Spacers.** `$` and `>` are blank glyphs with a real width. Shree writes ક as
  `L$` because ક's ink runs past its own advance; written bare it touches the next
  letter. They are part of the letter, not punctuation.
- **The byte band 0x80-0x9F.** A legacy file is read as Windows-1252, so byte 0x85
  arrives as U+2026 and the tables store it that way. The composers answer in raw
  bytes. Spell one as the other and a whole band looks like a gap.
- **Whitespace passes the converter unreported.** The Shree 7 and 4/5/6 tables write
  the 15 Unicode spaces no face maps (U+1680, U+2000-200A, U+202F, U+205F, U+3000) as
  the fonts' space; the Gujarati, Devanagari and Sanskrit ones write NEL, U+2028 and
  U+2029 as a space, reported (the Bangla/Assamese ones keep U+2028/U+2029 as line
  breaks, which a .txt writes as CR LF). A .txt would have written them '?', NEL as
  its raw byte - a letter.

## What the Shree 7 and 4/5/6 tables write for what the fonts cannot show

Every Shree-Lipi 7 and 4/5/6 table (guj7, guj456, dev7, dev456, san7 and the five
Bangla/Assamese ones) follows one output policy, applied by the shared generator
code (`tools/Shared/ShreeNonIndic.cs`) - never by hand:

- **P1** Indic text is written exactly as Shree's composer writes it; a word-final
  virama is the visible halant (`tools/Shared/ShreeWordFinal.cs`).
- **P2** A character the fonts draw as itself at its own code (`, . ( ) ? ! -`, the
  script's digit at an ASCII digit) is written as that code, not reported.
- **P3** A character whose own code the fonts draw as something else (a Latin letter,
  `[` - an i-sign -, `<` - where Shree's composer writes ₹ -, ° - B0, a ra-sign) is
  never written as that code: the fonts' look-alike if there is one, else left out
  and reported.
- **P3b** A character no code of the fonts draws visibly - ₹ (Shree's 3C has no
  glyph), ૱, ૰ (its glyph sits at 8F, which Word draws as nothing), the vocalic-l
  letters and signs (ઌ ૡ ૢ ૣ, ऌ ॡ ॢ ॣ, ॑, ৄ ৢ ৣ), the short e and o of Devanagari
  (ऄ ऎ ऒ ॆ ॊ - the composer writes the base and the blank 90, which read back as
  another letter: ॊ as ा), ৱ in the Bangla fonts (9E, a C1 code), a Bangla nukta on a
  letter with no nukta spelling (all but জ ড় ঢ় য়: it stays after its letter) - is
  passed through as itself and reported, never left out: Word draws it in a fallback
  font (on a dotted circle of its own, for a sign) and the typist sees the right
  character. A reph is written before such a sign, on its letter (ર્કૢ = 4C 24 AE
  U+0AE2, र्कॊ = 48 A9 24 U+094A, র্কৄ = 41 A2 A1 U+09C4). Typing
  sends it as Unicode (Gujarati Ctrl+Shift+1 then `$` types ₹). The tables' checks
  compare with the composer on the table as the composer spells (the character
  written as nothing, `NonIndic.AsComposer`) and prove the real table is exactly
  that plus the character (`NonIndic.CheckPassThrough`). The Sanskrit fonts' missing
  nukta is the one exception: the composer drops it, the rules can only carry it
  before its letter, so the letter is written as the composer writes it and the
  nukta is left out and reported.
- **P4** A code Word or RichEdit draws as nothing (the C1 codes 81 8D 8F 90 9D) is
  never written: another code of the same glyph, an equivalent spelling, the ASCII
  look-alike, or left out and reported. The control characters - C0 but TAB LF VT
  FF CR, DEL, C1 - are left out and reported: Word draws them as nothing, and a .txt
  would write a stray U+0092 (’ mis-decoded from Windows-1252) as byte 92, થ.
- **P5** Reading back accepts every spelling we or Shree ever wrote, the passed-through
  characters included: each reads as itself and is not reported (the reverse tables
  have a pair to itself for every character the forward table passes through,
  `NonIndic.PassedThrough`), and a reph still moves past a vocalic-l sign written the
  older way. 3C, where Shree's composers write ₹, reads ₹ in every script. In the 7
  Bangla/Assamese fonts the 7 composer's own raw codes read as their text: ঃ 40 (the
  hook there) wherever no letter the hook joins follows it, ্ন 97 (৺ there) straight
  after a letter, its dashes 2D FF 2D (FF is ড়্গ there); and the table shipped before
  the split, which wrote গ্ন as 4E A5 in the 4/5/6 fonts, reads গ্ন there.
- **P6** Typing, RTF/clipboard, .docx and .txt carry the same code points. A .txt
  writes U+0080-U+00FF as their raw bytes and leaves out (never '?') a character no
  Windows-1252 byte holds - one passed through, which the converter has reported.

What is reported is told apart (`ConversionResult`): `LeftOutCharacters` - not in the
text as itself (left out, or a stand-in the fonts draw: · and ∙ as the large bullet B7,
reported in every script) - and `KeptCharacters` - in the text as itself (P3b, or a code
a reverse table could not read). The Converter screen says which is which, and which
characters a .txt had no Windows-1252 byte for; the typing engine logs both.

A composer row that is its carrier, not text, is not learned: Bangla া.ে = E0 EC and
ৗ.ে = EF EC (the composer asked for a lone sign on '.' and dropped the '.') made every
'.' between a syllable ending in া ো ৗ ৌ and one with ে ো ৌ disappear (ডা.সেন).

Known and left as they are: RichEdit/WordPad draws U+0152 U+0153 U+0160 U+0161
U+0178 U+0192 as Latin letters (it rebinds them to charset 238, Arial) where Word
draws the font's glyph; Bangla প and দ are U+0161 and U+0192.

## Checking the tables

```
dotnet run --project tools/ConvTest          # Unicode -> legacy -> Unicode, every script
dotnet run --project tools/FullTest          # every language x font x keyboard
dotnet test tests/FontSoftware.Tests
```

`tools/GapFill` writes to the tables and nothing else should:

```
gapfill --plan  <file.json>       add pairs (both directions, each judged apart)
gapfill --fix   <file.json>       correct, add or remove one pair, by hex
gapfill --drop  <encoding> <U+X>  remove every pair carrying one character
gapfill --strip-joiners           take the zero-width joiners off the legacy side
```

## What is still missing

623 legacy codes that the fonts of their encoding draw, and that can carry script,
still have no pair keyed on them. Each is a character that will fall through the
converter. They are concentrated in the dense precomposed blocks of the Ex
families - consonant + vowel sign, consonant + subjoined consonant - which the
tables only ever transcribed in part:

| encoding | codes | encoding | codes | encoding | codes |
|---|---|---|---|---|---|
| shree-kanx | 78 | shree-banx | 94 | shree-guj7 | 11 |
| shree-telx | 109 | shree-gujx | 44 | shree-pun7 | 11 |
| shree-devx | 96 | shree-orix | 33 | shree-mal7 | 10 |
| shree-ban7 | 27 | shree-punx | 16 | shree-ori7 | 6 |
| shree-kan7 | 24 | shree-malx | 13 | shree-tamx | 5 |
| shree-tel7 | 24 | shree-dev456 | 17 | shree-tam7 | 5 |

The routes left for them, in order of strength: a fresh Rosetta capture on the
keyboards that reach those codes; the block arithmetic above, relaxed to accept a
block that is only mostly parallel; and shape-matching each glyph against its
Unicode twin (`tools/GlyphJoin`).

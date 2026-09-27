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

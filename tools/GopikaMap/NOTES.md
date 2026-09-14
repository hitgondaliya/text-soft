# How the Gopika table was derived

Gopika (name table: "Prepared by Oasis Systems, 1993", built in Fontographer 4.1)
is a Gujarati **legacy** face: 186 of its 193 mapped code points are in 0x20–0xFF
and it has **no** Gujarati Unicode coverage and no private-use area. Typing
Unicode with it selected shows either boxes or whatever font Windows falls back
to, so it needs a byte table like every other legacy family we support.

None of our six Gujarati encodings fit it. Converting a word through
shree-gujx, shree-guj7, terafont-guj, isfoc-guj, akruti-guj and
bhasha-bharti-2-guj and drawing the result in Gopika produced garbage in all six.
Its byte layout also differs from LMG-Arun's (`A` is છ in Gopika, બ in LMG-Arun),
so one table cannot serve both.

## Finding the scheme

Shape matching against a Unicode face does not work here: `tools/GlyphMatch`
scored only 34/183 bytes above 0.55 against Shruti and 14/183 against Aakar, and
many of those were wrong — the designs are too far apart.

What did work: a legacy font's byte assignment **is** the keyboard layout it was
drawn for. Comparing the glyph on each letter key against all 21 Gujarati
keyboard layouts we ship gave an exact match on `guj-itr.json` (Itr, the Indian
typewriter scheme) — 17 of 17 letter keys, where every other layout scored 0 or 1.

## Verifying it

The ITR layout supplied the candidate letter for each byte; each one was then
checked by rendering the byte in Gopika beside that letter. Letters, vowels,
matras and digits matched. The shifted and high bytes did not always, and were
pinned down by typing the byte inside a real word and reading the result:

| what              | where ITR put it | where Gopika actually has it |
|-------------------|------------------|------------------------------|
| ળ                 | shift+B          | 0xA4                          |
| શ્ર                | shift+G          | 0xA7                          |
| પ્ર / ક્ર / ફ્ર      | —                | 0xA1 / 0xA2 / 0xA3            |
| ્મ / ્ય / ્ન / ્ત    | —                | B / G / L / 0xC0              |
| ઇં / ઈં             | ઙ / ઞ            | # / $                         |
| ઼ (nukta)         | —                | \|                            |

`ઙ`, `ઞ` and `ૠ` have no glyph in this font; they are listed in
`assets/config/encoding-limits.json` so the matrix run does not flag them.

## What is mapped, and what is left

182 of the font's code points draw something. 108 of them are mapped: every
consonant, vowel, vowel sign, digit, punctuation mark and the ligatures and
variants that could be confirmed. **Typing needs nothing more** — a Gujarati
keyboard cannot produce a character this table does not cover (beyond the three
the font lacks), and text typed on the machine reads back exactly.

The remaining 74 are the ligature block, roughly 0xB5–0xFF: conjunct glyphs
(દ્ધ, દ્ય, હ્ય, ટ્ટ …) and combined consonant+vowel forms (દુ, ટૂ, રૃ …) that a
DTP program substituted automatically. They appear only in files made by such a
program, never in anything typed key by key. Converting a file that contains
them leaves them as-is and the status line names them, so nothing is silently
wrong.

Four ways of naming them automatically were tried and all failed — the shapes of
a 1993 typewriter face carry too little in common with anything else we have:

| method                                                | best scores |
|-------------------------------------------------------|-------------|
| glyph vs Unicode Gujarati (Shruti / Aakar)             | 34/183 and 14/183 above 0.55 |
| glyph vs the same cluster spelled from Gopika's own bytes | 0.9 only where the byte is a duplicate; 0.2–0.4 for real ligatures |
| left half / right half vs each consonant               | 0.2–0.4 |
| glyph vs the cluster in Terafont / ISFOC / Akruti      | 0.35–0.6 |

Finishing them needs ground truth, not more guessing: one document typed in
Gopika (any .doc/.pmd with its Gujarati text known) is enough to align bytes to
letters and map the block with proof. `gopika-remaining.png`, produced by the
scratch tool in this folder, is a numbered contact sheet of the 74 glyphs that a
Gopika user can name directly.

## Shape of the table

Unlike the Shree tables (captured from Converter nxt, ~1 MB of enumerated
clusters) this one is ~105 atoms plus two regex rules per direction: the i-matra
is typed before its cluster, and ra+virama becomes a reph mark drawn after it.
Clusters the font has no ligature for are written consonant + virama + consonant,
which is what the typewriter layout itself does.

Regenerate with:

```bash
dotnet run --project tools/GopikaMap
```

Covered by `ConverterTests.UnicodeToGopika` and
`ConverterTests.RoundTripUnicodeGopikaUnicode`.

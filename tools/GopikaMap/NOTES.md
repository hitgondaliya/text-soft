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

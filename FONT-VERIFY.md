# Client fonts (D:\fonts) — verified

Names read from each font's internal metadata (the family-name table — reliable, unlike glyph names).

## ✅ Bhasha Bharati — YES (6 files, all Gujarati, legacy)
| File | Internal name | Note |
|---|---|---|
| BAMBAR_.TTF | B Bharati **Abmar**Two | Bhasha Bharati |
| BASWAD_.TTF | B Bharati **Aswad** | Bhasha Bharati |
| BKA____.TTF | B Bharati **Kautilya** | Bhasha Bharati |
| BKAB___.TTF | B Bharati **Kautilya** (bold) | Bhasha Bharati |
| BSHARMI.TTF | B Bharati **Sharmila** | Bhasha Bharati |
| B_BHAR_1.TTF | B_Bharati02 | ⚠ only 23 glyphs — **broken/incomplete file**, re-get it |

So haan — Bhasha Bharati mil gaya (Abmar, Aswad, Kautilya, Sharmila). Ye sab ek hi encoding family hai.

## ✅ Ready to use NOW (nothing to build)
| File | Name | Script | Why ready |
|---|---|---|---|
| Aakar.ttf | aakar | Gujarati | **Unicode** — install → works |
| SHRUTI.TTF / SHRUTIB.TTF | Shruti | Gujarati | **Unicode** (Windows font) |
| Amita-*.ttf | Amita | Devanagari | **Unicode** (install) |
| Biryani-*.ttf (7) | Biryani | Devanagari | **Unicode** |
| Mukta-Regular.ttf | Mukta | Devanagari | **Unicode** |
| S0700810.TTF | SHREE-DEVX-0700 | Devanagari | **Shree — map already hai** |
| S0756/0757/0758/0759/0793/1120 810.TTF | SHREE-GUJX-#### | Gujarati | **Shree — map already hai** |

These work today: install them, and in the app they render/type (Shree ones via ShreeLipi-Ex layout, Unicode ones via Unicode layout).

## 🔧 Legacy fonts that need an encoding map (auto-map does NOT work — see below)
| File | Name | Script |
|---|---|---|
| BAMBAR/BASWAD/BKA/BKAB/BSHARMI | Bhasha Bharati (Abmar/Aswad/Kautilya/Sharmila) | Gujarati |
| AABATI__.TTF | AabadTwo | Gujarati |
| AAKATI__.TTF | AakarTwo (legacy, ≠ the Unicode "aakar") | Gujarati |
| AJANTN__.TTF | AjantaTwo | Gujarati |
| ITRL0697.TTF | ITRFK-Guj-Brush | Gujarati |
| NILG02B(1).TTF | Hitarth Guj Neena | Gujarati |

## Why these can't be auto-mapped from the file (tested, not guessed)
- **Glyph names:** junk — the Gujarati glyphs are named with Latin placeholders ("mac39" etc.), so the file doesn't say which byte is which Gujarati letter.
- **Shape-matching:** I built a matcher and validated it on the KNOWN Shree-GujX map → **0% accuracy**. Legacy Gujarati fonts store character *pieces* (half-forms, split matras) that don't visually match isolated Unicode letters. So shape alone can't recover the encoding.

## The reliable way forward (needs a little help, no decryption/copying)
To build each family's map (one map per family — Bhasha Bharati is one, Aabad one, etc.), the dependable method:
1. In Shree, type a **known Gujarati text** (a fixed sentence covering all letters) using that font, and save it.
2. Give me that saved file. Its bytes are the legacy encoding; the known text is the Unicode. From that pairing I derive the byte→Unicode map exactly — then wire it → full type/convert working.

One good pangram-style sample per family is enough. Bhasha Bharati first (you have it).

# Fonts to collect from client — to make each Font Layout working

**What kind of file:** the normal font file ending in `.ttf` (or `.otf`). Just the **Regular/Normal** one of each family — no bold/italic needed. These are the same files Shree installs; the client can copy them from `C:\Windows\Fonts` or the Shree font package.

**How many:** for each family, **ONE .ttf per language** is enough for me to build the map and make that whole family type/convert. (All numbered variants like 0700, 0701… share one encoding, so one sample activates the family.)

Send the files (rename allowed) with a note saying which family + language each is.

---

## Already working — DON'T need files
- **Unicode** — every language. ✅
- **ShreeLipi-Ex** — every language (map already built). ✅ *(optional: send the .ttf only if you also want those exact fonts to render.)*
- **Shree-Lipi 7.0** — Hindi, Marathi, Sanskrit, Gujarati. ✅

---

## NEED — one .ttf each (family × language)

### Shree families (rest of languages)
| Family | Languages to send a .ttf for |
|---|---|
| Shree-Lipi 7.0 | Assamese, Bangla, Manipuri, Telugu, Kannada, Tamil, Malayalam, Odia, Punjabi |
| Shree-Lipi 4.0/5.0/6.0 | Devanagari, Assamese, Bangla, Manipuri, Gujarati, Kannada, Telugu, Tamil, Odia |

### Third-party families
| Family | Languages to send a .ttf for |
|---|---|
| Shivaji | Devanagari |
| ISFOC Monolingual | Devanagari, Gujarati, Telugu |
| Indica / Indica 2004 | Devanagari (Indica); Assamese, Bangla, Manipuri (Indica 2004) |
| Anu (+ Anu6 / Anu 7.0 / Anu 4.0) | Devanagari, Kannada, Tamil, Telugu |
| APS Monolingual | Devanagari |
| Akruti Monolingual / Akruti 98 | Devanagari (Monolingual); Odia (Akruti 98) |
| Terafont | Gujarati |
| **Bhasha Bharti-1, Bhasha Bharti-2, Bhasha Bharati** | Gujarati |
| STM / STM-BNT / STM-BAT | Assamese, Bangla, Manipuri (STM, STM-BNT); Bangla (STM-BAT) |
| Ramdhenu 3.1 | Assamese, Bangla, Manipuri |
| BIJOY | Bangla |
| Bangla Akademi 2004 | Bangla |
| KGP/Nudi Monolingual | Kannada |
| Vanavil | Tamil |
| Tamil99 Monolingual / Tamil99 Bilingual | Tamil |
| Bamini | Tamil |
| Indic Diacritical | Diacritical |

---

## Priority (client's Gujarati is what you mentioned)
Since you have **Bhasha Bharti 1 & 2** — send those Gujarati `.ttf` first, I'll make them working, then we do the rest as files arrive.

## What I do once a .ttf arrives
1. Check if it's Unicode (instant) or legacy.
2. If legacy: detect known encoding (ISFOC/C-DAC etc.) and reuse map, else derive the map from the font (glyph shape-match + script rules).
3. Wire it → type + convert + render, same as ShreeLipi-Ex.

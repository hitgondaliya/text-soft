# Font Layout list — per language (copied from Shree-Lipi Nxt Plus UI)

**Source:** Shree-Lipi Nxt Plus → top-center **Font Layout** dropdown (the one showing "ShreeLipi-Ex"), read directly from your 13 screenshots.
**Backing file in Shree:** `data\SL_FontDetails.enc` — this is **DES-encrypted (PBKDF2 key, base64)**, unreadable without Shree's own key baked into `SLNXTPLUS.exe`. That is why it could not be pulled from the files; the screenshots are the reliable source.

This is the **Font Layout** axis (font product / encoding family). It is separate from the Keyboard Layout (InScript/Phonetic/…) already done.

| # | Language | Font Layouts (in Shree's order) |
|---|----------|--------------------------------|
| 1 | Devanagari | Unicode, ShreeLipi-Ex, Shree-Lipi 7.0, Shree-Lipi 4.0/5.0/6.0, Shivaji, ISFOC Monolingual, Indica, Anu, APS Monolingual, Akruti Monolingual |
| 2 | Sanskrit | Unicode, ShreeLipi-Ex, Shree-Lipi 7.0 |
| 3 | Assamese | Unicode, ShreeLipi-Ex, Shree-Lipi 7.0, Shree-Lipi 4.0/5.0/6.0, STM-BNT, STM, Ramdhenu 3.1, Indica 2004 |
| 4 | Bangla | Unicode, ShreeLipi-Ex, Shree-Lipi 7.0, Shree-Lipi 4.0/5.0/6.0, STM-BNT, STM-BAT, STM, Ramdhenu 3.1, Indica 2004, BIJOY, Bangla Akademi 2004 |
| 5 | Manipuri | Unicode, ShreeLipi-Ex, Shree-Lipi 7.0, Shree-Lipi 4.0/5.0/6.0, STM-BNT, STM, Ramdhenu 3.1, Indica 2004 |
| 6 | Gujarati | Unicode, ShreeLipi-Ex, Shree-Lipi 7.0, Shree-Lipi 4.0/5.0/6.0, Terafont, ISFOC Monolingual, Bhasha Bharti-2, Bhasha Bharti-1, Bhasha Bharati |
| 7 | Kannada | Unicode, ShreeLipi-Ex, Shree-Lipi 7.0, Shree-Lipi 4.0/5.0/6.0, KGP/Nudi Monolingual, Anu |
| 8 | Telugu | Unicode, ShreeLipi-Ex, Shree-Lipi 7.0, Shree-Lipi 4.0/5.0/6.0, ISFOC Monolingual, Anu6, Anu 7.0, Anu 4.0 |
| 9 | Tamil | Unicode, ShreeLipi-Ex, Shree-Lipi 7.0, Shree-Lipi 4.0/5.0/6.0, Vanavil, Tamil99 Monolingual, Tamil99 Bilingual, Bamini, Anu |
| 10 | Malayalam | Unicode, ShreeLipi-Ex, Shree-Lipi 7.0 |
| 11 | Odia | Unicode, ShreeLipi-Ex, Shree-Lipi 7.0, Shree-Lipi 4.0/5.0/6.0, Akruti 98 |
| 12 | Punjabi | Unicode, ShreeLipi-Ex, Shree-Lipi 7.0 |
| 13 | Diacritical | Unicode, ShreeLipi-Ex, Indic Diacritical |

## Notes / decisions needed

1. **Unicode** is always first in every language — matches what we already made default. ✅
2. **ShreeLipi-Ex** and **Shree-Lipi 7.0 / 4.0-5.0-6.0** are Shree's own font families (we already ship Shree-DevX / Shree-GujX etc.).
3. The rest (Terafont, ISFOC, Anu, APS, Akruti, STM, Ramdhenu, Indica, BIJOY, Bangla Akademi, Shivaji, Vanavil, Tamil99, Bamini, KGP/Nudi) are **third-party font products**. To make them TYPE/RENDER (not just appear in the list) we need, per family:
   - the actual **font files** (.ttf) of that family, and
   - its **encoding→Unicode map** (so typing/converter works).
   Just to SHOW them in the dropdown (marked "not installed" when the font is absent), we need nothing more — the screenshots are enough.
4. Each Font Layout expands to specific font names in Shree's **Font Manager** (e.g. ShreeLipi-Ex → SHREE-GUJX-0750, SHREE-GUJX-0751…). If you want that inner list too, send the Font Manager screenshots per language.

## What I will build now
- A **Font Layout** dropdown per language with exactly the above entries.
- Unicode fully working universal (own font + selected language + selected keyboard).
- Shree/legacy layouts that we already have (ShreeLipi-Ex, Shree-Lipi 7.0) wired; the rest listed and marked, ready to activate as their font+map are provided.

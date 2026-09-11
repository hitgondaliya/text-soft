# What every language now shows (FONT + KEYBOARD LAYOUT)

This is computed from the exact same data and install-check the app uses. It is what the dropdowns render right now on this PC (only **Nirmala UI** Unicode font installed, plus the two bundled Shree fonts).

Two separate dropdowns — this is the key point:

- **FONT** = the actual font that renders the text. Short list (the fonts we ship / that exist). Fonts not installed are now **shown and marked "— not installed"**, never hidden.
- **KEYBOARD LAYOUT** = the big scheme list you gave me (Akruti, Godrej, CRTronic, Remington, Inscript, Phonetic, Typewriter…). **This is where the long list lives, and it is already full.**

| Language | FONT list | KEYBOARD LAYOUT count |
|----------|-----------|:---:|
| Hindi | Unicode, Shree-DevX, Mangal *(not installed)*, Kruti Dev 010 *(not installed)*, DevLys 010 *(not installed)*, Shree-Dev 0700 | **41** |
| Marathi | Unicode, Shree-DevX, Mangal *(not installed)* | **41** |
| Sanskrit | Unicode, Shree-DevX, Mangal *(not installed)* | **39** |
| Punjabi | Unicode, Shree-PunX, Raavi *(not installed)* | **12** |
| Bangla | Unicode, Shree-BanX, Vrinda *(not installed)* | **27** |
| Telugu | Unicode, Shree-TelX, Gautami *(not installed)* | **17** |
| Gujarati | Unicode, Shree-GujX, Shruti *(not installed)*, Shree-Guj 1120 | **21** |
| Kannada | Unicode, Shree-KanX, Tunga *(not installed)* | **1** ⚠ |
| Malayalam | Unicode, Shree-MalX, Kartika *(not installed)* | **1** ⚠ |
| Odia | Unicode, Shree-OriX, Kalinga *(not installed)* | **1** ⚠ |
| Tamil | Unicode, Shree-TamX *(not installed)*, Latha *(not installed)* | **1** ⚠ |

## Full KEYBOARD LAYOUT lists (the big list you asked for)

- **Hindi / Marathi (41):** InScript, Abitr, Akrphone, Akruti, Akshar, Apple, Aps 1, Aps 2, Asterix, Bpmk, Compset, Compset1, Crtronic, Devyani, Devyani2, Doe, Eng, Godrej, Godrej1, Indica, Itr, K P Rao, Marathi, Mtnk, Naidunia, Network, Newroma, Phonetic, Phonetic 86, Rajya, Remington, Remington 2, Remington 3, Roma, Script, Script1, Srg, Sulipi, Typewriter, Typewriter 2, Wx Dev
- **Sanskrit (39):** same as Hindi minus Aps 1 / Aps 2
- **Gujarati (21):** InScript, Akshar, Asterix, Bpmk, Crtronic, Doe, Eng, Indica, Itr, Mtnk, Newroma, Phonetic, Phonetic 86, Remington, Remington 1, Remington 2, Roma, Script, Sulekh 1, Sulekh 2, Typewriter
- **Bangla (27):** InScript, Akadami, Akruti, Amr1, Avro, Bangword, Bijoy, Crtronic, Doe, Eng, Godrej, Indica, Indica1, Itr, Joy, Linotype, Monotype, Newroma, Phonetic, Phonetic 86, Piyali, Roma, Stm, Suneet, Swarna, Toptype, Webel
- **Telugu (17):** InScript, Apple, Applen, Crtronic, Doe, Eng, Godrej, Indica, Itr, Micro, Mono, Newroma, Phonetic, Prakash, Roma, Typewriter, Vision
- **Punjabi (12):** InScript, Crtronic, Doe, Eng, Godrej, Itr, Newroma, Phonetic, Remington, Roma, Typewriter, Varitype

## The one real gap (⚠)

**Kannada, Malayalam, Odia, Tamil** have only **InScript** built. Shree ships more (Kannada 21, Tamil 22, Malayalam 16, Odia 8). These layouts were never generated for these 4 scripts. **This is the remaining coding work** — I have the Shree source files (`C:\Shree-Lipi nxt Plus\*.KAN/.MAL/.ORI/.TAM`) and the generator that already built the other 7 languages.

## Fixed just now
- FONT dropdown shows the **full** supported list; not-installed fonts are marked, not hidden.
- **Unicode** is always first + default, renders in Nirmala UI even with zero Shree fonts.

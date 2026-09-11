# Shree-Lipi Nxt Plus — Extraction (languages, layouts, Unicode)

Source: live install at `C:\Shree-Lipi nxt Plus`. This is data mined from the shipped files, not guesses. **No code written yet** — this is the spec you asked for before coding.

---

## 1. Languages (11)

Each language = one file extension inside Shree.

| # | Language | Code (ext) | Notes |
|---|----------|-----------|-------|
| 1 | Devanagari | DEV | covers Hindi **and** Marathi (Marathi is a keyboard layout inside DEV) |
| 2 | Sanskrit | SAN | |
| 3 | Assamese | ASS | |
| 4 | Bengali | BAN | |
| 5 | Tamil | TAM | |
| 6 | Kannada | KAN | |
| 7 | Gujarati | GUJ | |
| 8 | Telugu | TEL | |
| 9 | Malayalam | MAL | |
| 10 | Punjabi (Gurmukhi) | PUN | |
| 11 | Oriya | ORI | |

---

## 2. Keyboard layouts — per language

These are the **typing schemes** (key → character map). Each one is a real file in Shree (`NAME.DEV`, `NAME.GUJ`, …) and each ships an on-screen key-chart help file (`NAMEDEV.chm`, etc). The **keyboard layout list is fixed data from Shree** — it does NOT depend on installed fonts.

Common well-known ones you'll recognize: **Inscript** (govt standard), **Phonetic**, **Typewriter**, **Remington** (RAMING), **Tamil99 / TSCII** (Tamil), **Bijoy / Avro** (Bengali).

- **Devanagari (41):** ABITR, AKRPHONE, AKRUTI, AKSHAR, APPLE, APS_1, APS_2, ASTERIX, BPMK, COMPSET, COMPSET1, CRTRONIC, DEVYANI, DEVYANI2, DOE, ENG, GODREJ, GODREJ1, INDICA, INSCRIPT, ITR, K_P_RAO, MARATHI, MTNK, NAIDUNIA, NETWORK, NEWROMA, PHONET86, PHONETIC, RAJYA, RAMING, RAMING2, RAMING3, ROMA, SCRIPT, SCRIPT1, SRG, SULIPI, TYPEWRI2, TYPEWRIT, WX_DEV
- **Sanskrit (39):** ABITR, AKRPHONE, AKRUTI, AKSHAR, APPLE, ASTERIX, BPMK, COMPSET, COMPSET1, CRTRONIC, DEVYANI, DEVYANI2, DOE, ENG, GODREJ, GODREJ1, INDICA, INSCRIPT, ITR, K_P_RAO, MARATHI, MTNK, NAIDUNIA, NETWORK, NEWROMA, PHONET86, PHONETIC, RAJYA, RAMING, RAMING2, RAMING3, ROMA, SCRIPT, SCRIPT1, SRG, SULIPI, TYPEWRI2, TYPEWRIT, WX_DEV
- **Assamese (27):** AKADAMI, AKRUTI, AMR1, AVRO, BANGWORD, BIJOY, CRTRONIC, DOE, ENG, GODREJ, INDICA, INDICA1, INSCRIPT, ITR, JOY, LINOTYPE, MODBAN, MONOTYPE, NEWROMA, PHONET86, PHONETIC, ROMA, STM, SUNEET, SWARNA, TOPTYPE, WEBEL
- **Bengali (27):** AKADAMI, AKRUTI, AMR1, AVRO, BANGWORD, BIJOY, CRTRONIC, DOE, ENG, GODREJ, INDICA, INDICA1, INSCRIPT, ITR, JOY, LINOTYPE, MONOTYPE, NEWROMA, PHONET86, PHONETIC, PIYALI, ROMA, STM, SUNEET, SWARNA, TOPTYPE, WEBEL
- **Tamil (22):** DOE, ENG, IE, INDOWORD, INSCRIPT, ITR, KANIAN, KEYMAN, MONO, NEWROMA, PATRIKA, PHONET86, PHONETIC, PHONSCR, PRKSHAK, RAMING, RAMING1, ROMA, SPATHA, TAMIL99, TSCII, TYPEWRIT
- **Kannada (21):** ASTERIX, AVENUE, BRINDA, CRTRONIC, DOE, ENG, GODREJ, INDICA, INSCRIPT, ITR, K_P_RAO, KGP, KGPNEW, NEWROMA, PHONETIC, ROMA, SONATA, SRG, SURABHI, TYPEWRIT, VSS
- **Gujarati (21):** AKSHAR, ASTERIX, BPMK, CRTRONIC, DOE, ENG, INDICA, INSCRIPT, ITR, MTNK, NEWROMA, PHONET86, PHONETIC, RAMING, RAMING1, RAMING2, ROMA, SCRIPT, SULEKH_1, SULEKH_2, TYPEWRIT
- **Telugu (17):** APPLE, APPLEN, CRTRONIC, DOE, ENG, GODREJ, INDICA, INSCRIPT, ITR, MICRO, MONO, NEWROMA, PHONETIC, PRAKASH, ROMA, TYPEWRIT, VISION
- **Malayalam (16):** AVENUE, CRTRONIC, DOE, ENG, INDICA, INSCRIPT, ISMENG, ITR, K_P_RAO, KERGOVT, NEWROMA, PANCHARI, PHONETIC, ROMA, TYPEWRI1, TYPEWRIT
- **Punjabi (12):** CRTRONIC, DOE, ENG, INSCRIPT, ITR, NEWROMA, PHONETIC, RAMING, ROMA, TYPEWRIT, VARITYPE, GODREJ
- **Oriya (8):** CRTRONIC, DOE, ENG, INSCRIPT, NEWROMA, PHONETIC, ROMA, TYPEWRIT

---

## 3. Font layout vs Keyboard layout — the two dropdowns

Two **independent** dropdowns. This is the core model:

**Keyboard layout** = how keys map to characters (Section 2). Fixed list, from Shree, per language.

**Font layout** = which font renders the text + how the output is encoded. Two kinds:
- **Legacy (non-Unicode) font** — output is a custom 8-bit encoding tied to ONE specific font (e.g. a "Shree Dev" font). Text is readable **only if that exact font is installed**. This is why on the client laptop with one font installed, only one shows — correct behavior, but fragile.
- **Unicode** — output is standard Unicode codepoints, rendered by **any** Unicode font that supports the script.

> Same keyboard layout (e.g. Inscript) can drive **either** a legacy font **or** Unicode. Keyboard layout and font layout are separate.

---

## 4. Unicode — the missing feature (the real fix)

**What it must do:** every language must have a **Unicode** option in the font-layout dropdown. When Unicode is selected:
- typing still uses the chosen **keyboard layout**,
- output = Unicode text,
- it renders with **whatever font the client already has** — including the client's own private font — no Shree font needed.

**Why you need it in every language:** client often has only their own/private font. With legacy font layout, nothing works unless our exact font is installed. With Unicode selected, their font just works. This is exactly what you described.

**Font-layout dropdown behavior:**
- List installed fonts that support the language script (that's why only 1 shows when only 1 installed — that part is right).
- **Always** add "Unicode" as an entry, regardless of installed fonts.
- When Unicode picked → enumerate all Unicode fonts on the machine that cover the script (Mangal, Nirmala UI, client private font, etc.) and let the client pick.

---

## 5. Shree modules (for the later "which integrated / which remaining" phase)

Executables found in the install (maps to the launcher tiles in your screenshot):

| Module | exe | Purpose |
|--------|-----|---------|
| Font Manager | FontManager.exe / MFM_SL7.exe | install/manage fonts |
| Compose (typing editor) | Compose32/64.exe | main type-in-layout editor |
| Keyboard Generator | KBD32.exe | keyboard layout tool |
| Roopa | Roopa_SlNxt.exe | Roopa SL7 fonts |
| Caligrafer | Shree-Lipi Caligrafer.exe | calligraphy |
| Translate | TranslateNXT.exe / TransNXT.exe | translation |
| Speech to Text / TTS | TTSNXT.exe | speech |
| OCR | OCRNXT.exe | OCR |
| PDF to Image | PDFToImage.exe | convert |
| Patrika | Patrika_CS.exe | Patrika keyboard/layout |

Spell checker data present (SpellData: 10 `.aff` + 10 `.dic`). OCR data present (10 `.traineddata`).

---

## 6. What is blocked

`SL_FontDetails.enc`, `SL_LangDetails.enc`, `KeyShortcuts.enc` etc. are **encrypted** (base64 ciphertext). We cannot read Shree's exact font-per-layout table from them. But we don't need it: the keyboard-layout lists above are the fixed data (readable), and font layout is driven by installed fonts + Unicode — which we generate ourselves.

---

## Next step (waiting your OK before I code)

1. Confirm the model above matches Shree (esp. font-layout = installed-fonts + always-Unicode).
2. Then I wire into TextSoft: language dropdown → keyboard-layout dropdown (data above) + font-layout dropdown (installed fonts + Unicode), and implement the Unicode typing path so any installed font works.
3. Then audit which TextSoft modules already exist vs remaining.

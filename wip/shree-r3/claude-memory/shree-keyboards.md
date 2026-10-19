---
name: shree-keyboards
description: "Which keyboards Shree-Lipi actually has, where each one's truth lives, and the traps in reading their help files"
metadata:
  node_type: memory
  type: reference
  originSessionId: 69d8b2a2-ed7d-447f-9ed1-ba585383f6f5
  modified: 2026-09-27T05:07:33.588Z
---

**The vendor's keyboard list is the CHM list**, one help file per keyboard in
`C:\Shree-Lipi nxt\data\<script>\`: ban 28, dev 40, guj 21, kan 22, mal 17, ori 9,
pun 13 (+ PATPUN/TYPEPUN, which are manuals, not keyboards), tam 23, tel 18.
TextSoft's list now matches it exactly, plus the user's own designs.

- **Modular** exists for every script as a chart with no runtime file — and it is
  *not* the composer's built-in table, which is InScript.
- Gujarati **Ekl**, Malayalam **Typewri1** (the vendor's own typo, `Typewirt1MAL.chm`),
  Telugu **Applen** (`AppleEnTel.chm`) are chart-only too.
- **Aps 1/2 and Sulekh 1/2 were never Shree keyboards** — early guesses, now gone.
- **Assamese has 15 keyboard files of its own** (`*.ASS`, incl. `MODBAN.ass`, which
  is Modular: 47/48 against the Bengali Modular chart) with no help files: one
  Bengali chart serves both, and ৱ/ৰ differ. Capture them through the *Bengali*
  composer with the `.ASS` path.

**Two traps in the help files**, both fixed in `tools/ChmMap`:
1. a literal `<` in a keystroke cell starts a fake HTML tag — `<d<` was read as
   `d` and put a conjunct on the virama key of every Kannada/Telugu keyboard;
2. punctuation keystrokes are written as numeric entities (`&#60;`), so stripping
   entities from the keystroke column loses the key.
Character cells can also hold a C1 control (a glyph number in some old symbol
font) — drop those.

A chart repeating itself (one character on two keystrokes, one keystroke with two
characters) is the tell that a row lost its own character; see
[[shree-tutor-api]] for what to do then. `docs/KEYBOARDS.md` in the repo has the
whole procedure and the four commands that rebuild the layouts.

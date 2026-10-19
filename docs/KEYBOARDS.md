# Where the keyboards come from

Every keyboard TextSoft types with is the vendor's own. Nothing in
`assets/config/layouts/` is hand-written or guessed; each key traces back to one
of Shree-Lipi's two accounts of itself, and `tools/KbdAudit` prints the ledger.

## The two sources

**The published charts.** Each keyboard has a help file in the install
(`C:\Shree-Lipi nxt\data\<script>\TypewritGUJ.chm`) whose pages are a table of
keystroke → character: single keys, multi-key runs ("jf" → જિ), the third layer
written as `Ø` + key, and a "special keys" table for the symbols every keyboard
of a script shares (Ctrl+Shift+1). This is what a typist is taught.
`tools/ChmMap` extracts them.

**The composer.** Each script has a DLL (`S8GujU64.dll`) whose Tutor exports
answer, key by key, what the loaded keyboard types — including the keys no chart
prints. It loads standalone: no SLNXT, no hook, no window, so a capture runs
while the machine is in use. `tools/KbdCapture` asks it; see the
`shree-tutor-api` memory for the call shapes.

A keyboard's runtime file (`INSCRIPT.GUJ`) is the third voice. It holds an
internal code per key rather than Unicode, but joining every chart of a script
with its own runtime file gives code → character, and that reading settles ties.

## Which one wins

The chart, by default. Asked about one key on its own the composer answers with
the state that key leaves the text in — a matra where a typist is taught the
vowel, a half form where the chart prints the letter — and our multi-key runs are
built on the chart's reading.

The exception is a chart row that repeats itself: one character on two
keystrokes, or one keystroke with two characters. One of those rows has lost its
own character, and then the composer is the one that still knows what the key
types. That tell, plus the runtime file as a third voice, is what puts the nukta
back on the Kannada `]` (the charts print an avagraha), the subjoined consonants
on the Punjabi keyboards (no Punjabi chart writes them down), the reph on the
Kannada `@`, and the half forms on the Punjabi Eng keyboard whose backtick the
chart itself uses to undo them.

`Vendor.Compare` in `tools/Shared/Vendor.cs` names every kind of difference and
`Vendor.ComposerWins` holds the rule.

## Languages with a file of their own

Assamese has fifteen keyboard files beside the Bengali ones — `INSCRIPT.ASS` is
`INSCRIPT.BAN` with ৱ where Bengali has ব — and the vendor writes one help file
for the two. Captures of those live under `capture/ass/`, this language's own
file has the last word on the keys where the two differ, and the letters it
changes change inside the Bengali chart's runs as well.

Sanskrit has its own composer (`S8SanU64.dll`) but answers exactly as Devanagari
does, so the Sanskrit layouts are the Devanagari ones.

## Running it

```bash
# 1. the charts, out of the help files (needs 7-Zip)
dotnet run --project tools/ChmMap -- --shree "C:\Shree-Lipi nxt" --out <chm dir>

# 2. the composer, keyboard by keyboard
dotnet run --project tools/KbdCapture -- --script guj --kbd INSCRIPT.GUJ \
    --out <capture dir>/guj/INSCRIPT.json

# 3. the charts into the layouts, then the composer over them - in that order
dotnet run --project tools/KbdFromChm -- --chm <chm dir> --scheme "C:\Shree-Lipi nxt" --apply
dotnet run --project tools/KbdFromCapture -- --chm <chm dir> --capture <capture dir> \
    --scheme "C:\Shree-Lipi nxt" --apply

# 4. the ledger
dotnet run --project tools/KbdAudit -- --chm <chm dir> --capture <capture dir>
```

`KbdFromChm` writes the chart's reading everywhere; `KbdFromCapture` then fills
the keys no chart prints and applies the exceptions above. Running the second
without the first leaves the exceptions unapplied, and the audit will say so.

## Where it stands

327 keyboards across 13 languages. Against the charts: 13,741 single keys and
37,960 multi-key runs, none different, none missing. Against the composer: 27,989
keys and 7,529 Ctrl+Space keys, three different, none missing.

Those three are keys where the help file and the composer name different
characters and nothing breaks the tie — Bengali Akruti's Ctrl+Space `d`, Telugu
Itr's `J`, Kannada Surabhi's `&`. The chart stands on all three.

Sixteen keyboards have a chart but no capture: the vendor documents them and
ships no runtime file for them (Modular in every script, Gujarati Ekl, Malayalam
Typewri1, Telugu Applen). They are complete from their charts.

A link key - code 64 in a vendor file (the tutor's "Lk"), the virama; code D7
("Hf"), virama + ZWJ - is typed as such on every Gujarati keyboard; six keys fell
through to something else and were set (scratchpad r3\guj-fix2\linkfix.js). On
Phonetic every consonant key already types its virama: a link key struck after a
letter that has its virama adds only its joiner (the engine's Emit, and
TypingOrder.Compose for keyboards typed out of Unicode's order), so k Q k a is
ક્‍ક, BC 4C 24 in SHREE-GUJ7 and SHREE-GUJ, as Shree writes the codes 73 D7 73.

## The order a keyboard is typed in

A chart shows how a sign goes onto a letter once, on ka, and that run is all the
layout carries: ITR's "rf" is કિ, "fo" is ર્ક, "ftu" is કો. Typed on any other
letter, the keys simply ran together - r then h gave િર where the keyboard means
રિ, and the reph landed behind its letter. `TypingOrder`
(`src/FontSoftware.Core/Keyboards/TypingOrder.cs`) reads those runs off each
layout and the engine applies them to every consonant:

- a sign struck before its letter (the i-sign on every typewriter-style keyboard)
  goes behind that letter's whole cluster, once the cluster is complete;
- the reph struck after its letter goes in front of it - only on keyboards that
  also strike the i-sign first. InScript's chart has "k$" for र्क too, but its
  typists strike the reph first, in Unicode's order, and that must stay as typed;
- a key that types a vowel alone types the vowel's sign after a consonant
  (Newroma's "e" is એ, "re" is રે);
- two signs a run joins (ા then ે is ો) are joined on every letter.

The engine keeps the word as struck and puts it on screen in Unicode's order, so
a legacy font is sent exactly what the converter makes of the right word, and
Backspace takes back the last key struck. A keyboard whose runs teach none of
this is typed exactly as before.

Two data gaps were closed alongside. The chart-only keyboards (Modular in every
script, Malayalam Typewri1, Telugu Applen, a key of Hindi Remington) had sign
keys that appear only in a run on ka and typed nothing alone; each now types the
sign its run shows. And Gujarati was the one script without the rupee on
Ctrl+Shift+1 then `$` - every keyboard now has it there, as all the others do.
In a Shree-Lipi 7 or 4/5/6 font, which has no rupee sign (Shree's own composer
writes 3C, where the fonts have no glyph), the key types ₹ itself, U+20B9, and Word
draws it in a fallback font - never nothing (the output policy, P3b, in
docs/CONVERTER.md).

## Importing a typist's own Shree keyboard

Keyboards -> "Import Shree-Lipi file..." reads a 406-byte `.GUJ`/`.DEV`/... file
(`ShreeKeyboardFile`) through `assets/config/shree-kbd/<SCRIPT>.json`, which says
what each of Shree's internal codes types. That table was read out of Shree's
Unicode composer, and a few codes only its legacy-font composer types: a client's
BPMK1 put the ru and ruu ligatures (E9, EA) on Shift+5 and 2, and the import left
both empty. Two rules came out of it:

- A code the table cannot read is named in the import's summary ("type nothing
  here: Shift+5"), never dropped without a word. When one turns up, ask the X
  composer what it types (`tools/KbdCapture --family X`), add the script's glyph
  offset, look the glyph up in `mappings/shree-<x>-to-unicode.json`, and add the
  code to the table.
- A layer the file leaves empty is written `""`, which types nothing (and the key
  is swallowed). A layer that is simply not set (`null`) means "Shift types the
  normal character" - right for our own layouts, wrong for an import, where it
  made Shift+5 type the 5 key's letter. The keyboard designer writes `""` too when
  a layer is cleared while the other still types.

Backspace takes back exactly the last keystroke (`TypingEngine._undo`, one state of
the whole engine per key): Shift+A's half letter બ્ whole, one key inside a chart run
(InScript k s Backspace leaves ક), a key that typed nothing (an empty slot, an unused
letter, a character the font cannot show), and - until the caret moves or a key goes
to the application - back into a word that a key of ours finished: a mark typed
through the font's table ('"' is 22 22), a fixed key or a run on the layer of the Eng
keyboard (Ctrl+Space n is ઙ્, taken back whole). A shortcut never rewrites the word:
the half form Shree shows while a virama waits stays, and nothing is sent under the
held Ctrl, Alt or Windows key.

### The two shapes of ruu, and the keys for them

Shree's Gujarati fonts draw ruu twice: the old shape (U+E3CA, like a 3 with a
tail) and the new looped one (U+E3C6), plus ru (U+E3C5). Ra then the uu sign
gives neither in Shree's own legacy typing - its keyboards have keys for them
(old ruu: ITR Shift+H, InScript Shift+J, Remington 1 Shift+Z, Script Shift+?, Roma
Shift+-, Crtronic Shift+;, and Ctrl+Space on Akshar x, Asterix j, BPMK v, DOE o,
MTNK l, Newroma l/b, Phonetic r/b, Remington and Remington 2 v, Typewriter a; new
ruu: Typewriter Shift+;, Phonetic Shift+4, DOE Ctrl+Space n; ru: Typewriter
Shift+3, Phonetic Shift+2, DOE Ctrl+Space b). The charts drew those keys as
pictures, so every one of them was missing here; they are back.

Unicode has one ruu. The new shape is written `રૂ` + U+FE00 (variation selector
1, invisible): `unicode-to-shree-gujx` gives it U+E3C6, plain `રૂ` stays U+E3CA,
and a font with one ruu gets that one - `FontConverter` drops a variation
selector its table has no entry for.

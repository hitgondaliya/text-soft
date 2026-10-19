---
name: shree-import-legacy-codes
description: Shree codes only the legacy composer types (GUJ E9 ru, EA/7E ruu) were missing from shree-kbd tables; import rules for empty slots and unread codes (2026-10-08)
metadata:
  type: project
---

Client bug 2026-10-08: their own BPMK1.GUJ (vendor BPMK + 22 bytes) put રુ on
Shift+5 (code E9) and રૂ on 2 (code EA). assets/config/shree-kbd/GUJ.json is
read from Shree's UNICODE composer, which answers nothing for those codes - only
the legacy X composer types them (glyphs E3C5, E3C6; X answer + 0xE200). So the
import left Shift+5 empty, the engine's "null Shift = normal char" rule typed પ
(the 5 key's letter) = "shift + key gives the normal key's word", and 2 typed an
English 2.

Fixed (uncommitted at the time, in TextSoft-Setup-1.0.0-2026-10-08b.exe): GUJ
7E/E9/EA (+3C), and verified codes for ORI/TAM/TEL/KAN/MAL added; import names
unread codes in its summary; an empty slot is written "" (= types nothing, key
swallowed, except in excluded apps); 0x00 = nothing like 0xFF; Backspace pops
whole keystrokes (_pieces) in legacy, order and Unicode paths; a mouse click
(MouseClickWatch, own thread) makes the engine forget the word.

**How to apply:** an import complaint -> diff the file against vendor files, run
tools/KbdCapture --family X/U on it (short path), compare with our import. Codes
still unidentified: GUJ EE (dash glyph E41A), PUN 41/4D/E1, ORI 51/EE, TEL
E1/E7/E9/EB, KAN D7/EB/E7/E8. Old imports must be re-imported (delete old first,
else it becomes "BPMK1 (2)"). See [[shree-keyboard-scheme-files]], [[shree-tutor-api]].

2026-10-10: the client's "missing ru" was the OLD ruu shape (glyph E3CA, a "3
with a tail" - also the "INR-like" glyph of their very first question) and the
new looped shape (E3C6) on their '2' key. Shree's 7E/EA/E9 keys were missing in
all 16 shipped Gujarati layouts (charts drew them as pictures) - added. New shape
= "રૂ"+U+FE00 -> E3C6 in shree-gujx; FontConverter drops unmapped VS. Shipped in
TextSoft-Setup-1.0.0-2026-10-10.exe (still uncommitted). Client said another issue
is coming next.

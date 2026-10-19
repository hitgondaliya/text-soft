---
name: keyboard-typing-order
description: Charts teach i-sign/reph/vowel signs on ka only; TypingOrder learns them per layout and the engine applies them to every consonant (2026-10-07)
metadata:
  type: project
---

Found 2026-10-07 while answering "where is રૂ / ₹ on the Gujarati keyboard": the
layouts carried each chart's "Applying Vowel Signs" runs for ka only, so on every
typewriter-style keyboard (ITR, Typewriter, Remington, Akshar... in all scripts)
r then h typed િર (not રિ) and the reph landed behind its letter, in Unicode output.
Legacy-font output looked right only by accident (visual order = font byte order).

Fix: `src/FontSoftware.Core/Keyboards/TypingOrder.cs` (+ `KeyboardLayout.Order`,
engine `_raw`/`_word`/`ShowWord`): i-sign struck first moves behind its cluster;
reph struck after moves in front (ONLY when the layout also strikes the i-sign
first - InScript's chart has "k$"=र्क but its typists type the reph first);
vowel key after a consonant types its sign (Newroma); learned sign joins (ા+ે=ો).
Data: Gujarati got ₹ on Ctrl+Shift+1 then $ (the only script without it); 131
chart-only sign keys (Modular etc.) that typed nothing alone were filled.

**How to apply:** if a keyboard "only works on ક", it is this class. Tests:
TypingEngineTests (TheISignStruckFirst..., InScriptKeepsTheRephWhereItIsTyped).
Live-tested 2026-10-08 (also on the obfuscated publish build); shipped in TextSoft-Setup-1.0.0-2026-10-08.exe (commit a829162). See [[shree-keyboards]],
[[live-testing-rules]].

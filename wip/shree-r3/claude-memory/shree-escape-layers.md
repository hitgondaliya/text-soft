---
name: shree-escape-layers
description: "Shree keyboards have two layers past Shift, and how their charts encode them (the Ø glyph inside a keystroke)"
metadata: 
  node_type: memory
  type: project
  originSessionId: 69d8b2a2-ed7d-447f-9ed1-ba585383f6f5
  modified: 2026-09-22T19:24:07.295Z
---

Shree-Lipi keyboards have **four** layers, not two. Beyond Normal and Shift:

- **Ctrl+Space** — the keyboard's own third layer, different on every keyboard:
  nukta letters, conjuncts, alternate forms (ITR Devanagari: `a`=क़, `i`=त्र, `u`=श्र).
- **Ctrl+Shift+1** — symbols every keyboard of a script shares: ॐ ।  ॥ ₹ ऽ ़ ॰ ×  ÷ — –.

They are **different key assignments** — on ITR dev, Ctrl+Space+u is श्र while
Ctrl+Shift+1+u is ॐ. Merging them is wrong.

**How the CHM charts encode it:** the keystroke column prints the escape as a
literal `Ø` (U+00D8, `&#216;`) inside the run — `Øj` = escape then j, `kØqk` =
k, escape, q, k. 4,100 chart cells use it. Proof it is not Shift: the byte
scheme (`.BAN`/`.DEV`) gives a *different* character for the shifted key
(BANGWORD: `j`=জ, `J`=য, `Øj`=ঝ). The "Special Keys" table at the end of each
chart page is the Ctrl+Shift+1 layer, written as "Ctrl+shift+1, k".

Our storage mirrors that: the marks live inside `sequences` keys
(`EscapeLayer.Mark` = U+00D8 for Ctrl+Space, `SymbolMark` = U+00A4 for
Ctrl+Shift+1), so the run machinery handles them; `escape`/`symbols` in each
layout file are the one-key entries again, for the viewer only.

Also: the help pages are windows-1252, not UTF-8 — read them as declared or the
dashes and × come out U+FFFD. `KBD32.exe` (Shree's keyboard generator) refuses
to run unless launched from SLNXT, so the charts are the way in.

See [[shree-keyboard-scheme-files]], [[textsoft-coverage-status]].

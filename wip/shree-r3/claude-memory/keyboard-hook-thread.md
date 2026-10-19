---
name: keyboard-hook-thread
description: Hook moved off the UI thread + watchdog (2026-10-07) after a demo PC typed "another key's character" until restart
metadata:
  type: project
---

Client report 2026-10-07: on a demo PC, after a while keys typed a *different
key's character* (not nothing), fixed by restarting TextSoft.

Cause found in code: the WH_KEYBOARD_LL hook lived on the WPF UI thread, and file
conversion (FileConversion.ConvertFile, Word for .doc) runs synchronously on that
thread. Windows 7+ silently removes a low-level hook that misses its timeout, and
the app never noticed. From then on raw keys reach the document; in a legacy font
(Shree, Terafont, BB) the raw ASCII byte shows as some other Gujarati glyph -
which is why it looked like the wrong key rather than English.

Fix: `src/FontSoftware.Engine/KeyboardThread.cs` - hook + engine on their own
thread (all MainViewModel engine calls go through `_keyboard.Invoke`, Toggled is
marshalled back to the UI), plus a 100 ms watchdog: a key down that the hook never
saw go down means Windows dropped it -> `Hook.Reinstall()`, `TypingEngine.Resync()`,
one warning in the log per minute.

Verified 2026-10-08 with tools/HookProbe: on this Windows 11 PC a hook held up
2-12 s (21 timeouts in a row) was NOT removed - keys only went through raw while
it was held. So "Windows dropped the hook" is not proven for the demo PC; raw
keys during a UI stall are. Simulated drop (unhooked behind the app): the
watchdog put it back after the first key, 6/7 reached it. Watchdog needs two
missed ticks (a key struck while the thread is busy is only late).

**How to apply:** if the client reports it again, get the logs folder
(%APPDATA%/TextSoft/logs) from that PC - new builds log "Keyboard hook: a key went
down that the hook was never called for". Alternative to rule out: the
Phonetic/type-by-sound toggle (not persisted). Shipped in
TextSoft-Setup-1.0.0-2026-10-08.exe (commit b96047d). See [[live-testing-rules]],
[[keyboard-typing-order]].

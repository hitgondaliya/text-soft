---
name: live-testing-rules
description: How to test TextSoft for real on the user's PC without getting in their way (TypeProbe, when not to run, no stray windows)
metadata:
  type: feedback
---

Test the running product, not just unit tests: the user said "tu test karta hain sab green green bol deta hain, me jab krta hoon tab bug hi bugs" — green unit tests are not evidence. Use `tools/TypeProbe` (real keystrokes into TextSoft with the hook installed).

**Why:** twice the unit tests passed while the real app failed (GetKeyState in a global hook; Ctrl held through Ctrl+Space). And on 2026-09-28 a probe run sent keys into the user's own window while they typed, and every run left a Notepad window behind — the user was angry ("alag alag notpad ki window kyu open kr raha hain").

**How to apply:**
- TypeProbe now types into its own window (Pad.cs), never Notepad, and stops if the foreground window is not the test's (Guard). Keep it that way.
- Screen/keyboard/clipboard tests (TypeProbe, convdrive.ps1 for Shree's Converter, UI clicks) only when the user has said they are away, or right before, with a warning. If the user is active, stop.
- Restart TextSoft for a profile with `taskkill /F` — a plain close only hides it in the tray and leaves a second hook running.
- TextSoft's main-window tab content is not in the UI Automation tree; click buttons by physical-pixel coordinates in a DPI-aware process (screen is 1920x1080 at 125%).
- Word on this PC is the free read-only edition — see [[word-free-edition-this-pc]].
- If the user left another app (e.g. Explorer) in front, the Windows foreground lock stops TypeProbe's pad taking focus (it STOPs safely, nothing typed); it works once the Claude app is in front. Write PowerShell test scripts ASCII-only (PS 5.1 reads BOM-less UTF-8 as ANSI).

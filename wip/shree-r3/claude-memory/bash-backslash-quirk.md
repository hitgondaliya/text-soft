---
name: bash-backslash-quirk
description: In this environment the Bash tool turns a double backslash into a single one inside commands (even quoted heredocs) — write scripts that need backslashes via the Write tool.
metadata:
  node_type: memory
  type: reference
  originSessionId: 69d8b2a2-ed7d-447f-9ed1-ba585383f6f5
  modified: 2026-09-30T20:28:44.147Z
---

Observed 2026-10-01: a double backslash (two backslash characters) typed in a Bash tool command
— in a heredoc quoted with 'EOF', in node -e, in sed — reaches the program as ONE backslash.
So a JS escape written to produce a literal backslash-u became a real \u escape (syntax errors,
or literal private-use glyph characters landing in C# source), sed patterns lost their escapes,
and a Windows path written with doubled separators never matched.

**How to apply:** any script containing backslashes (regex escapes, \u escapes, Windows paths)
→ write it with the Write tool (it stores text exactly), then run it with Bash. Inside JS, a
backslash can also be built as String.fromCharCode(92). Very long heredocs fail too ("name too
long") — use the Write tool for those as well.

Related PowerShell trap: variable names are case-insensitive, so a script parameter `$H`
(height) and a later `$h = <window handle>` are ONE variable - a resize test once sized the
window to the handle value. Use distinct names (`$hw`).

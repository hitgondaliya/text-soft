---
name: shree-7-vs-456-split
description: "Shree-Lipi 7.0 and 4/5/6 are separate font families with their own tables; Gujarati done 2026-10-11, Devanagari/Sanskrit/Bangla in progress; SAN7 is its own encoding"
metadata:
  node_type: memory
  type: project
  originSessionId: 624671f5-ee6d-402b-b810-05b52f236677
  modified: 2026-10-19T03:57:38.557Z
---

Client bug (2026-10-10): the font list showed both Shree-Lipi 7.0 and 4/5/6 fonts under either layout, and BPMK u,1 (ગ્ર) typed wrong bytes in both.

**State on 2026-10-11 (uncommitted in main):**
- The font list is split: a `families` regex on each Shree row, plus `FontLayout.Includes` and MainViewModel `FillFonts`.
- Gujarati tables were rebuilt from Shree's composer ground truth by tools/Guj7Truth:
  - unicode-to-shree-guj7 and guj456 plus their reverse tables. SHREE-GUJ-* fonts now use the shree-guj456 encoding.
  - Results: 17,335/17,335 truth strings, 20k/20k words.
  - Tests: ShreeComposerTruthTests with Data/shree-guj7-truth.json, and the TypingEngineTests theory `AShreeFontGetsTheBytesShreeItselfWrites` (BPMK u1 = 4E B0).
  - Full suite 324/324; ConfigCheck 0 errors.

**Still in progress:** workflow shree-7-456-finish.
- Devanagari: the old dev456 table matched Shree on only 15,922 of 27,175 strings.
- Sanskrit: SHREE-SAN7 fonts are NOT DEV7-encoded and need a san7 table.
- Bangla: needs a ban456 table.
- Ground truth for all three is in the session scratchpad: shree7\dev7, san7 and ban7 -groundtruth.json.

**Round 3 (2026-10-18):** a key-by-key Gujarati audit against the composer (r3\guj-audit) found 15 bugs: Backspace eating a whole chart run (engine-wide), રુ/રૂ ligatures, English .docx runs restamped, Eng/Ekl keyboards, 'A or B' chart runs, EE dash, ASCII drawn as Gujarati, ₹/॥. They were fixed in a copy and merged into main with Bangla/Assamese. Policy P1-P6 plus P3b: a character no font code draws (₹, °) passes through as Unicode. Live test 13/13 passed. Shipped Desktop\TextSoft-Setup-1.0.0-2026-10-18-Gujarati.exe, built from r3\merge-check1\repo (main after the merge). Inno needs a short path (junction Temp\txg).
Still open then: merge fix round 2 (Gujarati Phonetic Shift+Q, Bangla '.' drop and '@' read-back, dev ऄ ऎ), and the six-script tables (Punjabi, Odia, Telugu, Kannada, Tamil, Malayalam), each in its own copy under r3\tbl-*, still to be merged. The old tables matched the composer poorly: Odia 32%, Tamil 55%, Punjabi 91%.
**STOPPED by the user on 2026-10-19 09:30 to save their weekly limit (67% used).** Main holds Gujarati + Bangla, merged and checked clean (961/961 tests). The six scripts are not in main yet:
- Punjabi: done and clean (r3\tbl-pun).
- Odia: round 2 built; check2 cut off (r3\tbl-ori).
- Telugu: round-2 build cut off (r3\tbl-tel).
- Kannada, Tamil, Malayalam: captured (r3\cap-*); tables not started.
To continue, resume workflow wf_7ed24c18-f3d (script six-scripts-tables-wf_7ed24c18-f3d.js; it ends with a final merge into main). The scratchpad is session 7673923b. Do not restart it without the user's go-ahead: it costs a lot of their weekly quota. The PC slept 03:10-09:21 that night (lid or sleep button).
**The usage limit, not RAM, caps throughput:** four agents used a 5-hour quota in about 75 minutes.

**This PC has only 7.5 GB RAM.** Three agents building and testing at once ran out of memory and crashed, so run at most two.

**Why:** a round-trip test (Unicode→legacy→Unicode) passes even when the table is wrong. Only byte-exact tests against Shree's composer catch it. The user wants everything fixed before getting the exe ("sab fix krke hi dena").

**How to apply:** ship only after each script's byte tests pass and a skeptic has re-measured, then run the live TypeProbe test (scratchpad live-shree7.ps1, only while the user is idle) and installer/build-release.ps1. See [[shree-tutor-api]], [[live-testing-rules]], [[shree-keyboard-scheme-files]].

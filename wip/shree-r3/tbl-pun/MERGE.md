# Punjabi (Gurmukhi) Shree-Lipi 7 and 4/5/6: merge notes (round 2)

- **Copy:** `scratchpad\r3\tbl-pun\repo`.
- **Paths:** every path below is relative to the repo root.
- **Round 1's notes:** `round2\MERGE-round1.md`.

## 0. What this copy is based on (read this first)

### The rebase

Round 1 was built on `scratchpad\snap-base`. Main had moved on a lot since then:
- the engine's undo stack, the Ctrl+Space fix and the legacy table for unhandled keys;
- the shared non-Indic policy;
- the Bangla merge.

So at the start of round 2 I re-based the copy onto **a snapshot of main taken at 19:41**. The snapshot is in `scratchpad\r3\tbl-pun\round2\main-base`: 890 files (src, tests, tools, assets, FontSoftware.sln, logo.png), no bin or obj. Its rebase log is `round2\rebase.log`.

- **Every file not listed in sections 1 to 3 is byte-identical to main@19:41.** For those files, keep main's version.
- **Two files were taken from main later, at 20:07 and 20:13** (see below).
- **Base for a three-way merge:** use `round2\main-base`. Using `snap-base` also works, because every Punjabi edit below is re-applied by a script or is a self-contained file.

### Main changed again while I worked

1. `tools/Shared/ShreeNonIndic.cs`:
   - My copy is **main's 20:07 version, unedited**, md5 `620ecdc5760ba09c18d3b82e1a72cbb2`. It adds StandIns and `PassedThrough`.
   - **Keep whatever main has.** Then re-run the generator (section 4).
2. `src/FontSoftware.Engine/TypingEngine.cs`:
   - My copy is main@19:41 (md5 `b10509c263897e328fe549bacf830a0d`) plus my patch, `round2\TypingEngine.diff` (9 hunks).
   - Main's 20:13 version (md5 `4e6a8a55e31c3f97a52d50e69e208ffe`, saved as `round2\main-2013\TypingEngine.cs`) adds a link-virama de-duplication in `Emit` and logging in `RerenderLegacy`.
   - **My patch applies cleanly to it:** `patch --dry-run` showed every hunk succeeding with line offsets only and no fuzz.
   - **To merge:** take main's file and apply `round2\TypingEngine.diff`.

## 1. Files added (Punjabi only; take mine)

| File | What it is |
|---|---|
| `tools/PunTruth/` (PunTruth.csproj, Common.cs, Capture.cs, Family.cs, Rules.cs, Learner.cs, Reader.cs, Checks.cs, CrossCheck.cs, Fixtures.cs, Fonts.cs, Ascii.cs, Build.cs, Program.cs) | The generator. It is not in FontSoftware.sln. It links `tools/Shared/ShreeWordFinal.cs` and `tools/Shared/ShreeNonIndic.cs`. |
| `assets/config/mappings/unicode-to-shree-pun456.json` | Generated: 311 pairs. |
| `assets/config/mappings/shree-pun456-to-unicode.json` | Generated: 126 pairs. |
| `tests/FontSoftware.Tests/ShreePunjabiTruthTests.cs` | Two classes, `ShreePunjabiTruthTests` and `ShreePunjabiTypingTests` (the latter in `[Collection("typing engine")]`). 93 tests. |
| `tests/FontSoftware.Tests/Data/shree-pun7-truth.json` | Fixture written by `puntruth fixture`: 27,752 rows, md5 `3fd19ff2c7d65fdd45cc655a7c34c253`. |
| `tests/FontSoftware.Tests/Data/shree-pun-cmaps.json` | Written by `puntruth cmaps`. md5 `12e02e8199d99f20b12f2c1463e366f8`. |

## 2. Files changed that only Punjabi touches (take mine, or re-run the script)

- **`assets/config/mappings/unicode-to-shree-pun7.json` (311 pairs) and `shree-pun7-to-unicode.json` (125 pairs):** generated. Take mine, or re-run the generator.
- **`assets/config/layouts/`:** apply the two scripts in this order.
  1. `round1\patch-layouts.js <layoutsDir>`: the round 1 edits. 11 files. Its log is `round1\layout-fixes.txt`.
  2. `round2\patch-shift.js <layoutsDir> [C:\Shree-Lipi nxt]`: **new.**
     - It sets 64 slots to `""` (types nothing). In each, Shree's own `.PUN` file has byte FF and the layout had no entry for that Shift state, so the engine typed the unshifted key's character, or let the raw key through.
     - The script checks every slot against the `.PUN` byte and throws if the slot is already set. Its log is `round2\patch-shift.txt`.
     - Layouts changed:
       - pun-crtronic, pun-doe, pun-godrej, punjabi-inscript, pun-itr, pun-raming, pun-typewrit: 38 slots.
       - pun-eng, pun-newroma, pun-phonetic, pun-roma: 26 slots.
     - **pun-eng.json is new to the changed list in this round.** pun-modular and pun-varitype are not changed.
- **`assets/config/translit/punjabi-shree-eng.json`:** `round2\patch-eng.js <file>`. **New in this round.** It keeps CRLF line endings, and I checked it reproduces my file byte for byte.
  - Shree's own ENG.PUN keys that typed nothing (or raw Latin) are added:
    - consonants: R ੜ, z ਝ, H ਹ, V ਵ, W ਵ (after a letter, H, V and W give the subjoined ੍ਹ and ੍ਵ, as Shree's do);
    - specials: C ੱ, Q ੰ.
  - Vowel `"aa": ["ਆ","ਾ"]` is added, as gujarati-shree-eng.json has it. Before this, no key sequence could type ਾ after a letter: `kaa` typed ਕਅ.
  - The special `"a": "ਾ"` is removed. It collided with the vowel `"a"`, and `List.Sort` (unstable) decided which one won.
  - **Check:** `TransliterationTests` "kamala" → ਕਮਲ still passes.
- **`tests/FontSoftware.Tests/ShreeNonIndicTests.cs`:** the round 1 edits (the NonIndic part of `round1\patch-tests.js`). Main has not changed this file since snap-base.

## 3. Shared files: the exact edits to re-apply on main's current version

- **`src/FontSoftware.Engine/TypingEngine.cs`:** apply `round2\TypingEngine.diff` (74 changed lines). It makes three changes.
  1. **A key that types nothing keeps the word.** This covers a letter key the layout does not use, and a slot set to `""`.
     - **Before:** main typed nothing too, but `CommitWord()` ended the word. The next sign started a new word, so DOE v d w (ਨੈ) gave B2 BE B1 where Shree and the table write B2 B1 BE.
     - **Now:** the key is swallowed, the run in progress is restored from `before`, an undo entry is pushed, and the method returns.
     - **Code:** the block before `if (string.IsNullOrEmpty(output) && replace == 0)`. It is guarded by `!typedSilent`, a new local set where the `silent` keys were emitted.
  2. **A key typed on its own in a legacy font is spelled after what it follows.**
     - **Code:**
       - `TableText(typed, context)` and the new `ConvertAfter(context, text)`: `conv(context + text)` without `conv(context)`.
       - `TypeAlone(text, context)`: when the context changes the spelling, it injects through `InjectLegacy` directly. `RerenderLegacy` is not touched.
     - **Why:** an ASCII quote opens after a space and closes after a letter. Converted alone, the apostrophe of ਪ' came out as the opening 22, where the converter writes 27.
  3. **`_prevOut`:** the last character before the caret.
     - It is set in `CommitWord()`, on a key the engine lets through (a space for the space bar), and reset in `ForgetWord()` and on a shortcut.
     - `TextBefore()` returns the word in progress, or else `_prevOut`. So '3' followed by ' gives 33 27, as the converter writes it.
- **`tests/FontSoftware.Tests/ShreeFontCmapTests.cs`:** two edits on main's 18:39 version.
  1. In `Families`, after the shree-san7 row, add these two rows:
     `{ "raavi", "shree-pun7", "^SHREE-PUN7-", "shree-pun7-truth.json" },`
     `{ "raavi", "shree-pun456", "^SHREE-PUN-", "shree-pun7-truth.json" },`
  2. In `PassedThrough(...)`, add `&& c != '·'`, plus the comment.
     - **Why:** the shared StandIns policy writes · as B7, which is U+00B7 itself and is reported. Without this exception the test reports "maps U+00B7, which the table passes through as itself".
     - **Main will need this edit too.** Main's own unicode-to-shree-guj7.json (20:27) now has the same pair `"·" -> "·"` unmapped. My copy's Gujarati table is the 19:41 one, so I could not see the Gujarati failure here.
- **`assets/config/languages.json`:** main's file, then run `node round1\patch-languages.js <languages.json>`. It rewires 5 SHREE-PUN-* fonts to shree-pun456 and lists the two new tables.
- **`tools/RegisterFonts/Program.cs`:** main's file, then run `node round1\patch-reg.js <Program.cs>`. The edits are as in round 1.
- **`assets/config/encoding-limits.json`:** main's file, then re-run the generator. It rewrites `"shree-pun7"` and adds `"shree-pun456"`; no other key changes.
  - `shree-pun7` = `0023 0024 0026 003C 003E 0040 005C 005E 005F 0060 007B 007C 007D 007E 00B0 0A3C 0A51 0A75 0A76`
  - `shree-pun456` = the same plus `20B9`.
- **The test csproj and ConfigCheck need no change.**

## 4. Commands to re-run after the merge (from the repo root, one build at a time)

```
dotnet build tools/PunTruth/PunTruth.csproj -c Release -nodeReuse:false -p:UseSharedCompilation=false
tools/PunTruth/bin/Release/net8.0/puntruth.exe fixture   # Data/shree-pun7-truth.json (needs the capture: env PUN7_GROUNDTRUTH or r3\cap-pun)
tools/PunTruth/bin/Release/net8.0/puntruth.exe cmaps     # Data/shree-pun-cmaps.json (needs the installed faces)
tools/PunTruth/bin/Release/net8.0/puntruth.exe build     # the 4 tables + the 2 encoding-limits keys, and every check
tools/PunTruth/bin/Release/net8.0/puntruth.exe verify    # regenerate into temp, compare byte for byte (exit 0 = identical)
tools/PunTruth/bin/Release/net8.0/puntruth.exe cv shree-pun7 10 1
dotnet test tests/FontSoftware.Tests -c Release --filter "FullyQualifiedName~Shree|FullyQualifiedName~TypingEngine|FullyQualifiedName~GujaratiAudit"
```

PunTruth calls these from the shared file:
- `NonIndic.Complete(MappingTable, IEnumerable<char>, string, string)`, which now also adds StandIns;
- `NonIndic.Check(FontConverter, FontConverter, ISet<char>, string, out List<string>, string blank = "")`;
- `NonIndic.LeaveOutControls(MappingTable)`;
- `NonIndic.PassedThrough(FontConverter, int)`.

If the merged shared file renames any of them, adjust `tools/PunTruth/Build.cs`.

The md5 values after `build` on this copy (with the 20:07 shared file):

| File | md5 |
|---|---|
| unicode-to-shree-pun7 | 94b01d904de2fcd3fd4326a38d3a1e74 |
| shree-pun7-to-unicode | 8484a7814b3d34f25f72f69d2817e274 |
| unicode-to-shree-pun456 | ee7b94fa9fca718412584a27cc7bc13d |
| shree-pun456-to-unicode | 37bb88f072c137a3917e8d9ca0a2beb1 |
| TypingEngine.cs | 38bb4cdd7d9d252949b0668346945c64 (main@19:41 + diff) |

## 5. What changed in the generator in round 2 (all inside tools/PunTruth)

- **`Ascii.cs`:** now the same policy as main's `tools/BanTruth/Ascii.cs`.
  - The **Latin letters, `x`, `{`, and `<` in both families** are left out and reported. Before this round, the letters and `x`/`{` passed through as their own codes, which draw Gurmukhi letters (C 43 = ੳ, H 48 = ਕ).
  - Unicode spaces (U+1680, U+2000-200A, U+202F, U+205F, U+3000) are written as the space 20.
  - The soft hyphen is left out and reported.
  - · and ∙ are left to the shared StandIns: B7, a stand-in, reported.
- **`Build.cs`:**
  - `NonIndic.LeaveOutControls`: C0 (except TAB LF VT FF CR), DEL and C1 are left out and reported.
  - Explicit P3b pass-through pairs, marked unmapped, for ੑ ੵ ੶ and, in 4/5/6, ₹.
  - P5: the reverse tables read those characters as themselves, unreported (`NonIndic.PassedThrough(fwd, 0x0A00)`).
- **`Rules.cs` and `Reader.cs`:** the `NoMarksIn` rule. A noncharacter U+FDD0-U+FDDF in the input becomes U+FFFD, reported, never one of the tables' marks.
- **`Capture.cs` (`HalantBeforeZwj`) and `Learner.cs`:** a virama before a ZWJ is the visible halant B5, as before a ZWNJ.
  - ਕ੍‍ → 48 B5. ਵਾਕ੍‍ → 65 70 48 B5. ਕ੍‍ਰ → 48 B5 63.
  - 38 fixture rows (C + virama + ZWJ) now prefer their ZWNJ twin's spelling. The composer's link spelling (the bare letter) stays accepted.

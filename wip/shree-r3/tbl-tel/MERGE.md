# Telugu Shree-Lipi 7 and 4/5/6: merge notes

- **Copy:** `scratchpad\r3\tbl-tel\repo` (robocopy of `scratchpad\snap-base`: src, tests, tools, assets, FontSoftware.sln).
- **Paths:** every path below is relative to the repo root unless it starts with `round1\` / `round2\` (= `scratchpad\r3\tbl-tel\round1|round2`).
- **Base for a three-way merge:** `scratchpad\snap-base`. Every shared-file edit below is also a script that applies to
  main's current file (each was dry-run against main on 2026-10-19 00:55-01:10, see section 3), so main's own later
  changes are kept.
- **Nothing in `src/` was changed.** No engine code was touched (TypingEngine.cs, TypingOrder.cs are snap-base's).

## 0. Files in the copy that are main's, not mine (keep main's)

| File | What it is |
|---|---|
| `tools/Shared/ShreeNonIndic.cs` | main's 2026-10-18 20:07 version, unedited (md5 `620ecdc5760ba09c18d3b82e1a72cbb2`, identical to main at 2026-10-19 00:55). The generator was built and run against it. **Keep main's** and re-run the generator (section 4). |
| `tests/FontSoftware.Tests/ShreeFontCmapTests.cs` | main's version (md5 `ba0ecf52...`, with `PassedThrough`) **plus my 2 rows** (section 3). |
| `assets/config/layouts/tel-itr.json`, `tel-newroma.json` | started from main's 2026-10-18 11:41 versions (the chart "A or B" runs split), then my layout patch. Applying `round1\patch-layouts.js` to main's files gives my files byte for byte. |
| `logo.png` (repo root) | copied from main only so FontSoftware.App builds for the test run. Not a change. |

## 1. Files added (Telugu only; take mine)

| File | What it is |
|---|---|
| `tools/TelTruth/` - TelTruth.csproj, Ascii.cs, Build.cs, Capture.cs, Checks.cs, Common.cs, CrossCheck.cs, Family.cs, Fixtures.cs, Fonts.cs, Learner.cs, Program.cs, Reader.cs, Rules.cs | The generator (exe `teltruth`). Not in FontSoftware.sln (as Guj7Truth/BanTruth/PunTruth). Links `tools/Shared/ShreeWordFinal.cs` and `tools/Shared/ShreeNonIndic.cs`. |
| `assets/config/mappings/unicode-to-shree-tel7w.json` | Generated, 770 pairs (+23 pre rules, 1 post rule), md5 `419eb90563a0a6ad9a81cb62e7a10ebd`. For Shree-Tel7-001/002 (WTEL7001/7002.TTF), which draw a star at 92 where the other faces draw the rupee sign. |
| `assets/config/mappings/shree-tel7w-to-unicode.json` | Generated, 634 pairs, md5 `c4694e63755a818a83f91a5c53f6b774`. |
| `tests/FontSoftware.Tests/ShreeTeluguTruthTests.cs` | Two classes: `ShreeTeluguTruthTests` and `ShreeTeluguTypingTests` (`[Collection("typing engine")]`). md5 `6bb7d961697a0fdc757a62ce9fc9c140`. |
| `tests/FontSoftware.Tests/Data/shree-tel7-truth.json` | Written by `teltruth fixture`: 26,618 rows (20,383 the composer's own bytes, 6,235 documented exceptions), md5 `d9e886bd8f10516cd98f868046442a6b`. |
| `tests/FontSoftware.Tests/Data/shree-tel-cmaps.json` | Written by `teltruth cmaps` (every installed SHREE-TEL7/SHREE-TEL/Shree-Tel7-00x face), md5 `1194be4944443dcc8c771a03153a472b`. |

## 2. Files changed that only Telugu touches (take mine, or re-run the script)

- **`assets/config/mappings/unicode-to-shree-tel7.json`** (770 pairs, was 18,235; md5 `1b5ec5c2a126069c6e71f1a45a807865`) and
  **`shree-tel7-to-unicode.json`** (633 pairs, was 16,925; md5 `c2c1c7570dbb34e40025dca8e8e26b35`): generated. Main still has
  the shipped ones (md5 7aa6ce74 / 4778c126 = snap-base). Take mine or re-run the generator.
- **`assets/config/layouts/`** (16 files: tel-apple, tel-crtronic, tel-doe, tel-eng, tel-godrej, tel-indica, tel-itr,
  tel-micro, tel-mono, tel-newroma, tel-phonetic, tel-prakash, tel-roma, tel-typewrit, tel-vision, telugu-inscript;
  tel-applen and tel-modular unchanged): `node round1\patch-layouts.js <layoutsDir> [C:\Shree-Lipi nxt]`.
  It reads Shree's own `<KB>.TEL` file for every slot it touches and throws on anything unexpected; idempotent.
  Fresh on main's layouts it makes 114 edits (61 empty slots, 8 7-only slots); log of the round-1 part:
  `round1\layout-fixes.txt`, full log on main: `round2\dry-main2\log.txt`.
  1. INDICA / InScript `^` `*`: త్ర, శ్ర (were the chart's visual order ్రత, ్రశ).
  2. The ై key alone (Shree code 6D) on 12 layouts + NEWROMA Ctrl+Space e: ఐ (was the composer's placeholder ఐ్ / ఐ‌ / ఐొ, which typed Cఐ్ after every letter but క).
  3. ఎ without the ZWNJ the chart cell carried (DOE S, GODREJ F, INDICA Z, InScript Z, ITR :).
  4. TYPEWRIT @ = ్ఞ (Shree slot 64 D2); PRAKASH Shift+D = ్ (link 64); ITR a e i q r w = "" (FF in ITR.TEL; the chart repeated its Ctrl+Space row there).
  5. The halant key (Shree 44) types ్‌ (virama + ZWNJ, the visible halant) on 15 layouts, 4 of them on Ctrl+Space (was the bare virama = a conjunct with the next letter).
  6. 61 slots Shree's file leaves FF and the layout left unset (the engine typed the unshifted key or the raw key into the Shree font): "".
  7. **(round 2, new)** 8 unset slots whose Shree code is one only the 7 composer types (E1, a ra-vattu hook written after the syllable; the Unicode composer types nothing): "" - APPLE Shift+Q (typed అ), DOE Shift+X (ం), ENG Shift+2 (౨; ENG is phonetic, so no effect there), InScript Shift+B (వ), NEWROMA Shift+W (వ), PHONETIC Shift+2 (౨), ROMA Shift+X (ష), VISION Shift+. (a full stop).
- **`assets/config/translit/telugu-shree-eng.json`**: `node round1\patch-eng.js <file>` (keeps CRLF; reproduces my file byte for byte from main's, which = snap-base). Log `round1\eng-fixes.txt`: "e" = ఎ/ె (the bare-sign special removed), A I U V Y ` vowels, B C F G J K P Q W X q w consonants, specials # > ] [ < Z, as Shree's ENG.TEL types them.
- **`tests/FontSoftware.Tests/ShreeNonIndicTests.cs`** (main = snap-base): the NonIndic part of `node round1\patch-tests.js <testsDir>`:
  the rows `{ "gautami", "shree-tel7" }, { "gautami", "shree-tel7w" }`; `set.Add('\u0090')` for the Telugu tables (the faces map byte 90 at U+0090, a blank, the same glyph as A0); the letter క for shree-tel*; two `InlineData` rows (`“తెలుగు”` and `తెలుగు… •`).

## 3. Shared files: the exact edits to re-apply on main's current version (all dry-run on main, 2026-10-19)

- **`assets/config/languages.json`**: `node round1\patch-languages.js <languages.json>`. Exactly 4 lines: the fonts
  `shree-tel7-001` and `shree-tel7-002` (Shree-Tel7-001/002) `"encoding": "shree-tel7"` -> `"shree-tel7w"`, and after
  `"mappings/unicode-to-shree-tel7.json",` the two lines `"mappings/shree-tel7w-to-unicode.json",` `"mappings/unicode-to-shree-tel7w.json",`.
  SHREE-TEL7-* and SHREE-TEL-* (4/5/6) stay on shree-tel7: their faces are identical (family check), one table serves both.
- **`assets/config/encoding-limits.json`**: re-run the generator (it now writes only its own blocks, as text, so no other
  key is re-serialised), or `node round1\patch-limits.js <copy's encoding-limits.json> <main's>`. It replaces `"shree-tel7"` and adds `"shree-tel7w"` after it:
  - `shree-tel7` = `0023 0024 0026 002A 003C 003E 0040 005B 005C 005D 005E 005F 0060 007B 007C 007D 007E 00B0 00D7 00F7 0C00 0C04 0C0C 0C34 0C3C 0C44 0C55 0C58 0C59 0C5A 0C5D 0C61 0C62 0C63 0C77 0C78 0C79 0C7A 0C7B 0C7C 0C7D 0C7E 0C7F`
  - `shree-tel7w` = the same without `002A` (`*` is the star 92 there), plus `20B9` (no rupee glyph: passed through, reported).
- **`tests/FontSoftware.Tests/ShreeFontCmapTests.cs`**: the cmap part of `round1\patch-tests.js`: in `Families`, after the shree-san7 row:
  `{ "gautami", "shree-tel7", "^SHREE-TEL7?-", "shree-tel7-truth.json" },`
  `{ "gautami", "shree-tel7w", "^Shree-Tel7-00[12]$", "shree-tel7-truth.json" },`
  (patched main = my file, byte for byte.)
- **`tools/RegisterFonts/Program.cs`** (main's version, with `familyToEnc`; my copy's RegisterFonts is snap-base's, unedited):
  `node round1\patch-registerfonts.js <Program.cs>`: `["Shree-Tel7-001"] = "shree-tel7w", ["Shree-Tel7-002"] = "shree-tel7w",` after the Shree-Ass7 line, and `["shree-tel7w"] = new[] { "Telugu" }` in `encToLangs`. Compile-checked (0 errors) on a copy of main's file.
- **Test csproj, ConfigCheck, FontSoftware.sln: no change.** Main's newer ConfigCheck adds the "literal 'or' in a key run" error: the Telugu layouts have 0 such runs.

## 4. Commands to re-run after the merge (from the repo root, one build at a time)

```
dotnet build tools/TelTruth/TelTruth.csproj -c Release -nodeReuse:false -p:UseSharedCompilation=false
tools/TelTruth/bin/Release/net8.0/teltruth.exe fixture   # Data/shree-tel7-truth.json (needs the capture: env TEL7_GROUNDTRUTH or scratchpad\r3\cap-tel\tel7-groundtruth.json)
tools/TelTruth/bin/Release/net8.0/teltruth.exe cmaps     # Data/shree-tel-cmaps.json (needs the installed faces; env TEL_FONTDIR)
tools/TelTruth/bin/Release/net8.0/teltruth.exe build     # the 4 tables + the 2 encoding-limits keys, and every check
tools/TelTruth/bin/Release/net8.0/teltruth.exe verify    # regenerate into temp, compare byte for byte (works without the capture too)
tools/TelTruth/bin/Release/net8.0/teltruth.exe cv shree-tel7 10 1
dotnet test tests/FontSoftware.Tests -c Release --filter "FullyQualifiedName~ShreeTelugu|FullyQualifiedName~ShreeNonIndic|FullyQualifiedName~ShreeFontCmap|FullyQualifiedName~TypingEngine"
```

TelTruth calls from the shared files: `NonIndic.Complete(MappingTable, IEnumerable<char>, "22", "27")`,
`NonIndic.LeaveOutControls(MappingTable)`, `NonIndic.InvisibleInWord`, `NonIndic.PassedThrough(FontConverter, 0x0C00)`,
`NonIndic.Check(FontConverter, FontConverter, ISet<char>, string, out List<string>)`, `WordFinal.Spell(...)`.
If the merged shared file renames any of them, adjust `tools/TelTruth/Build.cs` / `Capture.cs`.

md5 after `build` on this copy (shared file 620ecdc5): unicode-to-shree-tel7 `1b5ec5c2a126069c6e71f1a45a807865`,
shree-tel7-to-unicode `c2c1c7570dbb34e40025dca8e8e26b35`, unicode-to-shree-tel7w `419eb90563a0a6ad9a81cb62e7a10ebd`,
shree-tel7w-to-unicode `c4694e63755a818a83f91a5c53f6b774`. `verify` = byte-identical for all 4 tables and encoding-limits.json
(2026-10-19 00:56); `fixture` and `cmaps` re-run = byte-identical.

## 5. Engine bugs found by the audit (NOT fixed here; for the engine owner)

1. **Ra-vattu struck first (TypingOrder).** Shree's charts strike the ra-vattu ్ర (code D7) BEFORE its consonant
   (runs CRTRONIC `Tw`, GODREJ `Øfj`, ITR `Øgf`, MICRO `Qe`, MONO `&e`, ROMA `Rk` = క్ర; TYPEWRIT `K'>` = ష్ట్ర). The
   chart teaches it on క only; `TypingOrder.Learn` takes only one-character pre-signs, so on every other letter the
   engine types `్ర` + C (malformed Unicode, e.g. ROMA `RpcM` = ్రపచం U+0C4D 0C30 0C2A 0C1A 0C02), and after a syllable
   it joins the PREVIOUS consonant: TYPEWRIT `mKjk` (చ ్ర క ం) = చ్రకం, bytes 7B 5E E8 FE 4D E6 FC 85, where Shree types
   చక్రం 5E E8 FE 7B 4D E6 FC 85 (the hook lands on చ). At a word start / after ం the legacy bytes come out right only
   because the converter writes `్ర`+C as 7B + C. Fix idea: learn a two-character subjoined form struck before its
   letter (alone[k1] = virama + consonant, run k1 k2 = alone[k2] + alone[k1]) as a pre-element, with a two-character
   stand-in, flushed after the whole cluster (TYPEWRIT `Kra` = స్త్ర, Shree E7 DC 88).
2. **Ctrl+Space fallback:** with no Ctrl+Space entry the engine types the key's own character: 1,164 Telugu positions where Shree types nothing.
3. **Phonetic (ENG) punctuation passes through raw:** 39 positions; `@ [ ] { } ~ < > #` etc. reach the Shree font as their own bytes (40 = a talakattu tick, 5D = the visarga, 7B = the ra-vattu hook, 7D = శ్రీ). The transliterator's specials `# > ] [ <` (patch-eng.js) are never consulted for those keys (`telaudit type tel-eng "]"` = 5D, unhandled).
4. As in the Gujarati audit, Backspace after a key that completed a chart run removes the whole run, in all 3 profiles:
   DOE `k e <BS>` (కా) leaves nothing where Shree leaves క (4D E6 FC); DOE `k d j <BS>` (క్ర) and ROMA `R k <BS>` leave nothing.
   (No Telugu run starts with a key that types nothing alone: ITR a e i q r w, now "", start no run.)

## 6. Measured on this copy (real FontConverter / TypingEngine; details in the final report)

- Truth 26,618 rows: preferred bytes 26,618/26,618 in both families (20,383 the composer's own, 6,235 documented
  exceptions, `Capture.Exceptions`); shipped table 18,978 (71.30%), composer's own 14,127/26,546 (53.22%).
- 20,000 joined words x seeds 7, 11, 2026: 100% (shipped 37.85% / 38.16%). 10-fold hold-out (seeds 1, 2): 25,851/25,852.
- Reverse: truth 25,959 exact + 659 same text, 0 wrong; whole capture 37,339 spellings: 0 Latin/control (shipped 941).
- Per-key audit (round1\audit, results\l1-r5-full.txt): 3,008 positions on 16 vendor keyboards / 18 layouts; word plans
  5,529/5,529 typed = converter bytes (3 profiles).
- Filtered tests 286/286 (round2\test-filtered.txt); whole suite 622/622 after the last edit (round2\test-full.txt); ConfigCheck 0 errors, 123 warnings (= snap-base's 123), 60 tables.

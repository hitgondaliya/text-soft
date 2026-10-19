# Odia (Shree-Lipi 7.0 and 4.0/5.0/6.0): merge notes

- **Copy:** `scratchpad\r3\tbl-ori\repo` (robocopy of `scratchpad\snap-base`: src, tests, tools, assets, FontSoftware.sln; no bin/obj).
- **Merge base:** `scratchpad\snap-base`. Every path below is relative to the repo root.
- **Main has not touched any Odia file since snap-base** (checked 2026-10-18 21:5x): the 9 Odia layouts, `translit/odia-shree-eng.json`, both `shree-ori7` tables and the Odia entry of `languages.json` are byte-identical in main and snap-base. So every Odia file below can be taken from my copy as it is.
- **Merge rehearsal:** main's `assets/` (copied 2026-10-18) + the two scripts below + my two tables + my `shree-ori7` limits key, checked with main's ConfigCheck: **0 errors, 123 warnings** (main alone: 0 errors, 123 warnings). Files: `round1\merge-rehearsal`, `round1\merge-rehearsal-cc-main-*.txt`.

## 1. Files added (Odia only; take mine)

| File | What it is |
|---|---|
| `tools/OriTruth/` (OriTruth.csproj, Ascii.cs, Build.cs, Capture.cs, Checks.cs, Common.cs, CrossCheck.cs, Family.cs, Fixtures.cs, Fonts.cs, Learner.cs, Policy.cs, Program.cs, Reader.cs, Rules.cs) | The generator (`oritruth`). Not in FontSoftware.sln. Links `tools/Shared/ShreeWordFinal.cs` and `tools/Shared/ShreeNonIndic.cs`. |
| `tests/FontSoftware.Tests/ShreeOdiaTruthTests.cs` | Two classes: `ShreeOdiaTruthTests` (table tests) and `ShreeOdiaTypingTests` (`[Collection("typing engine")]`, the real TypingEngine). 113 tests. md5 `ae3c34b0880c8ef8d3582eb1f44ae562`. Uses `RecordingInjector` from TypingEngineTests.cs (present in snap-base and main). |
| `tests/FontSoftware.Tests/Data/shree-ori7-truth.json` | Written by `oritruth fixture` from the capture: 41,470 rows. md5 `5081a6da94504b4737dbe6f0d2f951c7`. |
| `tests/FontSoftware.Tests/Data/shree-ori-cmaps.json` | Written by `oritruth cmaps` from C:\Windows\Fonts (12 faces). md5 `ad98a623c2287ced433fab6a7b45ae87`. |
| `logo.png` (repo root) | **Not a change**: copied from main only so the App project (referenced by the tests) builds in the copy. Main has it. Do not merge. |

## 2. Files changed that only Odia touches (take mine, or re-run the scripts)

- **`assets/config/mappings/unicode-to-shree-ori7.json` (941 pairs) and `shree-ori7-to-unicode.json` (258 pairs):** written only by the generator. md5 `8d6409c07c0faf1012a89604db763378` / `b039fba7b29d11ff1ef8ebb2719e1424`. One table pair serves both families (Family.cs).
- **`assets/config/layouts/`** - 8 files: `odia-inscript.json ori-crtronic.json ori-doe.json ori-eng.json ori-newroma.json ori-phonetic.json ori-roma.json ori-typewrit.json` (`ori-modular.json` is unchanged).
  - Apply `node round1\patch-layouts.js <layoutsDir> "C:\Shree-Lipi nxt"` on main's layouts. It needs the .ORI files and the capture `r3\cap-ori\ori-vendor-keys.json` (env `ORI_VENDOR_KEYS` to move it). 88 changes from snap-base; a second run makes 0. Log: `round1\layout-fixes.txt`.
  - Checked: from snap-base's layouts the script reproduces my 8 files byte for byte (`round1\layouts-scratch`).
  - Rules A-I are in the script's header: A empty slots (Shree FF) that typed raw ASCII / the unshifted letter / a Latin chart letter -> "" (49); B link key code 64 -> ୍ (InScript d, DOE d, Newroma =, Roma f, Typewriter d); C half key D5 -> ୍‍ (Crtronic Z, DOE Z, Newroma F, Phonetic Q); D hook code 51 -> "" (DOE =, Typewriter Q); E Ctrl+Space E1 -> ୍ବ (Crtronic l, DOE i, Eng b, Newroma b, Phonetic b, Roma d); F InScript Ctrl+Space q/w -> % ‘; H Newroma run ko -> କୋ; I 23 chart runs that contradict their own keys removed.
- **`assets/config/translit/odia-shree-eng.json`:** apply `node round1\patch-eng.js <file>` (keeps CRLF, no final newline). 24 changes, second run 0. Log: `round1\patch-eng.txt`. Check: `TransliterationTests` "kamaLa" -> କମଳ still passes.

## 3. Shared files: exact edits to re-apply on main's current version

- **`assets/config/encoding-limits.json`:** take main's file, then re-run the generator (`oritruth build`): it rewrites the `"shree-ori7"` key only. New value:
  `0023 0024 0026 002B 003B 003C 003D 003E 0040 005B 005C 005D 005E 005F 0060 007B 007C 007D 007E 00B0 00D7 00F7 0B35 0B3C 0B3D 0B44 0B55 0B61 0B62 0B63 0B70 0B72 0B73 0B74 0B75 0B76 0B77 20B9` (was `0B35 0B3C 200C 20B9`).
- **`tests/FontSoftware.Tests/ShreeNonIndicTests.cs`** (main = snap-base, md5 a9af8a23...): four edits.
  1. `Tables`: after the line `{ "mangal", "shree-dev7" }, { "mangal", "shree-dev456" }, { "mangal", "shree-san7" },` add `{ "kalinga", "shree-ori7" },`.
  2. `FontCodes`: before `if (encoding is "shree-guj7" or "shree-guj456") set.UnionWith(...` add `if (encoding == "shree-ori7") set.UnionWith("\u008F\u0090");` (the Odia faces map ୰ ଽ at U+008F U+0090).
  3. `NoTableWritesALetterForTextThatIsNotIndic`: the letter becomes `encoding.StartsWith("shree-guj") ? "ક" : encoding.StartsWith("shree-ori") ? "କ" : "क"`.
  4. `PunctuationIsWrittenAsTheLookAlikeTheFontsDraw`: after the shree-san7 "राम…" row add
     `[InlineData("kalinga", "shree-ori7", "“ଓଡ଼ିଆ”", "22 22 48 DD E7 41 E6 27 27")]` and `[InlineData("kalinga", "shree-ori7", "ରାମ… •", "C0 FF E6 FE 2E 2E 2E 20 B7")]`.
- **`tests/FontSoftware.Tests/ShreeFontCmapTests.cs`:** my copy is **main's 18:39 version** (md5 ba0ecf52..., with `PassedThrough`) plus two `Families` rows after the shree-san7 row:
  `{ "kalinga", "shree-ori7", "^SHREE-ORI7-", "shree-ori7-truth.json" },`
  `{ "kalinga", "shree-ori7", "^SHREE-ORI-", "shree-ori7-truth.json" },`
- **`tools/Shared/ShreeNonIndic.cs`:** my copy is **main's 20:07 version, unedited** (md5 `620ecdc5760ba09c18d3b82e1a72cbb2`). Keep whatever main has, then re-run the generator. OriTruth calls `NonIndic.Complete(MappingTable, IEnumerable<char>, string, string)`, `NonIndic.LeaveOutControls(MappingTable)`, `NonIndic.PassedThrough(FontConverter, int)`, `NonIndic.Check(FontConverter, FontConverter, ISet<char>, string, out List<string>, string)` and the `Common.Hex/FromHex` it needs (OriTruth's Common has both). If a merged version renames them, adjust `tools/OriTruth/Build.cs`.
- **`tools/ConfigCheck/Program.cs`:** keep **main's**. My copy is snap-base's plus main's noncharacter hunk only (a pair from U+FDD0-FDEF that writes nothing is not an error: the table's WaitingMark pair `U+FDEF -> ""`), so the copy's ConfigCheck could pass without main's 'A or B' check, which flags 124 runs in other scripts' snap-base layouts (none Odia).
- **Not changed:** `languages.json` (SHREE-ORI7-* and SHREE-ORI-* are already on `shree-ori7`), the test csproj (Data\** is copied already), `tools/RegisterFonts/Program.cs` (ORI7 and ORI already map to shree-ori7), the engine, FontConverter.

## 4. Commands to re-run after the merge (repo root, one build at a time)

```
dotnet build tools/OriTruth/OriTruth.csproj -c Release -nodeReuse:false -p:UseSharedCompilation=false
tools/OriTruth/bin/Release/net8.0/oritruth.exe fixture   # Data/shree-ori7-truth.json (needs the capture: env ORI7_GROUNDTRUTH or r3\cap-ori\ori7-groundtruth.json)
tools/OriTruth/bin/Release/net8.0/oritruth.exe cmaps     # Data/shree-ori-cmaps.json (needs the installed faces)
tools/OriTruth/bin/Release/net8.0/oritruth.exe build     # both tables + the shree-ori7 limits key, and every check (log it)
tools/OriTruth/bin/Release/net8.0/oritruth.exe verify    # regenerate into temp, compare byte for byte (exit 0 = identical)
tools/OriTruth/bin/Release/net8.0/oritruth.exe cv 10 1
node <round1>\patch-layouts.js assets/config/layouts "C:\Shree-Lipi nxt"   # 0 changes when already applied
node <round1>\patch-eng.js assets/config/translit/odia-shree-eng.json      # 0 changes when already applied
dotnet test tests/FontSoftware.Tests -c Release --filter "FullyQualifiedName~Shree|FullyQualifiedName~TypingEngine|FullyQualifiedName~Transliteration"
```

Expected from `build` (this copy): truth 41,470/41,470 preferred; words 20,000/20,000 on seeds 7, 11, 2026; word-final 20,000/20,000; leaks 0; 214 code points written, 12 faces, 0 missing from a cmap; 0 written that Word draws as nothing; reverse 41,467 read, 0 wrong; non-Indic 62,046, 0 wrong; ASCII 0 wrong.

## 5. Two decisions a reviewer should know

1. **ନ is written 5B FF, not the composer's raw 9C FF.** All 12 installed faces draw the ligature ଙ୍କ୍ତ at 9C and ନ at 5B (rendered by glyph index and in WPF: `round1\glyphs3.png`, `sheet-00.png` row 47, `sheet-01.png` row 101, `sheet-02.png` row 148; advances 9C 566, 5B 341, FF 163). 1,427 truth rows carry ନ. Every other raw byte of the truth draws its text (259 spellings checked by eye). The reverse table reads 9C as ଙ୍କ୍ତ.
2. **One table for both families; ₹ ୰ ଽ pass through (P3b).** 3C (₹) is only in WORI7001/2, which share the names SHREE-ORI7-0603/0604 with S0603892/S0604892; the other faces show Word's '<' there. ୰ ଽ sit at U+008F U+0090, C1 code points Word draws as nothing. The reverse table still reads 3C 8F 90.

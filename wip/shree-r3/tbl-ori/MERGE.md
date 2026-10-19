# Odia (Shree-Lipi 7.0 and 4.0/5.0/6.0): merge notes (round 2)

- **Copy:** `scratchpad\r3\tbl-ori\repo`. Every path below is relative to the repo root unless it starts with `round1\` / `round2\` (= `scratchpad\r3\tbl-ori\round1|round2`).
- **Round 1's notes:** `round2\MERGE-round1.md` (superseded by this file).

## 0. What this copy is based on - read this first

- **Rebased onto main.** Round 1 was built on `scratchpad\snap-base`. At the start of round 2 the copy was re-based onto **a snapshot of main taken 2026-10-19 01:22**: `round2\main-base` (890 files: src, tests, tools, assets, FontSoftware.sln, logo.png; no bin/obj/node_modules). The Odia files of round 1 were put back on top (main had not touched any of them). Main was checked again at 02:36: unchanged (TypingEngine, TypingOrder, FontConverter, encoding-limits, languages.json, ShreeNonIndic, ConfigCheck, the shared tests, the Odia layouts and tables are byte-identical to the snapshot).
- **Every file not listed in sections 1-3 is byte-identical to `round2\main-base`.** Keep main's version of those.
- **Base for a three-way merge: `round2\main-base`.** (snap-base also works for the Odia-only files, which main never touched.)
- **The Punjabi round-2 engine patch is applied in this copy** (`r3\tbl-pun\round2\TypingEngine.diff`, 9 hunks, unchanged): its "a key that types nothing keeps the word" and "a key typed alone is spelled after what it follows" are exactly two of the Odia defects (empty key inside a word; the quote after a letter). The Odia engine edits are a separate diff on top of it (section 3). If the Punjabi copy is merged first, its hunks are already in main and only `round2\diffs\TypingEngine-ori.diff` remains to apply.

## 1. Files added (Odia only; take mine)

| File | md5 | What it is |
|---|---|---|
| `tools/OriTruth/` (OriTruth.csproj, Ascii.cs, Build.cs, Capture.cs, Checks.cs, Common.cs, CrossCheck.cs, Family.cs, Fixtures.cs, Fonts.cs, Learner.cs, Policy.cs, Program.cs, Reader.cs, Rules.cs) | Build.cs 41a165ff, Reader.cs 8168a1b2, Family.cs 4323d3fc, Fixtures.cs 9077fd40 (the four changed in round 2) | The generator (`oritruth`). Not in FontSoftware.sln. Links `tools/Shared/ShreeWordFinal.cs` and `tools/Shared/ShreeNonIndic.cs` (main's 20:07, unedited). |
| `tests/FontSoftware.Tests/ShreeOdiaTruthTests.cs` | f7993923543df77f793292c6f832c9cf | `ShreeOdiaTruthTests` + `ShreeOdiaTypingTests` (`[Collection("typing engine")]`, the real TypingEngine; uses `RecordingInjector` from TypingEngineTests.cs). **143 tests.** |
| `tests/FontSoftware.Tests/Data/shree-ori7-truth.json` | f069febb3f001df3596252bdd674679e | `oritruth fixture` (41,470 rows; round 2 only changed the byteMap text: 9C FF reads as ନ). |
| `tests/FontSoftware.Tests/Data/shree-ori-cmaps.json` | ad98a623c2287ced433fab6a7b45ae87 | `oritruth cmaps` (12 faces), unchanged. |

## 2. Files only Odia changes (take mine, or re-run the generator / scripts)

- **`assets/config/mappings/unicode-to-shree-ori7.json`** (941 pairs, md5 14d9f97de767593757f7050cc48ed989) and **`shree-ori7-to-unicode.json`** (258 pairs, md5 448aa0412c0163a35813af595e783be1): written only by `oritruth build`. Round 2: the lone waiting-mark pair `U+FDEF -> ""` is now `"unmapped": true` (reported); the reverse table has a new pre rule `9C FF -> 5B FF` (ନ as the composer and the earlier table wrote it).
- **Layouts** (`assets/config/layouts/`): `odia-inscript.json` c4fd215f, `ori-crtronic.json` ef84a1ff, `ori-doe.json` 07387160, `ori-eng.json` a5c0fcb9, `ori-newroma.json` 9eadfcd2, `ori-phonetic.json` ce53bebb, `ori-roma.json` 872b2f1a, `ori-typewrit.json` 8796313d (`ori-modular.json` unchanged, a971c13a). Apply, in this order, on main's files:
  1. `node round1\patch-layouts.js <layoutsDir> "C:\Shree-Lipi nxt"` - round 1, 88 changes (log `round1\layout-fixes.txt`).
  2. `node round2\patch-round2.js <assets\config dir>` - round 2 (also edits ori-eng.json and the translit file): P1-P6 on ori-phonetic.json, E1-E2 on ori-eng.json, T1-T4 on odia-shree-eng.json; rules in the script's header; log `round2\patch-round2-from-before.txt` (226 changes on top of round 1). A second run makes 0 changes.
  - Both need `C:\Shree-Lipi nxt` (.ORI files, round 1 only) and the capture `r3\cap-ori\ori-vendor-keys.json` (env `ORI_VENDOR_KEYS` to move it).
  - **Rehearsed:** main's layouts + translit through round1 then round2 scripts = my 9 layouts and the translit file, byte for byte (`round2\rehearsal`).
- **`assets/config/translit/odia-shree-eng.json`** (md5 a721967aabcf180881fb7984b011c72b): `node round1\patch-eng.js <file>` then `round2\patch-round2.js` (above). Keeps CRLF, no final newline.

## 3. Shared files: the exact edits to re-apply on main's current version

- **`src/FontSoftware.Engine/TypingEngine.cs`** - final md5 2e1257bbabb17c5212366e5e9faa938f = main 20:13 (4e6a8a55e31c3f97a52d50e69e208ffe) + `r3\tbl-pun\round2\TypingEngine.diff` (-> e69af621..., saved as `round2\eng-base\TypingEngine.main2013+pun.cs`) + **`round2\diffs\TypingEngine-ori.diff`** (4 hunks, 62 lines). Checked with `patch`: main + both diffs = my file byte for byte (`round2\diffs\try`). The Odia hunks:
  1. `ContinuePhoneticEscape`: Ctrl+Space then a key its layer leaves empty, on the Eng keyboard, takes up the word typed by sound before the escape again (nothing on screen changes; the escape and the key stay one Backspace each). It used to leave the word finished: l, Ctrl+Space <, @ wrote E0 FF 7B F2 for ଲୌ (converter 7B E0 FF F2).
  2. `RerenderLegacy`: the engine's own waiting mark is never reported in the log (`WaitingMarkText`), now that the Odia table reports a lone U+FDEF.
  3. `HandlePhonetic`: a key the Eng keyboard leaves empty (`""`) is swallowed before `FinishLegacyWord` and the word typed by sound goes on (k [ @ = କୌ, was କ then ୌ alone).
- **`src/FontSoftware.Core/Keyboards/TypingOrder.cs`** - final md5 fbbb99ab67f484536d6ad611405d9adb = main (ce0bf9b46a2382babef9b73b37015f48) + **`round2\diffs\TypingOrder-ori.diff`** (7 hunks): `LearnAfterVirama` / `JoinAfterVirama` / `AfterVirama`. On a keyboard whose consonant keys type the virama form, the runs on ka teach what a vowel key does after a virama (ke = କେ, ka = କ, kee = କୀ, kai = କୈ); Compose applies it to every consonant and cluster. Learned only when a run joins an *independent vowel* this way (a phonetic keyboard), so typewriter half letters are untouched.
  - **This touches every script's Phonetic keyboard, not only Odia** (`round2\ordercheck`: 12 of 328 layouts learn joins - dev-phonetic hi/mr, san, ban, as, mni-ban, guj, tel, kan, mal, tam, ori). For them a consonant key then a vowel key now types the sign, where it typed C + virama + the independent vowel. Measured on the real engine (`round2\phoncheck` on this copy, `round2\phoncheck-main` on main's snapshot): guj-phonetic d e દ્એ -> દે, d i દ્ઇ -> દિ, d u દ્ુ (malformed) -> દુ; dev-phonetic d e द्ए -> दे; tel-phonetic d e ద్ఎ -> దె; ori-phonetic d e ଦ୍ଏ -> ଦେ, d a i ଦଇ -> ଦୈ. Their layout data is not changed, so on the other scripts' keyboards a vowel after "C a" (Odia's "dai") still follows their own chart runs. The whole suite passes (1112/1112).
- **`assets/config/encoding-limits.json`:** take main's, then `oritruth build` rewrites the `"shree-ori7"` key only (value unchanged from round 1: `0023 0024 0026 002B 003B 003C 003D 003E 0040 005B 005C 005D 005E 005F 0060 007B 007C 007D 007E 00B0 00D7 00F7 0B35 0B3C 0B3D 0B44 0B55 0B61 0B62 0B63 0B70 0B72 0B73 0B74 0B75 0B76 0B77 20B9`; main's was `0B35 0B3C 200C 20B9`). md5 after: 118c5659a9486f86a207b14b972e324a.
- **`tests/FontSoftware.Tests/ShreeNonIndicTests.cs`** (md5 14cea6fc...): `round2\diffs\ShreeNonIndicTests.diff` on main's (= snap-base's, a9af8a23...): the `shree-ori7` row in `Tables`, U+008F/U+0090 in `FontCodes`, କ as the letter, two punctuation rows.
- **`tests/FontSoftware.Tests/ShreeFontCmapTests.cs`** (md5 59bc4fd4...): `round2\diffs\ShreeFontCmapTests.diff` on main's (ba0ecf52...): two `Families` rows (`^SHREE-ORI7-`, `^SHREE-ORI-`). If the Punjabi copy's `PassedThrough` edit (`&& c != '·'`) lands too, both apply cleanly (different lines).
- **Not changed (keep main's):** `tools/Shared/ShreeNonIndic.cs` (20:07, 620ecdc5...), `tools/ConfigCheck/Program.cs`, `assets/config/languages.json` (SHREE-ORI7-* and SHREE-ORI-* already on shree-ori7), `tools/RegisterFonts/Program.cs`, the test csproj, `FontConverter.cs`.

## 4. Commands to re-run after the merge (repo root, one build at a time)

```
dotnet build tools/OriTruth/OriTruth.csproj -c Release -nodeReuse:false -p:UseSharedCompilation=false
tools/OriTruth/bin/Release/net8.0/oritruth.exe fixture   # Data/shree-ori7-truth.json (env ORI7_GROUNDTRUTH or r3\cap-ori\ori7-groundtruth.json)
tools/OriTruth/bin/Release/net8.0/oritruth.exe cmaps     # Data/shree-ori-cmaps.json (needs the installed faces)
tools/OriTruth/bin/Release/net8.0/oritruth.exe build     # both tables + the shree-ori7 limits key, every check (log it)
tools/OriTruth/bin/Release/net8.0/oritruth.exe verify    # regenerate into temp, compare byte for byte (exit 0 = identical)
node <round1>\patch-layouts.js assets/config/layouts "C:\Shree-Lipi nxt"   # 0 changes when already applied
node <round1>\patch-eng.js assets/config/translit/odia-shree-eng.json      # 0 changes when already applied
node <round2>\patch-round2.js assets/config                                # 0 changes when already applied
dotnet test tests/FontSoftware.Tests -c Release --filter "FullyQualifiedName~Shree|FullyQualifiedName~TypingEngine|FullyQualifiedName~Transliteration|FullyQualifiedName~GujaratiAudit"
```

**Important:** after copying files with robocopy, delete the copied projects' `bin`/`obj` (or build with `--no-incremental`): robocopy keeps the source timestamps, older than old build outputs, and MSBuild then skips the changed sources (it happened here: OriTruth first built against the old Core).

Expected from `build` on this copy (`round2\build1.txt`): truth 41,470/41,470 preferred; words 20,000/20,000 on seeds 7, 11, 2026; word-final 20,000/20,000 on seeds 7, 11; leaks 0; 214 code points written, 12 faces, 0 missing; 0 written that Word or RichEdit draw as nothing; reverse 41,467 read, 0 wrong; capture 54,914 read, 0 Latin/control; **raw composer spellings holding 9C: 1,445, all read as their 5B spelling reads; forward strings written with 9C FF: 0; U+FDEF alone: written '', reported**; non-Indic 62,046, 0 wrong; ASCII 0 wrong. `verify`: 3 files byte-identical (`round2\verify1.txt`).

## 5. Checks run on this copy

- `oritruth verify`: unicode-to-shree-ori7.json, shree-ori7-to-unicode.json, encoding-limits.json byte-identical.
- Tests: ShreeOdia 143/143; filter `Shree` 735/735; filter `Shree|TypingEngine|Transliteration|GujaratiAudit` 963/963; **whole suite 1112/1112** (`round2\fullsuite.txt`).
- ConfigCheck (main's): 0 errors, 123 warnings - the same 123 lines as main alone (`round2\configcheck-copy.txt`, `round2\configcheck-mainbase.txt`).
- The skeptic's own tool (`check1\skeptic-main`, copied to `round2\skeptic` with RepoDir = this copy) and audit harness (`round2\audit`): outputs in `round2\out`.

## 6. Decisions a reviewer should know

1. **ନ is written 5B FF; 9C FF reads as ନ.** All 12 faces draw ଙ୍କ୍ତ at 9C and ନ at 5B (round 1). The composer writes 9C only before FF (1,810 of 1,810 times), the tables write ଙ୍କ୍ତ as a bare 9C and never 9C FF, so the reverse pre rule `9C FF -> 5B FF` is unambiguous; a bare 9C still reads ଙ୍କ୍ତ.
2. **୍ୱ stays B4 and reads back as ୍ବ (left, with evidence).** FD (Shree's E1 glyph) is the full subjoined-ba shape, not the stroke Nirmala UI draws for ୍ୱ (`check1\js2\glyphs-check.png`); no other code of the fonts draws the stroke, so ୍ୱ and ୍ବ share B4 and the reverse reads it as ୍ବ, the composer's reading. The text looks the same; the code points differ.
3. **Phonetic keyboard data (`patch-round2.js` P1-P6):** a = ଅ and h = ହ୍ as S8OriU64 answers those keys; 149 "<consonant keys>a" runs removed and 14 virama-form runs added (dh = ଧ୍ ...), because the vowel join now makes "a" the inherent a on every consonant (a run that typed the bare letter cut the word off from the vowel after it: "dai" was ଦଇ); "aa" is a run (ଆ) so a then Shift+A is ନଅ; runs through Ctrl+Space are kept (Ctrl may be held through them) and get their virama form and aa/ai/au runs (49 added).

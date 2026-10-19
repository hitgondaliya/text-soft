---
name: glyphjoin-shape-join
description: Breakthrough path for legacy Shree encodings — join legacy font to its Unicode counterpart of the same design number by rendered glyph shape.
metadata: 
  node_type: memory
  type: project
  originSessionId: 69d8b2a2-ed7d-447f-9ed1-ba585383f6f5
  modified: 2026-09-11T18:29:30.323Z
---

2026-09-11. Way past the legacy wall in [[converter-encoding-findings]] / [[client-fonts-2026-09]] WITHOUT touching Shree's encrypted `.enc`.

**Idea:** Shree ships every typeface twice — a legacy TTF (`S0750810.TTF` = SHREE-GUJX-0750, ASCII+PUA bytes) and a Unicode OTF of the SAME design number (`SHREE0750`). Render every glyph of both to a 32x32 bitmap and match by shape: legacy byte -> GID -> (same shape) -> Unicode GID -> code point. Ground truth, derived only from fonts the user legitimately installed.

**Validated 2026-09-11:** `tools/GlyphJoin` (probe/map/xmap/batch). Self-test on two legacy fonts of the same design — S0750810 (GUJX, 583 inked glyphs) vs S0750892 (GUJ7, 214) — matched 208/214 of the smaller font, 1 ambiguous. Shape matching inside one design is reliable.

**State of the machine:** client reactivated the Shree-Lipi NXT Plus demo and installed ~804 Shree fonts (registry HKLM Fonts). `glyphjoin batch` sees **757 legacy Shree fonts in 106 distinct byte layouts** — so 106 tables, not 750, cover everything.

**The one missing input:** no Shree UNICODE font is installed. `D:\font software\FONTS\Unicode\*._TF` (372 files, 12 scripts) are ENCRYPTED (random first bytes, not sfnt); so is `FONTS\ShreeLipi-Ex` (5428 files). Only `FONTS\Default` (84) is plain TTF. Shree Font Manager (`C:\Shree-Lipi nxt Plus\FontManager.exe`, GUI only, no CLI) decrypts on install — that is how the 804 legacy ones got in. **Action: user installs the Unicode font set through Font Manager, then `dotnet run --project tools/GlyphJoin -- batch --out-dir <dir>` solves the layouts automatically.**

Design-number overlap between the Unicode set and already-installed legacy fonts is only 7 (0501 mal, 0560 ass/ban/man, 0601 ori, 0750 guj, 0800 tam, 0850 kan, 0951 pun) — enough to crack those encodings, since one solved design covers its whole layout-hash group. Devanagari/Telugu need the matching Ex legacy designs installed too (`FONTS\ShreeLipi-Ex\DEVANAGARI` has all 36 Unicode dev design numbers).

**Still to build after the join:** PUA conjunct glyphs have no single code point — recover their Unicode SEQUENCE by reading the Unicode font's GSUB ligature lookups (base GIDs -> ligature GID), then invert.

**Also usable:** `xmap` joins two legacy fonts of one design, so cracking one series (X/7/0-series) propagates to the others.

**CRACKED 2026-09-12.** Font Manager is automatable through UI Automation — it opens as a tile INSIDE `SLNXTPLUS.exe` (not as its own window), and its **"Copy Fonts"** button writes DECRYPTED TTFs to `C:\SLFNXT` with no dialog. Scripts: `scratchpad/uia.ps1` (tree/click/combo/tile/checkitems) and `autofm.ps1` (loop over Script x Layout, tick all, Copy).

Copied 7 Shree Unicode fonts: `SHREE0750` guj, `SHREE0850` kan, `SHREE0501` mal, `SHREE0601` ori, `SHREE0951` pun, `SHREE0800` tam, `SHREE0900` tel. **Demo lists 0 Unicode families for DEVANAGARI, SANSKRIT, BANGLA, ASSAMESE, MANIPURI** — that is the remaining gap.

`glyphjoin map` results (legacy -> Unicode, with `Gsub.cs` expanding conjuncts/half-forms out of the Unicode font's GSUB ligature lookups): 8-bit 7-series/0-series fonts map 85-95% of their codes; PUA X-series 55-60% (legacy carries extra positional variants). X-series layout is regular: a per-script PUA base (guj E304, pun E480, ori E804, kan EB04) then the Unicode block in order.

`glyphjoin proof` renders Unicode text vs the same text pushed through the derived table into the legacy font — Gujarati line renders correctly. Only defect left is **i-matra pre-base reorder** (ક્ષત્રિય came out ક્ષત્રયિ); our converter already has that as a pre/post rule, just needs wiring.

**Devanagari path (open, 2026-09-12).** Font Manager lists the "Unicode" layout for DEVANAGARI but **0 families** — the demo licence grants no Devanagari/Sanskrit/Bangla/Assamese/Manipuri Unicode font, so shape-join cannot run there. Tried and ruled out so far:
- `TransNXT.exe` standalone refuses: "Launch me from the source application". Word 16 IS installed but has **no Shree COM add-in / template** (`Word.Application.COMAddIns` and `.AddIns` are both empty), so there is nothing to launch it from yet.
- Typed-capture through the Shree IME: keys now reach Word (real `SetForegroundWindow` + a mouse click into the page, then `SendKeys`), but Shree does **not** transform them — `kKiI` stays `kKiI`. The Shree keyboard is not switched on for Word; `ManualAppDetect.exe` is probably the missing step.
- Inferring the layout from the PUA ordering rule: verified against solved scripts and it is NOT safe — Gujarati agrees 83/83, but Kannada and Oriya drift (Shree skips some code points) and Punjabi starts at the consonants instead of the vowel signs. Use only as a gap-filler between two shape-matched neighbours (`glyphjoin guess`, `Emit.FillOrderedGaps`).

Kannada-type scripts also still miss their **subjoined (vattu) forms**: those legacy glyphs get no shape match, and the Unicode font's `blwf` lookup yields no virama-initial sequence even after ordering form-defining features first.

Word COM note: `ActiveDocument` returns null while a modal dialog is up, and `SaveAs2` can raise one — avoid SaveAs, and close a stuck dialog by clicking its TitleBar `Close`.

**KEYBOARD ORACLE FOUND 2026-09-12 — this supersedes the font-twin limit.** Shree's IME activation lives in SLNXTPLUS Settings (the `SettingsButton`, which opens a `Settings` child window): `cmb_ActivationKey` = **Scroll Lock** here, plus `chkALwaysActive` (already On). With Scroll Lock ON and Word focused, keystrokes are converted live: typing `kKiI` into Word with Script=DEVANAGARI / Layout=ShreeLipi-Ex / Keyboard=INSCRIPT produced `U+E017 E018 E019 E01A` — real Devanagari X-series codes.

So the general oracle is: type the SAME key sequences twice, once under a legacy Layout and once under Layout=Unicode, and join on the keystroke. No font twin, no decryption, works for every script AND every third-party layout Shree lists (Kruti Dev, Bhasha Bharati, ISFOC, Terafont, Anu...). Scripts: `scratchpad/capture.ps1` (sets Script/Layout/Keyboard, ensures Scroll Lock, SendKeys one sequence per paragraph, reads back via Word COM) and `glyphjoin keystroke <legacy-cap> <unicode-cap> --out raw.json`.

Gotchas: SendKeys only lands after `SetForegroundWindow` on WINWORD **plus** a real mouse click into the page; Word COM calls block while the capture is typing; escape `+^%~(){}[]` for SendKeys.

**WIRED INTO THE APP 2026-09-12.** 11 verified encodings now ship in `assets/config/mappings` (replacing the old wrong shree-gujx/kanx/malx/orix/punx tables and adding shree-guj7/kan7/mal7/ori7/pun7/tam7). New tool `tools/RegisterFonts` scans `C:\Windows\Fonts`, groups fonts by cmap layout hash, and registers every installed font of a solved encoding into languages.json plus its `mappingFiles` list — **116 fonts** across Gujarati, Kannada, Malayalam, Odia, Punjabi, Tamil. 60/60 tests pass, ConfigCheck 0 errors, ConvTest 38/38.

Also fixed a pre-existing red test: Assamese and Manipuri use a unicode font whose id is `unicode`, so no table matched `vrinda` — gave those entries `"encoding": "vrinda"` (FontAsset.ConversionKey).

Render checks (`glyphjoin proof`): Gujarati pixel-identical; Punjabi and Malayalam correct except a nukta pair (ਸ਼) and one conjunct; **Odia needed `Emit.PreBaseMatras` corrected — its i-matra U+0B3F is POST-base**, unlike Devanagari/Gujarati/Bengali/Gurmukhi. Kannada still misses subjoined (vattu) forms.

Left: 692 of 805 installed Shree fonts still have no table — Devanagari/Sanskrit/Bangla/Assamese/Manipuri (no Unicode twin in the demo licence) plus the other layout families (ISFOC, Terafont, Bhasha Bharati, Anu). Devanagari base letters were already captured via the keyboard oracle (`maps/dev-ex-keystroke.json`, 65 entries) but not yet wired.

**BUG PASS 2026-09-12 (all three reported gaps fixed).** Root causes, all in `tools/GlyphJoin`:
1. **`Gsub.Virama()` returned the wrong script's virama.** It took the first Indic code point in the cmap, and every Indic font also maps danda U+0964 — a Devanagari code — so Kannada got U+094D instead of U+0CCD and no subjoined form was ever recognised. Now takes the block the font mostly covers, ignoring U+0964/0965/0970.
2. **Below-base forms were spelled backwards.** A blwf/pstf/vatu/pref LIGATURE of (consonant, virama) makes the glyph that hangs under the previous letter, so in text the virama comes FIRST. It was stored as "consonant + virama", colliding with the explicit-halant form.
3. **Greedy longest-match ate the halant.** On "ನ ್ ನ" the converter took "ನ ್" and the conjunct never formed. `Emit.AddSubjoinedCombos` now writes the whole cluster as one three-character pair (1190 for Kannada, 1002 for Odia) so the correct reading is the longest one.
4. Punjabi ਸ਼ / Malayalam ൺ: `Emit.Alternates` registers other spellings of the same text — NFD decomposition, chillu as consonant+virama, and a conjunct minus the trailing virama the half-form lookup leaves on it. Runs as a SECOND pass so it only fills gaps.
5. Gurmukhi independent vowels are written bearer + matra (ਆ = ਅ + ਾ). `Emit.WrittenAsTwo` + `ComposedLetters` supply the reverse pair and a forward postRule recomposes them.
6. **Odia i-matra U+0B3F is POST-base**, unlike Devanagari/Gujarati/Bengali/Gurmukhi — it was in `PreBaseMatras` and broke ଅଛି.

`tools/ConvTest` now discovers every encoding wired in languages.json instead of a hardcoded list, compares NFC, and **treats any unmapped character as a failure** — unmapped characters pass through untouched, so a round trip can "pass" while the legacy text is really the original letters in a legacy font. That is what exposed shree-kan7.

**shree-kan7 was pulled back out** (`registerfonts --remove shree-kan7`, 16 fonts): its table covers only 9 of Kannada's 34 consonants because the 7-series Kannada font does not shape-match the Unicode twin (tolerance and `glyphjoin chain` through the X-series both plateau ~106/216). It needs a keyboard-capture pass.

Final state: **96 legacy fonts registered** across Gujarati/Kannada/Malayalam/Odia/Punjabi/Tamil, 30 mapping tables, ConvTest **61/61 with zero unmapped**, 60/60 xUnit, ConfigCheck 0 errors, solution builds clean, app launches clean (`app-20260912.log`, no errors) — it stops at the **Activation required** dialog, which is the licensing gate, not a regression.

**In-app typing test 2026-09-12 — cannot be automated.** `TypingEngine.OnKey` returns early on `e.Injected` (so the engine never reacts to its own injected text), and SendInput/SendKeys is always flagged `LLKHF_INJECTED`. Automated keystrokes therefore pass straight through: typing `kcn` into Word landed as literal `k c n`. **Only physical typing exercises the hook** — set the app up and hand it to the user.

Also learned: **Shree-Lipi hijacks keystrokes even with Scroll Lock off**, because its Settings has `chkALwaysActive` = On. While SLNXTPLUS ran, `kcn` in Word produced `U+E017 E030 E034` — Devanagari X-series codes from Shree's own engine, nothing to do with TextSoft. Close SLNXTPLUS (or untick Always Active) before testing TextSoft's keyboard.

Dev unlock for testing without a licence key: `LicenseViewModel.DevUnlock` reads env var **TEXTSOFT_DEV_UNLOCK=1**; `IsActive = _manager.IsActive || DevUnlock`. Off unless the variable is set, and `LicenseManager` itself is untouched. Remove/ignore the variable to get the real gate back.

The app's own UIA tree exposes only 22 elements (the tab content is not in it), so drive it by screen coordinates — and call `SetProcessDPIAware()` first or the screenshot and the cursor disagree by the 1.25 display scale. Helpers: `scratchpad/click.ps1`, `scratchpad/app.ps1`, `scratchpad/wordtest.ps1`.

**FULL-MATRIX TEST 2026-09-12 — `tools/FullTest`.** Walks every language x font layout x keyboard layout, presses every key of the layout, converts through the same `FontConverter` the typing engine uses, then checks the produced code actually EXISTS in the cmap of every font offered under that encoding. A table can round-trip perfectly and still render nothing. Went from **59/436 to 404/798** combinations passing as the bugs below were fixed. Note what it does NOT prove: for the unverified devx/banx/telx/tamx tables a "pass" only means every key maps and every code exists — not that the code means the right letter.

Bugs it found, all now fixed:
- **64 FONT LAYOUT dropdown entries had an empty `fontId`** — visible in the UI, did nothing. `registerfonts --fix-layouts` wires every one we can serve (7 legacy) and leaves the genuinely unsupported ones empty.
- **Gujarati "Shree-Lipi 7.0" pointed at `shree-guj-1120`, an X-series font.** The repair compares the font's ENCODING against the series the layout names, not just whether the id resolves.
- **11 languages' Unicode fontLayout pointed at fontId `unicode`**, which is not a font in those languages.
- **Mangal, Raavi, Tunga, Kartika, Kalinga, Latha, Gautami, Vrinda are on no current Windows** — only `Nirmala.ttc` is, and it covers every one of these scripts. `registerfonts --add-nirmala` adds "Nirmala UI" to each Indic language (carrying the old id as its `encoding`, so existing tables still match) and points the Unicode layout at it. This alone took 140 -> 384.
- **RegisterFonts grouped fonts by code-point set alone, which is unsound for 8-bit fonts**: ITRFK-Guj-Brush matched the Shree 7-series set but shares only **1 of 214 glyphs**. Now also requires the `SHREE-<SCRIPT>-<design>` family naming. The one bad entry was removed.
- FullTest itself had two false positives: it skipped `.ttc` (so Nirmala read as missing), missed the per-user fonts folder (`%LOCALAPPDATA%\Microsoft\Windows\Fonts`), and counted self-mapping ASCII punctuation as unmapped.

Remaining gaps it reports, smallest first: Odia missing danda U+0964 (its X font's danda glyph did not shape-match); Gujarati nukta U+0ABC; Tamil ள in the old shree-tamx table; Devanagari/Bangla/Telugu missing their digits and a few letters — those are the **unverified** old tables, expected. 61 dropdown entries stay unsupported (Shivaji, Indica, Anu, ISFOC, Bhasha Bharati, STM, BIJOY, Akruti, Terafont, Vanavil...) — no tables for those encodings at all. Punjabi's 4/5/6 fonts are registered but unreachable: Punjabi has a "Shree-Lipi 7.0" fontLayout and no "4.0/5.0/6.0" one.

**THE GENERAL ORACLE — "Converter nxt" 2026-09-12.** Shree-Lipi's home screen has a **Converter nxt** tile. It converts text between ANY two font layouts it lists, clipboard in and clipboard out — no Unicode twin font, no keyboard hook, no Word. This is the tool that unlocks the third-party encodings (Bhasha Bharati, ISFOC, Akruti, Anu, Terafont, Shivaji, Indica, STM, BIJOY) and the scripts with no twin (Devanagari, Bangla, Telugu).

Control ids (all UIA-drivable): `cmbScript`, `cmbInputLayout`, `cmbOutputLayout`, `s_Clipboard` / `d_Clipboard`, `btnGetClipData`, `BtnConvert` (labelled "Next"), output box `rtbOPData`, and **`StartOver` — a finished conversion locks the panel, so press it before every run** or "Get Data" comes back disabled and you silently re-read the previous result.

Pipeline: `scratchpad/units.ps1` writes one text unit per line (block letters, half forms, consonant+matra, consonant+virama+consonant — 1720 lines for Gujarati) -> `scratchpad/convdrive.ps1` runs it through Converter nxt -> `glyphjoin aligned <units> <converted> --out raw.json` -> `glyphjoin emit` -> `registerfonts --add-font "<lang>|<id>|<name>|<family>|<encoding>|<fontLayout>"`. One conversion is ~15 seconds and yields a whole table.

**Proved end to end on Bhasha Bharati:** Gujarati produced 1604-1680 pairs per variant, 34/34 consonants. Shree lists three variants and the installed fonts are a different one each — rendering the same sentence through each table identified **"Bhasha Bharti-2" as the layout of `B Bharati AbmarTwo` (BAMBAR_.TTF)**: the proof render matches the Unicode line exactly. ("Bhasha Bharati" plain is a Devanagari-ordered layout; BKA/Kautilya renders Devanagari, so the earlier note in [[client-fonts-2026-09]] calling those Gujarati is wrong.)

Two gotchas for converter-captured tables:
- The keyboard route is NOT a substitute: after restarting SLNXTPLUS its hook stopped converting altogether (both Scroll Lock states, any layout), so typing captures are unreliable. Converter nxt is the one to use.
- The unit list contains whole clusters (consonant+matra), which **conflicts with the pre-base reorder rule** — the cluster pair matches the un-reordered text and puts the matra on the wrong side. `Emit` now drops "consonant + pre-base matra" pairs from the REVERSE table when the reorder rule is on. Still open on this table: the FORWARD (legacy->Unicode) direction puts the i-matra after the whole cluster in `વિદ્યા`; typing (Unicode->legacy) is correct.

**BATCH RUN 2026-09-12 — 404 -> 609/840 combinations.** Captured through Converter nxt and re-emitted: **shree-devx** (33132 pairs, 37/37 consonants, 16/16 vowels), **shree-banx** (15200), **shree-tamx** (6694), **shree-telx** (1831), **shree-kan7** (1838, so Kannada 7-series is back in), **bhasha-bharti-2-guj** (16280). The four previously UNVERIFIED tables (devx, banx, telx, tamx) are now ground truth from Shree's own engine; render proofs match for Devanagari (`कर्म` reph, `श्री`, `क्षत्रिय`, `ज्ञान`), Bangla and Telugu. Hindi 123/123, Telugu 34/34, Kannada 63/63 combinations pass.

Two things this run settled:
- **The unit list must contain every cluster the text can contain** — including consonant+virama+consonant+**matra** (33k lines for Devanagari). Then emit with `--no-reorder`: the captured pairs already carry the right order, and applying the pre-base reorder rule on top moves an already-correct matra past the NEXT cluster (`विद्यालय` came out `वद्यिालय`). Big inputs need the output-box polling in `convdrive.ps1`, not a fixed sleep.
- The three `ShreeDevxToUnicode` unit tests asserted the OLD scheme-derived ASCII codes (`"sam"` -> कमल). The X-series is a PUA layout; they now assert the real codes (`\uE017\uE030\uE034`).

Still open, all small and named: danda U+0964 unmapped in Bangla/Assamese/Manipuri/Odia (84 combinations — Shree returns blank for it, so the legacy fonts may have no danda glyph and passing it through is arguably right); Tamil `ள`; Odia `ୱ` and its danda; Punjabi nukta `਼`; and the Bhasha Bharti-2 table lacks the independent vowels આ ઍ એ ઐ ઓ ઔ and matras ો ૌ. 61 dropdown entries still have no table at all (ISFOC, Akruti, Anu, Terafont, Shivaji, Indica, STM, BIJOY) — every one of them is now a mechanical run of the same four steps.

**FULL LICENCE INSTALL 2026-09-13 — the big unlock.** Client installed the full `C:\Shree-Lipi nxt` (process `SLNXT`; the old `Shree-Lipi nxt Plus` is still there too). **2,817 Shree fonts, 189 distinct byte layouts** (demo had 805 / 106). Crucially the licence now exposes **Unicode twin fonts for every script**: Devanagari 12 (demo had 0), Bangla/Assamese/Manipuri 9 each, Gujarati/Kannada/Malayalam/Odia/Punjabi/Tamil/Telugu 10 each; Sanskrit 0 (it shares Devanagari). `tools/shree/copyfonts.ps1` dumped them — **73 decrypted Unicode twins now in `C:\SLFNXT`** (was 7).

**The insight that mattered more than any of it: an encoding is per (script x SERIES), not per design.** `SHREE-GUJX-5227` — a completely different design that the layout-hash batch called its own unsolved layout — renders correctly through the existing `shree-gujx` table (verified by render). Grouping by the exact cmap code-point set was splitting one encoding into dozens of groups and blocking thousands of fonts.

`registerfonts --by-series` now reads Shree's own taxonomy out of the family name (`SHREE-<SCRIPT><SERIES>-<design>`, series X / 7 / none) and then PROVES it: every legacy code the encoding's table can emit must exist in the candidate's cmap, else reject. **Legacy fonts registered went 96 -> 1096**, 92 rejected by that check. Render-verified on fonts never individually solved: Tamil `SHREE-TAMX-RKrishnamurthy-0801`, Telugu `SHREE-TELX-Apoorva-0902`.

Also: `--sync-mappings` keeps `mappingFiles` in step with the encodings fonts name (a missing entry makes a font look wired while the converter finds no table). Helper scripts now live in **`tools/shree/`** — the scratchpad is wiped between sessions and the earlier copies were lost.

Two routes now, and the choice is not close for Shree's own fonts:
- **shape join on files** (`glyphjoin batch`) — no GUI, whole batch at once. Needs the Unicode twin, which now exists everywhere. Note the batch picks the first twin with a matching design number and the `*_OTF_*` twins match WORSE than the same-numbered `SHREE####` ones (GUJX-0750: 133 vs 317 mapped) — it should try all candidates and keep the best.
- **Converter nxt** — still the only way for third-party layouts (Bhasha Bharati, Akruti, ISFOC, Anu, Terafont), because Font Manager only ships Shree's own fonts.

Git: repo initialised 2026-09-12, `.gitignore` excludes bin/obj/.vs/dist/node_modules. Commits `80ec46a` (initial) and `5e8d433` (by-series).

**WHY THE CLIENT'S TEST FAILED (2026-09-13).** The build handed to the client was `dist/` from **4 September** — its `unicode-to-shree-gujx.json` contains **zero** legacy codes, i.e. the old broken tables. Every fix since then existed only in source; **no new build was ever packaged**. Lesson: after a table or config change, republish `dist/` before anyone tests, and check `grep -c uE3 dist/TextSoft/config/mappings/unicode-to-shree-gujx.json` against the source copy.

Packaging: `dotnet publish src/FontSoftware.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true`, copy publish output to `dist/TextSoft`, drop `*.pdb`, `Compress-Archive` to `dist/TextSoft-portable.zip` (~73 MB, too big for the 30 MB chat upload — hand over the path). Inno Setup is NOT installed on this machine, so `installer/build-installer.ps1` cannot run here. The packaged build keeps the real licence gate; `TEXTSOFT_DEV_UNLOCK=1` is only for local testing.

**Remaining gap is much smaller than it looked.** `registerfonts --by-series --dry-run` now prints the series it has no table for. Of the fonts not registered: **ENGX 501 + ENG7 402 are English/Latin** and **SYMX 111 + SYM7 102 are Symbol** faces — none of them need an Indic mapping; 346 more are ordinary Windows fonts (Arial, Calibri) that merely failed the SHREE regex. The real remainder was ~75 Indic fonts, and this session closed most of it: **shree-ban7** (13,943 pairs, covers BAN7/ASS7/BAN/ASS, 48 fonts) and **shree-tel7** (16,901 pairs, 6 fonts). Sanskrit maps onto the Devanagari layout for only **3 of its 12** fonts — the cmap check rejects the other 9, so SANX is genuinely a different layout and still needs its own capture. DIAX (19, Diacritical) is untouched.

State: **1,154 legacy fonts registered**, 40 mapping tables, 60/60 tests, ConvTest 80/80, ConfigCheck 0 errors. Commits `5e8d433` (by-series), `f822e25` (ban7/tel7 + repackage).

**THIRD-PARTY LAYOUTS 2026-09-13 — what is actually possible.** Of the families the client named (Bhasha Bharati, Akruti, ISFOC, Anu, Terafont, plus Kruti Dev / DevLys / Shivaji), **only Bhasha Bharati is installed on this machine**: `B Bharati Kautilya / Sharmila / Aswad / AbmarTwo`, `B_Bharati02`. Checked both font folders and both registry hives. Tables for the others can still be captured from Converter nxt — it needs no font — but they **cannot be verified** without one font per family, and shipping an unverified table is exactly what burned us. **Ask the client for one font file per family.**

Bhasha Bharati is done: all three variants captured for Devanagari produce an **identical** table, so Devanagari has one BB encoding (`bhasha-bharati-dev`, 2750 pairs), verified by render into B Bharati Kautilya. Gujarati's is `bhasha-bharti-2-guj` and the variants there DO differ — AbmarTwo is Bhasha Bharti-2. So variant identity is per script; capture all three and compare before assuming.

**`--add-font` now proves before listing** (same cmap test as `--by-series`): B_Bharati02 rejected 80/80 codes absent, B Bharati Aswad 4/80 — neither is listed as working. **`--add-layout`** adds a FONT LAYOUT entry the config lacked; Hindi and Marathi had no Bhasha Bharati entry at all, so the encoding was unreachable from the UI even once its table existed.

**Dead end recorded: do not re-try shape comparison to verify a table against an arbitrary font.** `Verify.cs` did exactly that and scored a known-correct table (bhasha-bharti-2-guj against AbmarTwo) at 0%, because the legacy font and the Unicode reference are different typefaces. Removed. Cross-typeface shape matching only ever works within one design number.

Family names: the registry key ("B Bharati Kautilya Normal") is NOT the font's internal family ("B Bharati Kautilya"). Config `family` must be the internal one or the app reports the font as not installed.

State after this: ConvTest 84/84, 60/60 tests, ConfigCheck 0 errors, 42 mapping tables. Commit `d7936a8`.

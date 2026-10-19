export const meta = {
  name: 'guj-bangla-parallel',
  description: 'Gujarati 15-bug fix in its own repo copy in parallel with Bangla/Assamese finishing in main; skeptics; then a 3-way merge of Gujarati into main, all generators re-run, merge skeptic.',
  phases: [
    { title: 'Gujarati fix', detail: 'own copy of the repo, 15 audit bugs + policy, skeptic' },
    { title: 'Bangla', detail: 'continue in main, skeptic' },
    { title: 'Merge', detail: '3-way merge Gujarati into main, regenerate tables, skeptic' },
  ],
}

const REPO = 'C:\\Users\\harde\\Desktop\\Windows Font Software'
const SP = 'C:\\Users\\harde\\AppData\\Local\\Temp\\claude\\C--Users-harde-Desktop-Windows-Font-Software\\7673923b-96b4-4287-87dd-a8e7c6533eca\\scratchpad'
const R3 = SP + '\\r3'
const SNAP = SP + '\\snap-base'
const PREV = 'C:\\Users\\harde\\AppData\\Local\\Temp\\claude\\C--Users-harde-Desktop-Windows-Font-Software\\624671f5-ee6d-402b-b810-05b52f236677'
const MEM = 'C:\\Users\\harde\\.claude\\projects\\C--Users-harde-Desktop-Windows-Font-Software\\memory'
const WT = REPO + '\\.claude\\worktrees\\wf_607a9ebd-c6c-2'
const WFDIR = 'C:\\Users\\harde\\.claude\\projects\\C--Users-harde-Desktop-Windows-Font-Software\\7673923b-96b4-4287-87dd-a8e7c6533eca\\subagents\\workflows\\wf_d2d50cfe-4e5'
const GUJCOPY = R3 + '\\guj-repo'

const COMMON = `
PROJECT: TextSoft, a .NET 8 WPF app that types Indian languages into any Windows program through keyboard layouts, and converts text/files between Unicode and legacy 8-bit fonts. Repo: ${REPO} (git branch master). The working tree holds a week of UNCOMMITTED, verified work: never run git stash, checkout, reset, restore, clean or commit, and never delete files you did not create.
How legacy output works: assets/config/mappings/unicode-to-shree-<enc>.json (forward) and shree-<enc>-to-unicode.json (reverse) are MappingTables (pairs + preRules/postRules, longest match; src/FontSoftware.Core/Conversion/FontConverter.cs, src/FontSoftware.Core/Models/MappingTable.cs). assets/config/languages.json wires each font to an encoding; each language has font layouts 'Shree-Lipi 7.0' (SHREE-<SCR>7-* faces) and 'Shree-Lipi 4.0/5.0/6.0' (SHREE-<SCR>-* faces). The typing engine is src/FontSoftware.Engine/TypingEngine.cs (layouts in assets/config/layouts/*.json, keyboard order rules in src/FontSoftware.Core/Keyboards/TypingOrder.cs).
GROUND TRUTH is Shree-Lipi's own composer output (S8<Scr><Fam>64.dll; U = Unicode, 7 = Shree-Lipi 7 raw bytes), already captured and saved. Before starting read these memory notes in ${MEM}: shree-tutor-api.md, shree-7-vs-456-split.md, shree-keyboard-scheme-files.md, shree-escape-layers.md, shree-keyboards.md, keyboard-typing-order.md, shree-import-legacy-codes.md, legacy-byte-0xad-trap.md, live-testing-rules.md; and docs/CONVERTER.md and docs/KEYBOARDS.md.
THE CLIENT says TextSoft's Shree-Lipi 7 and 4/5/6 Gujarati output has 'many bugs' and has been angry that 'tests pass' while the real product fails. Green unit tests are NOT evidence: measure the real FontConverter and TypingEngine against Shree's own output, byte for byte, and say exactly what you measured.
PC LIMITS: 7.5 GB RAM and little of it free; up to three other agents work beside you. Run at most ONE dotnet build or test process at a time; filtered tests while iterating, the whole suite at most once, at the end. Builds may be slow (paging): if something dies out of memory, wait a minute and retry. Kill only dotnet/testhost/VBCSCompiler/MSBuild/your own exe processes that YOU started; never kill TextSoft.exe, SLNXT.exe, WINWORD.EXE you did not start, or any other user program.
COMPOSER: do NOT load Shree's composer DLLs (another agent owns them). Use the saved captures.
NO UI: never type into, click, or focus the user's windows; no TypeProbe, SendInput, clipboard or keyboard-hook tests (the live test comes later). Word automation only for a visibility check: a NEW hidden instance via COM, read-only documents you made, Quit only your instance.
Write scratch files under your own directory. Be exact: hex bytes, counts, file paths. Your final answer is the structured output.`

const POLICY = `
GLOBAL OUTPUT POLICY for every Shree-Lipi 7 and 4/5/6 table. Apply it through the shared generator code (tools/Shared/ShreeNonIndic.cs and the per-script generators), never by hand-editing generated tables:
P1. Indic text: write exactly what Shree's composer writes (the truth), syllable for syllable; a word-final virama is the visible halant (tools/Shared/ShreeWordFinal.cs).
P2. A character the target fonts draw as itself at its own code (or the script's own digit at an ASCII digit code, as Shree does) is written as that code and NOT reported as unmapped (e.g. , . ( ) ? ! - and digits).
P3. A character whose own code the fonts draw as something else (e.g. ASCII '[' drawing an i-sign) is never written as that code: write the fonts' look-alike if they have one, otherwise leave it out and report it (MappingPair.Unmapped). This covers ASCII as well as non-ASCII.
P4. Word must show what we write. If a code point we write is one Word or RichEdit draws as nothing (see the Word-visibility map), write instead: another code point of the same fonts that reaches the same glyph; else an equivalent spelling from glyphs Word does draw that reads as the same text; else, for punctuation, the ASCII look-alike the fonts draw; else leave it out and report it. Never drop anything silently.
P5. Reading legacy text back accepts every spelling we or Shree ever wrote (including the old C1 code points in existing documents).
P6. Every output path carries the same code points: typing, RTF/clipboard, .docx, and .txt (a .txt must write U+0080-U+00FF as their raw bytes, never '?').
WORD-VISIBILITY MAP (finished this morning): ${R3}\\result-word-map.json (summary) and ${R3}\\word\\ (summary.json, fam\\<family>.json per font family, final-invisible.txt, final-ascii.txt, final-alternatives.txt). Read it before deciding any P2-P4 question.`

const OWNERSHIP = `
OWNERSHIP while two agents work at once (the Gujarati agent in its own copy ${GUJCOPY}, the Bangla agent in the main checkout); the Gujarati copy is 3-way merged into main afterwards with base ${SNAP} (the main checkout as it was at 08:30 this morning):
- Gujarati agent owns: the general P2-P4 code in tools/Shared/ShreeNonIndic.cs as Guj7Truth and ShreeDevTruth use it; tools/Guj7Truth and tools/ShreeDevTruth; the engine (src/FontSoftware.Engine), layouts, TypingOrder, ConverterViewModel, MainViewModel, FontLayout; FileConversion's .docx/.rtf run and font handling.
- Bangla agent owns: tools/BanTruth, the Bangla/Assamese tables, their languages.json/encoding-limits.json/RegisterFonts entries, and the P6 raw-byte .txt writer in FileConversion.WriteTxt (for every script).
- Both: keep edits to shared files minimal, additive and local (no reformatting, no moving code), so the merge is easy.`

const FIX_SCHEMA = {
  type: 'object',
  properties: {
    files: { type: 'array', items: { type: 'string' } },
    perItem: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          status: { type: 'string', enum: ['fixed', 'partly', 'not-fixed', 'not-a-bug'] },
          detail: { type: 'string', description: 'before -> after in hex, tests added' },
        },
        required: ['id', 'status', 'detail'],
      },
    },
    numbers: { type: 'string', description: 'truth, words, reverse, typing, suite, ConfigCheck results' },
    testsPass: { type: 'boolean' },
    notes: { type: 'string' },
  },
  required: ['files', 'perItem', 'numbers', 'testsPass'],
}

const CHECK_SCHEMA = {
  type: 'object',
  properties: {
    numbers: { type: 'string' },
    defects: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          severity: { type: 'string', enum: ['HIGH', 'MEDIUM', 'LOW', 'INFO'] },
          text: { type: 'string' },
        },
        required: ['severity', 'text'],
      },
    },
    verdict: { type: 'string' },
    clean: { type: 'boolean', description: 'true only if no HIGH or MEDIUM defect remains' },
  },
  required: ['numbers', 'defects', 'verdict', 'clean'],
}

// ---------------------------------------------------------------- Gujarati

const gujFixPrompt = (round, defects, prev) => `${COMMON}
${POLICY}
${OWNERSHIP}
YOU ARE THE GUJARATI AGENT. YOUR REPO COPY: ${GUJCOPY}. If it does not exist, create it: robocopy ${SNAP} -> ${GUJCOPY} (src, tests, tools, assets, FontSoftware.sln; exclude bin and obj). Edit and build ONLY in your copy; never touch the main checkout (the Bangla agent is changing it now). Keep ${GUJCOPY}\\MERGE.md up to date: every file you added or changed, one line why, and the generator commands to re-run after the merge. Scratch: ${R3}\\guj-fix${round}.
THE AUDIT: ${R3}\\result-guj-audit.json (read ALL of it: 15 bugs with repro, hex, cause, fix). Its saved composer answers, harness and results are in ${R3}\\guj-audit (composer\\, harness\\, results\\, analysis\\); the harness builds against ${R3}\\guj-audit\\repo, so point it at your copy to re-measure.
${round === 1 ? `TASK (round 1): fix all 15 audit bugs at the narrowest responsible layer, HIGH first, plus P1-P6 for the guj7/guj456 tables and the P2-P6 items for dev7/dev456/san7 (they write × ÷ – — ऄ ऎ ऒ ॆ ॊ at C1 code points U+0081 U+008F U+008D U+0090; fix only the policy items there, not their syllables). Tables only through their generators (tools/Guj7Truth, tools/ShreeDevTruth). Guidance per bug:
- BUG-01 Backspace in chart runs: engine-wide (every script, Unicode and legacy profiles): Backspace takes back exactly the last keystroke, also inside a run (one piece per keystroke, or a stack of run states). Test on several layouts of several scripts.
- BUG-02 રુ/રૂ: write the ligatures Shree's composer writes (7B 24 for રુ; EA 24 old રૂ; 7E 24 new રૂ = "રૂ"+U+FE00 as in shree-gujx; their anusvara/reph/visarga variants exactly as captured) in both tables, read them back, and make every ru/ruu key type them.
- BUG-03: a .docx/.rtf conversion keeps the original font on runs that do not hold the source script; only runs with the script's text get the legacy font. Test with a mixed document.
- BUG-04 Eng keyboard: make guj-eng type what ENG.GUJ types (saved composer answers per key; chart for multi-key behaviour).
- BUG-05, 06, 10, 11, 12: P2-P5. ₹: none of the 18 installed GUJ7/GUJ fonts draws 0x3C, so decide with the Word map and the fonts (look-alike or leave out + report); ॥ = two dandas as Shree types it (97 26 26).
- BUG-07: split the 78 chart 'A or B' entries into their alternatives (fix the chart import or the layout data, whichever made them; check all scripts' layouts for the same artefact and fix them too).
- BUG-08 Ekl: a run may start with a key that types nothing alone.
- BUG-09: code EE (byte 90, short dash) on the 15 keys; read 90 back as the character it draws (choose with the glyph and the Word map).
- BUG-13: while a half form, link or reph waits for its letter, show the glyph Shree shows (E5, AE...); a visible halant only if the word ends there.
- BUG-14: the converter's output font list only offers the output layout's family; an old saved setting is migrated visibly.
- BUG-15: empty vendor slots type nothing (as Shree), InScript z types what the vendor file says.
Every fix gets a test that fails before and passes after, built from the audit's repro and the composer's saved answers (never from our own output). For a bug you conclude is not a bug, give the evidence.` : `TASK (round ${round}): the skeptic found these defects in round ${round - 1}. Fix every HIGH and MEDIUM one and the cheap safe LOW ones; for each you leave, give the evidence.
DEFECTS: ${JSON.stringify(defects)}
PREVIOUS FIX REPORT: ${JSON.stringify(prev)}`}
Measure at the end, in your copy: guj7truth build (tables regenerate byte-identical), truth 17,335/17,335 for guj7 and guj456, 20,000 words on two seeds, reverse, the audit's L1 per-key and L2 typing harness against the saved composer answers (before/after mismatch counts per lens), shreedevtruth dev7/san7 verify, ShreeNonIndicTests, ShreeFontCmapTests, the whole suite once, ConfigCheck. Report before -> after in hex for every item.`

const gujCheckPrompt = (round, fix) => `${COMMON}
${POLICY}
YOU ARE THE INDEPENDENT GUJARATI SKEPTIC. Do not edit ${GUJCOPY} or the main checkout: copy ${GUJCOPY} (src, tests, tools, assets, FontSoftware.sln; no bin/obj) to ${R3}\\guj-check${round}\\repo and build there, one build at a time.
THE AUDIT: ${R3}\\result-guj-audit.json and ${R3}\\guj-audit.
THE FIX AGENT REPORTS: ${JSON.stringify(fix)}
Re-measure everything yourself: regenerate guj7/guj456 and dev7/dev456/san7 from their generators into a scratch folder and compare byte for byte with the shipped ones; truth (17,335 Gujarati, 27,175 Devanagari, 25,468 Sanskrit rows) with the real FontConverter; 20,000 words on two NEW seeds; reverse; re-run the audit's L1 per-key and L2 typing harnesses against the saved composer answers and report the remaining mismatch counts per lens; check each of the 15 audit bugs with its exact repro (Backspace on several scripts' layouts too); every code point written is in every installed face's cmap and is drawn by Word per the Word map (spot-check the new choices in Word yourself: new hidden instance, read-only docx you made, PDF export; quit only yours); the .docx/.rtf mixed-run conversion; the whole suite once; ConfigCheck. Look hard for regressions and for fixes that only make a test pass. Report defects with severity; clean = no HIGH or MEDIUM left.`

// ---------------------------------------------------------------- Bangla

const banglaFixPrompt = (round, defects, prev) => `${COMMON}
${POLICY}
${OWNERSHIP}
YOU ARE THE BANGLA AGENT. You edit and build the MAIN checkout ${REPO}; you are the only agent editing or building there during your run. Scratch: ${R3}\\bangla-fix${round}b.
${round === 1 ? `TASK (round 1, CONTINUED): bring the Bangla/Assamese Shree-Lipi 7 and 4/5/6 work into the main checkout and finish it. A previous agent with this same task was stopped at 10:52 (the plan changed, nothing was wrong); it had worked in main for 30 minutes. Its transcript: ${WFDIR}\\agent-a81ae96de3d3efa61.jsonl (read its last part: what it had done and decided). Compare main with the morning snapshot ${SNAP} to see exactly what it changed (31 files: tools/BanTruth/*, the ten tables, Data/shree-ban7-truth.json and shree-ban-cmaps.json, languages.json, encoding-limits.json, tools/RegisterFonts/Program.cs, tools/Shared/ShreeNonIndic.cs, src/FontSoftware.App/Services/FileConversion.cs). A file may be half-written; verify each and continue from there.
SOURCE of the Bangla work: the worktree ${WT} (based on commit b96047d, WITHOUT main's uncommitted work). Its round-1 report and the skeptic's report: ${PREV}\\tasks\\wps8wzzb3.output (read it all). A round-2 agent later worked in the worktree and died without reporting (transcript under ${PREV}\\subagents\\workflows\\wf_7ad28609-595\\). Merge file by file (git -C <worktree> diff b96047d -- <file>); never overwrite main's own changes. Make BanTruth build against main's Core and follow main's shared policy.
THEN FIX every open defect from the last skeptic:
 - HIGH: ৱ written as U+009E (ban7, ban456): Word and RichEdit draw nothing; FileConversion.WriteTxt (Encoding 1252) turns U+009E into '?' silently. Apply P4, and P6 (the raw-byte .txt writer for every script; add a test).
 - MEDIUM: ব্ঢ (U+0081), ব্ড (U+008D, Bangla families), চ্ঞ (U+0090) written as C1 code points Word draws as nothing: apply P4.
 - MEDIUM regression: the composer's own ra + ra-phala bytes read back with Latin ø (41 F4 A1 B9 F8 = ক্ৰ্র reads ক্রø; B9 F8 reads রø; B9 E7 A1 F8 reads রুø; 62 composer spellings per family) because the 127 rra-no-ra-twin rows were dropped; fix the reverse and test it on the composer's spellings, not only the fixture.
 - LOW: those 127 strings are written without a test: ক্ৰ্র / ক্র্র become 41 F6 F6 A1 (two ra-phala glyphs on one spot); write what the composer writes (41 F4 A1 B9 F8) or another spelling that draws both.
 - LOW: control codes 09-0D stay whitespace (keep that).
 - LOW: pass-through characters draw letters (/ = ; + * [ and ASCII " ' draw ব-base, থ, ৎ, ঔ, ও, i-sign, অ, ঐ): apply P2/P3 (ASCII quotes may map to the curly-quote glyphs 91/92 if that is the look-alike).` : `TASK (round ${round}): the skeptic found these defects in round ${round - 1}. Fix every HIGH and MEDIUM one, and the LOW ones that are cheap and safe; for each one you decide not to fix, say why with evidence.
DEFECTS: ${JSON.stringify(defects)}
PREVIOUS FIX REPORT: ${JSON.stringify(prev)}
RESUME: earlier attempts of this round were stopped twice (12:19 by a usage limit, about 15:20 by a plan change; nothing was wrong), so main may hold their partial edits. The first attempt was stopped after 10 minutes. It had edited tools/BanTruth/Reader.cs, Common.cs and Ascii.cs in main; its last edit to Ascii.cs was a node string-replace whose heredoc failed, so Ascii.cs may be half-edited. Its transcript: ${WFDIR.replace('wf_d2d50cfe-4e5', 'wf_67bf1827-89f')}\\agent-a8fe753ff17456ee1.jsonl. Check those three files first (do they compile, what did they intend), then continue.`}
Rebuild the tables with the BanTruth generator (the only writer of the ten tables and the Bangla encoding-limits keys); measure: truth per family, 20,000 words on two seeds, reverse on the fixture AND on every composer spelling in ${PREV}\\scratchpad\\shree7\\ban7-groundtruth.json, word-final hasanta, the BMP sweep, cmaps against every installed face, Word visibility of every code point written. Bangla tests while iterating, then the WHOLE suite once and tools/ConfigCheck. Do not change the Gujarati, Devanagari or Sanskrit tables. Report before -> after in hex for every item.`

const banglaCheckPrompt = (round, fix) => `${COMMON}
${POLICY}
YOU ARE THE INDEPENDENT BANGLA SKEPTIC. Do not edit the main checkout: copy src, tests, tools, assets and FontSoftware.sln (no bin/obj) to ${R3}\\bangla-check${round}b\\repo and build there, one build at a time.
THE FIX AGENT REPORTS: ${JSON.stringify(fix)}
Re-measure everything yourself with the real FontConverter, and the TypingEngine for a Bangla and an Assamese layout in a 7 and a 4/5/6 profile: regenerate the ten tables from tools/BanTruth into a scratch folder and compare byte for byte; truth per family (the 25,741 composer rows in ${PREV}\\scratchpad\\shree7\\ban7-groundtruth.json: every row composer-preferred, composer-accepted or a justified decision); 20,000 words on two NEW seeds; reverse on every composer spelling (no Latin or control characters may come out); the claimed fixes one by one with the exact inputs; every code point written is in every installed face's cmap and is drawn by Word and RichEdit per the Word map (spot-check the alternatives the fix chose in Word yourself: new hidden instance, read-only docx you made, PDF export; quit only yours); the .txt writer; that languages.json, encoding-limits.json, RegisterFonts and the csproj kept main's other changes (compare with ${SNAP}); the Gujarati/Devanagari/Sanskrit truth tests still pass; the whole suite once; ConfigCheck. Report defects with severity; clean = no HIGH or MEDIUM left.`

// ---------------------------------------------------------------- Merge

const P3B = `
POLICY REFINEMENT (decided by the lead after reading both fix reports; apply it in this merge to every table: guj7, guj456, dev7, dev456, san7 and the five Bangla/Assamese families, through the shared policy code and the generators):
P3b. A character that NO code point of the target fonts draws (for example ₹ U+20B9 in the Gujarati Shree fonts, where Shree's own 3C has no glyph and Word shows '<'; ૱; °; ৱ where no Word-visible code reaches its glyph) is passed through as its own Unicode character and reported, never left out, because Word shows it in a fallback font (Nirmala UI) and the typist sees the right character. In typing such a character is typed as Unicode, never swallowed (the client asked for the Gujarati ₹ key on 2026-10-07: it must keep working). 'Leave it out and report' stays only for characters whose own code point the fonts map to a DIFFERENT glyph (ASCII Latin letters, '<', '[', ']' and the like). A .txt cannot hold such a character in Windows-1252: there it is left out and reported, never '?'.
RichEdit/WordPad drawing U+0152 U+0153 U+0160 U+0161 U+0178 U+0192 as Latin letters (charset 238) while Word draws them right: no change; mention it in your report.`

const mergePrompt = (round, defects, prev, guj, ban) => `${COMMON}
${POLICY}
${P3B}
YOU ARE THE MERGE AGENT. You edit and build the MAIN checkout ${REPO} (now holding the finished Bangla/Assamese work); nobody else edits it during your run. Scratch: ${R3}\\merge${round}.
${round === 1 ? `TASK: 3-way merge the Gujarati fix from the copy ${GUJCOPY} into main. Base = ${SNAP} (the state both started from). Read ${GUJCOPY}\\MERGE.md and both agents' final reports below. For every file the copy added or changed relative to the base: if main's file equals the base, take the copy's file; if main changed it too, run git merge-file -p <main file> <base file> <copy file> into a temp file and resolve every conflict by understanding both intents; never drop either side's change. New files: copy. Then re-run EVERY table generator in main (tools/Guj7Truth build, tools/ShreeDevTruth dev7 and san7 build, tools/BanTruth build) so every table uses the merged shared policy, and confirm each generator's own checks (truth, words, reverse). Run the whole suite once and ConfigCheck; fix only what the merge broke. Report every conflict and its resolution, and which generated tables changed after regeneration and why.` : `TASK (round ${round}): the merge skeptic found these defects. Fix them in main (merge mistakes first).
DEFECTS: ${JSON.stringify(defects)}
PREVIOUS REPORT: ${JSON.stringify(prev)}`}
GUJARATI AGENT'S FINAL REPORTS: ${JSON.stringify(guj)}
BANGLA AGENT'S FINAL REPORTS: ${JSON.stringify(ban)}`

const mergeCheckPrompt = (round, fix, guj, ban) => `${COMMON}
${POLICY}
${P3B}
YOU ARE THE INDEPENDENT MERGE SKEPTIC. Do not edit the main checkout: copy src, tests, tools, assets, FontSoftware.sln (no bin/obj) to ${R3}\\merge-check${round}\\repo and build there.
THE MERGE REPORT: ${JSON.stringify(fix)}
Check that main now holds BOTH agents' work and that nothing regressed: for each Gujarati audit bug (${R3}\\result-guj-audit.json) its repro; the audit's L1/L2 harness (${R3}\\guj-audit) against the saved composer answers, pointed at your copy; truth/words/reverse for guj7, guj456, dev7, dev456, san7 and the five Bangla/Assamese families; the generators regenerate every shipped table byte-identically; Word visibility of the code points the merged tables write; the .txt/.docx paths; the claims in both agents' final reports below hold in the merged code; the whole suite once; ConfigCheck. Report defects with severity; clean = no HIGH or MEDIUM left.
GUJARATI AGENT'S FINAL REPORTS: ${JSON.stringify(guj)}
BANGLA AGENT'S FINAL REPORTS: ${JSON.stringify(ban)}`

// ---------------------------------------------------------------- run

async function fixLoop(name, phaseName, makeFix, makeCheck, maxRounds) {
  const rounds = []
  let defects = null
  let prevFix = null
  for (let r = 1; r <= maxRounds; r++) {
    const fix = await agent(makeFix(r, defects, prevFix), { label: `${name}:fix${r}`, phase: phaseName, schema: FIX_SCHEMA })
    if (!fix) { log(`${name}: fix round ${r} returned nothing; stopping this stream`); break }
    const check = await agent(makeCheck(r, fix), { label: `${name}:check${r}`, phase: phaseName, schema: CHECK_SCHEMA })
    rounds.push({ round: r, fix, check })
    if (!check) { log(`${name}: skeptic round ${r} returned nothing`); break }
    const open = (check.defects || []).filter((d) => d.severity === 'HIGH' || d.severity === 'MEDIUM').length
    log(`${name}: round ${r} skeptic clean=${check.clean}, ${open} HIGH/MEDIUM open`)
    if (check.clean) break
    defects = check.defects
    prevFix = fix
  }
  return rounds
}

const last = (rounds) => (rounds.length ? { fix: rounds[rounds.length - 1].fix, check: rounds[rounds.length - 1].check } : null)

const [gujarati, bangla] = await parallel([
  () => fixLoop('gujarati', 'Gujarati fix', gujFixPrompt, gujCheckPrompt, 2),
  () => fixLoop('bangla', 'Bangla', banglaFixPrompt, banglaCheckPrompt, 2),
])

let merge = []
if (gujarati && gujarati.length) {
  const g = last(gujarati)
  const b = last(bangla || [])
  merge = await fixLoop('merge', 'Merge',
    (r, d, p) => mergePrompt(r, d, p, g, b),
    (r, f) => mergeCheckPrompt(r, f, g, b), 2)
} else {
  log('Gujarati stream produced nothing: merge skipped')
}
return { gujarati, bangla, merge }

export const meta = {
  name: 'six-scripts-tables',
  description: 'Composer ground truth for Telugu (resume), Kannada, Tamil, Malayalam; then Shree-Lipi 7 and 4/5/6 tables + per-key layout audit for Punjabi, Odia, Telugu, Kannada, Tamil, Malayalam, each in its own repo copy, each with a skeptic. Max 2 agents.',
  phases: [
    { title: 'Capture', detail: 'composer ground truth, serialized' },
    { title: 'Tables', detail: 'generator + tables + tests + layout audit per script, in own copies' },
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

const COMMON = `
PROJECT: TextSoft, a .NET 8 WPF app that types Indian languages into any Windows program through keyboard layouts, and converts text/files between Unicode and legacy 8-bit fonts. Repo: ${REPO} (git branch master). The working tree holds a week of UNCOMMITTED, verified work: never run git stash, checkout, reset, restore, clean or commit, and never delete files you did not create.
How legacy output works: assets/config/mappings/unicode-to-shree-<enc>.json (forward) and shree-<enc>-to-unicode.json (reverse) are MappingTables (pairs + preRules/postRules, longest match; src/FontSoftware.Core/Conversion/FontConverter.cs, src/FontSoftware.Core/Models/MappingTable.cs). assets/config/languages.json wires each font to an encoding; each language has font layouts 'Shree-Lipi 7.0' (SHREE-<SCR>7-* faces) and 'Shree-Lipi 4.0/5.0/6.0' (SHREE-<SCR>-* faces). The typing engine is src/FontSoftware.Engine/TypingEngine.cs (layouts in assets/config/layouts/*.json, keyboard order rules in src/FontSoftware.Core/Keyboards/TypingOrder.cs).
GROUND TRUTH is Shree-Lipi's own composer DLLs in C:\\Shree-Lipi nxt\\ (S8<Scr><Fam>64.dll: U = Unicode, 7 = Shree-Lipi 7 raw bytes). Before starting read these memory notes in ${MEM}: shree-tutor-api.md, shree-7-vs-456-split.md, shree-keyboard-scheme-files.md, shree-escape-layers.md, shree-keyboards.md, keyboard-typing-order.md, shree-import-legacy-codes.md, legacy-byte-0xad-trap.md, live-testing-rules.md; and docs/CONVERTER.md and docs/KEYBOARDS.md.
HOW IT WAS DONE BEFORE: Gujarati (tools/Guj7Truth), Devanagari/Sanskrit (tools/ShreeDevTruth) and Bangla/Assamese (tools/BanTruth, now in the main checkout) were each rebuilt from a composer capture: rules for what the composer does everywhere + a learner that adds a pair only where composing from pieces gives the wrong bytes; a test fixture of the truth; tests ShreeComposerTruthTests, ShreeDevanagariTruthTests, ShreeBanglaTruthTests, ShreeNonIndicTests, ShreeFontCmapTests. Last round's evidence: ${PREV}\\scratchpad\\shree7\\ (family-diff-456-vs-7.json, *-groundtruth.json). Capture pipelines: C:\\Users\\harde\\AppData\\Local\\Temp\\tsk-ban (Bangla), tsk-dev, tsk7g, and this round's Punjabi capture ${R3}\\cap-pun (probe2 C# tool that dumps/restores SLNXT's shared memory, gen*.js, build.js, collectv.js, groundtruth.js, measure\\ harness).
THE CLIENT has been angry that 'tests pass' while the real product fails. Green unit tests are NOT evidence: measure the real FontConverter and TypingEngine against Shree's own output, byte for byte, and say exactly what you measured.
PC LIMITS: 7.5 GB RAM and little of it free; up to three other agents work beside you. Run at most ONE dotnet build or test process at a time; filtered tests while iterating, the whole suite at most once, at the end. Builds may be slow (paging): if something dies out of memory, wait a minute and retry. Kill only dotnet/testhost/VBCSCompiler/MSBuild/your own exe processes that YOU started; never kill TextSoft.exe, SLNXT.exe, WINWORD.EXE you did not start, or any other user program.
NO UI: never type into, click, or focus the user's windows; no TypeProbe, SendInput, clipboard or keyboard-hook tests. Word automation only for a visibility check: a NEW hidden instance via COM, read-only documents you made, Quit only your instance.
Write scratch files under your own directory. Be exact: hex bytes, counts, file paths. Your final answer is the structured output.`

const COMPOSER_OWNER = `
COMPOSER RULES: during your run you are the ONLY agent that may load Shree's composer DLLs. They share SLNXT's live shared memory (SL8_DLL_DATA, SL8_DEFAULTS_DATA) with the SLNXT.exe the user may have running, so: one composer process at a time; short backslash paths and NON-vendor file names for probe keyboards; dump SLNXT's shared memory before your first load and write it back when you finish or abort (cap-pun's probe2 --put does this; this morning's 'before' dumps are in ${R3}\\cap-pun\\shmdump\\before). Save every composer answer to files so later agents can work WITHOUT the composer.`

const NO_COMPOSER = `
COMPOSER RULES: do NOT load Shree's composer DLLs (another agent owns them). Use the saved captures.`

const POLICY = `
GLOBAL OUTPUT POLICY for every Shree-Lipi 7 and 4/5/6 table. Route all non-Indic handling through the shared tools/Shared/ShreeNonIndic.cs exactly as Guj7Truth does (do not fork it: it is being extended in another copy right now, and your generator will be re-run against the merged version), and the word-final rule through tools/Shared/ShreeWordFinal.cs:
P1. Indic text: write exactly what Shree's composer writes (the truth), syllable for syllable; a word-final virama is the visible halant.
P2. A character the target fonts draw as itself at its own code (or the script's own digit at an ASCII digit code, as Shree does) is written as that code and NOT reported as unmapped.
P3. A character whose own code the fonts draw as something else is never written as that code: write the fonts' look-alike if they have one, otherwise leave it out and report it (MappingPair.Unmapped). ASCII included.
P4. Word must show what we write. If a code point we write is one Word or RichEdit draws as nothing, write instead: another code point of the same fonts reaching the same glyph; else an equivalent spelling from glyphs Word does draw; else, for punctuation, the ASCII look-alike; else leave it out and report it. Never drop anything silently.
P5. Reading legacy text back accepts every spelling we or Shree ever wrote.
P6. Every output path carries the same code points (typing, RTF/clipboard, .docx, .txt).
WORD-VISIBILITY MAP (finished this morning, covers every installed Shree family): ${R3}\\result-word-map.json (summary) and ${R3}\\word\\ (summary.json, fam\\<family>.json, final-invisible.txt, final-ascii.txt, final-alternatives.txt).`

const CAPTURE_SCHEMA = {
  type: 'object',
  properties: {
    script: { type: 'string' },
    files: { type: 'array', items: { type: 'string' } },
    truthRows: { type: 'number' },
    coverage: { type: 'string' },
    stability: { type: 'string' },
    family: { type: 'string', description: '7 vs 4/5/6 faces: cmaps and glyph differences; can one table serve both' },
    keyboards: { type: 'string', description: 'per-key captures of the vendor keyboards saved (paths, layers)' },
    currentTables: { type: 'string', description: 'the shipped tables measured against the truth' },
    notes: { type: 'string' },
  },
  required: ['script', 'files', 'truthRows', 'coverage', 'stability', 'family', 'currentTables'],
}

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
          detail: { type: 'string' },
        },
        required: ['id', 'status', 'detail'],
      },
    },
    numbers: { type: 'string', description: 'truth, words, hold-out, reverse, per-key layout audit, suite, ConfigCheck' },
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

const S = {
  pun: { name: 'Punjabi (Gurmukhi)', dll: 'Pun', ext: 'PUN', tool: 'PunTruth', test: 'ShreePunjabiTruthTests', lang: 'pa', table: 'shree-pun7', faces: 'SHREE-PUN7-0951/0952 (WPUN7001/2.TTF) and SHREE-PUN-0950/0951/0952/0955/0956 (S09xx0F2.TTF)', notes: 'addak, tippi vs bindi, subjoined ra/ha/va/ya (pairin), half ya, nukta letters, i-sign before its consonant. 7 and 4/5/6 glyphs identical except ₹ at 0x3C (7 only).' },
  ori: { name: 'Odia', dll: 'Ori', ext: 'ORI', tool: 'OriTruth', test: 'ShreeOdiaTruthTests', lang: 'or', table: 'shree-ori7', faces: 'SHREE-ORI7-* (incl. WORI7001/2) and SHREE-ORI-* (0600-0604)', notes: 'pre-base e/ai, split o/au, reph, ya-phala, ra-phala, conjunct ligatures, ୱ ୟ, ୰ and ଽ (the shipped table writes them at C1 code points). 7 and 4/5/6 identical except ₹ at 0x3C in WORI7001/2. The shipped table matched only 32% of the truth this morning.' },
  tel: { name: 'Telugu', dll: 'Tel', ext: 'TEL', tool: 'TelTruth', test: 'ShreeTeluguTruthTests', lang: 'te', table: 'shree-tel7', faces: 'SHREE-TEL7-Apoorva/Asmitha (M/N...890.TTF), Shree-Tel7-001/002, SHREE-TEL-Apoorva/Asmitha (...0F0.TTF); some faces are split over two files (M and N)', notes: 'consonant-specific vowel-sign shapes (gunintham), vattu (subjoined) forms below and after, reph, many ligatures; the shipped table has 18,235 pairs from an older pipeline. 7 and 4/5/6 glyphs identical per the last round.' },
  kan: { name: 'Kannada', dll: 'Kan', ext: 'KAN', tool: 'KanTruth', test: 'ShreeKannadaTruthTests', lang: 'kn', table: 'shree-kan7', faces: 'SHREE-KAN7-* and SHREE-KAN-* (0850-0855, 0899)', notes: 'consonant-specific vowel signs, ottakshara (subjoined) forms, arkavattu reph written after its syllable, ligatures; the reph comes back on a carrier ಕ in composer answers. 7 and 4/5/6 identical per the last round.' },
  tam: { name: 'Tamil', dll: 'Tam', ext: 'TAM', tool: 'TamTruth', test: 'ShreeTamilTruthTests', lang: 'ta', table: 'shree-tam7', faces: 'SHREE-TAM7-* and SHREE-TAM-* (0800-0803, 1316, 3883)', notes: 'pre-base e/ee/ai, split o/oo/au, the unique consonant+i/ii/u/uu glyphs, pulli, ஸ்ரீ, Grantha letters; the e/ai signs once came back as a bare "." (composer settings). 7 and 4/5/6 differ only by redrawn ரி ரீ ர் (same characters).' },
  mal: { name: 'Malayalam', dll: 'Mal', ext: 'MAL', tool: 'MalTruth', test: 'ShreeMalayalamTruthTests', lang: 'ml', table: 'shree-mal7', faces: 'SHREE-MAL7-* only (no 4/5/6 face installed)', notes: 'pre-base e/ee/ai, split o/oo/au, chillu letters (atomic and with ZWJ), reph (dot reph vs ര്), traditional conjunct ligatures, u/uu forms per consonant, ya/ra/va/la signs.' },
}

const capturePrompt = (key) => {
  const s = S[key]
  return `${COMMON}
${COMPOSER_OWNER}
YOUR DIRECTORY: ${R3}\\cap-${key} (keep everything resumable: the probe set, every raw capture and progress on disk). A pristine snapshot of the repo is at ${SNAP}: copy it into your directory if you need to build anything from the repo, and build only there. Do not edit the main checkout.
${key === 'tel' ? `RESUME: an earlier attempt of this task was stopped at 10:52 while analysing its probe results (capp\\P-results.json etc. are in your directory; its transcript is ${WFDIR}\\agent-ae73fa3f2c3432e67.jsonl). Continue from its files; first make sure SLNXT's shared memory is back to the 'before' dump (it may still hold a probe keyboard), and re-run only what is missing or unstable.` : 'If your directory already holds files from an earlier attempt, continue from them.'}
TASK: capture Shree's own ground truth for ${s.name} in the Shree-Lipi 7 family, as this round did for Punjabi (${R3}\\cap-pun, report ${R3}\\result-capture-pun.json) and Odia (${R3}\\cap-ori, ${R3}\\result-capture-ori.json): start from those pipelines. Composers: S8${s.dll}U64.dll (Unicode) and S8${s.dll}764.dll (Shree-Lipi 7, raw bytes). Faces: ${s.faces}. Script specifics: ${s.notes}
1. Learn the script's internal code set from the vendor keyboard files C:\\Shree-Lipi nxt\\*.${s.ext} (406-byte code tables) and the Tutor answers.
2. Probe sequences (at most four codes per conjunct slot; LoadKbdOverlay with the full path and extension, ANSI): every consonant and independent vowel alone; every consonant with every vowel sign, with anusvara/candrabindu/visarga after a sign, with the virama/half/link forms; every two-consonant cluster for all consonant pairs, alone and with each vowel sign; the subjoined forms; the reph in every typing order; nukta letters; special ligatures; frequent three-consonant clusters (from the vendor conjunct slots, the CHM charts in C:\\Shree-Lipi nxt\\data and common words); digits, punctuation, the symbol layer. Run every probe set TWICE and compare; record quirks.
3. Capture every vendor keyboard of this script key by key on all layers (normal/shift, Ctrl+Space, Ctrl+Shift+1) in BOTH families (U and 7) and save it, so a per-key audit needs no composer later.
4. Write ${R3}\\cap-${key}\\${key}7-groundtruth.json in the same shape as the Punjabi one, and a patterns summary.
5. Family check: (3,1) cmaps of every installed 7 and 4/5/6 face of this script, glyphs compared code by code: can one table serve both families, or which codes differ.
6. Measure the CURRENT shipped tables (unicode-to-${s.table}.json and ${s.table}-to-unicode.json in your snapshot copy) with the real FontConverter against your truth: forward exact/accepted %, reverse %, unmapped, failure categories with examples.
Write SLNXT's shared memory back at the end. Report numbers, files and anything that blocks a perfect table.`
}

const P3B = `
P3b (lead's refinement, overrides P3 where they differ): a character that NO code point of the target fonts draws (e.g. ₹ U+20B9 where the fonts have no rupee glyph, °, a sign with no glyph) is passed through as its own Unicode character and reported, never left out, because Word then shows it in a fallback font and a typist sees the right character; typing must type it, never swallow it. 'Leave it out and report' stays only for characters whose own code point the fonts map to a DIFFERENT glyph (ASCII Latin letters, '<', '[', ']' and the like). In a Windows-1252 .txt such a character is left out and reported, never '?'. The Gujarati/Bangla merge in main applies the same rule through tools/Shared/ShreeNonIndic.cs; your generator is re-run against it after the final merge.`

const buildPrompt = (key, round, defects, prev, cap) => {
  const s = S[key]
  return `${COMMON}
${NO_COMPOSER}
${POLICY}${key === 'pun' && round === 1 ? '' : P3B}
YOUR REPO COPY: ${R3}\\tbl-${key}\\repo. If it does not exist, create it: robocopy ${SNAP} -> it (src, tests, tools, assets, FontSoftware.sln; no bin/obj). Edit and build ONLY there; never touch the main checkout (other agents are changing it). Your copy is merged into main later with base ${SNAP}: keep a precise ${R3}\\tbl-${key}\\MERGE.md (every file added or changed, the exact edits to shared files such as languages.json, encoding-limits.json, the test csproj, RegisterFonts, ConfigCheck, and the generator commands to re-run after the merge). Scratch: ${R3}\\tbl-${key}\\round${round}.
THE CAPTURE: ${R3}\\cap-${key}\\ (${key}7-groundtruth.json, per-key vendor keyboard captures, family check, measure harness) and its report: ${cap ? JSON.stringify(cap) : `${R3}\\result-capture-${key}.json`}
${round === 1 && ['tel', 'kan', 'tam', 'mal'].includes(key) ? `RESUME: an earlier attempt of this exact task may have been stopped by a usage limit (22:48 on 2026-10-18). If ${R3}\\tbl-${key}\\repo and ${R3}\\tbl-${key}\\round1 already exist, read MERGE.md, your notes and the generator code there first and continue from that state; do not start over. Its transcript, if any, is the newest agent-*.jsonl whose .meta.json says tables:${key}:build1 in ${WFDIR.replace('wf_d2d50cfe-4e5', 'wf_7ed24c18-f3d')}.
` : ''}${round === 1 ? `TASK: rebuild TextSoft's ${s.name} Shree-Lipi 7 and 4/5/6 output from Shree's ground truth. Script specifics: ${s.notes} Faces: ${s.faces}.
1. A generator tools/${s.tool} (its own project, like tools/Guj7Truth / tools/BanTruth: Rules for what the composer does everywhere, a Learner that adds a pair only where composing from pieces gives the wrong bytes, Build/Measure/Words/CV/Verify/Fixture modes). One table pair per font family that really differs (per the capture's family check); if the 7 and 4/5/6 faces are identical, one table serves both, else a 456 table (and wire the 4/5/6 fonts to it in languages.json).
2. The tables: unicode-to-${s.table}.json and ${s.table}-to-unicode.json (plus any family variant), written ONLY by the generator; encoding-limits.json keys for what is left out.
3. Tests: tests/FontSoftware.Tests/Data/shree-${key}7-truth.json (fixture from the capture) and tests/FontSoftware.Tests/${s.test}.cs: truth per family, joined words, reverse of every composer spelling, word-final virama, non-Indic sweep, cmap of every installed face; add the tables to ShreeNonIndicTests/ShreeFontCmapTests where those enumerate tables.
4. A per-key layout audit, as the Gujarati audit did (its harness is in ${R3}\\guj-audit\\harness; its report ${R3}\\result-guj-audit.json lists the classes of bugs it found): for every vendor keyboard of this script and its TextSoft layout (assets/config/layouts/<prefix>-*.json and the InScript layout of this script), drive the real TypingEngine with your new tables in a 7 profile (and a 4/5/6 profile if one exists) key by key on every layer, and compare with the saved per-key 7-composer answers. Fix layout-data bugs in your copy (missing keys, keys whose code has no Unicode the tables can write, chart 'A or B' artefacts, ligature keys). Engine-wide bugs (Backspace removing a whole chart run, a run that cannot start with a key typing nothing alone) are being fixed in the Gujarati copy for every script: list them in your report, do not fix them.
TARGETS: forward = the composer's preferred bytes on 100% of the truth (justify every documented exception with evidence); 20,000 joined words on two seeds 100%; a 10-fold hold-out reported; reverse reads every composer spelling back exactly or as the same text, never Latin or control characters; P2-P4 (no code point Word cannot draw, per the Word map); every written code point in every installed face's cmap.` : `TASK (round ${round}): the skeptic found these defects. Fix every HIGH and MEDIUM one and the cheap safe LOW ones; for each you leave, give the evidence.
DEFECTS: ${JSON.stringify(defects)}
PREVIOUS REPORT: ${JSON.stringify(prev)}`}
At the end, in your copy: the generator regenerates the tables byte-identically, the ${s.test} and the Shree tests pass, the whole suite once, ConfigCheck. Report before (the shipped table) -> after numbers.`
}

const checkPrompt = (key, round, fix, cap) => {
  const s = S[key]
  return `${COMMON}
${NO_COMPOSER}
${POLICY}${key === 'pun' && round === 1 ? '' : P3B}
YOU ARE THE INDEPENDENT ${s.name.toUpperCase()} SKEPTIC. Do not edit ${R3}\\tbl-${key}\\repo or the main checkout: copy ${R3}\\tbl-${key}\\repo (src, tests, tools, assets, FontSoftware.sln; no bin/obj) to ${R3}\\tbl-${key}\\check${round}\\repo and build there, one build at a time.
THE CAPTURE: ${R3}\\cap-${key}\\ and its report ${cap ? JSON.stringify(cap) : `${R3}\\result-capture-${key}.json`}
THE BUILD AGENT REPORTS: ${JSON.stringify(fix)}
Re-measure everything yourself with the real FontConverter and TypingEngine: regenerate the tables from tools/${s.tool} into a scratch folder and compare byte for byte with the shipped ones; truth from the raw capture ${R3}\\cap-${key}\\${key}7-groundtruth.json (not only the fixture): every row composer-preferred, composer-accepted or a justified exception; 20,000 words on two NEW seeds; reverse on every composer spelling (no Latin or control characters may come out); word-final virama; the BMP non-Indic sweep; every written code point in every installed face's cmap and drawn by Word per the Word map (spot-check in Word yourself: new hidden instance, read-only docx you made, PDF export; quit only yours); re-run the per-key layout audit against the saved per-key composer answers and report remaining mismatches by class; languages.json wiring of every 7 and 4/5/6 font of this script; the whole suite once; ConfigCheck. Look hard for fixes that only make a test pass. Report defects with severity; clean = no HIGH or MEDIUM left.`
}

// ---------------------------------------------------------------- run

const caps = {}
const ready = {}
const resolvers = {}
for (const k of Object.keys(S)) ready[k] = new Promise((r) => { resolvers[k] = r })
resolvers.pun(null) // finished this morning: report in r3\result-capture-pun.json
resolvers.ori(null) // finished this morning: report in r3\result-capture-ori.json
const captured = { pun: true, ori: true }
let resolveAllCaps
const allCaps = new Promise((r) => { resolveAllCaps = r })

async function captureLane() {
  try {
    for (const k of ['tel', 'kan', 'tam', 'mal']) {
      let c = null
      try {
        c = await agent(capturePrompt(k), { label: `capture:${k}`, phase: 'Capture', schema: CAPTURE_SCHEMA })
      } finally {
        caps[k] = c
        captured[k] = !!c
        resolvers[k](c)
      }
      log(`capture ${k}: ${c ? c.truthRows + ' truth rows' : 'FAILED'}`)
    }
  } finally {
    resolveAllCaps(true)
  }
}

async function tableStream(key, cap) {
  const rounds = []
  let defects = null
  let prev = null
  for (let r = 1; r <= 2; r++) {
    const fix = await agent(buildPrompt(key, r, defects, prev, cap), { label: `tables:${key}:build${r}`, phase: 'Tables', schema: FIX_SCHEMA })
    if (!fix) { log(`tables ${key}: build round ${r} returned nothing`); break }
    const check = await agent(checkPrompt(key, r, fix, cap), { label: `tables:${key}:check${r}`, phase: 'Tables', schema: CHECK_SCHEMA })
    rounds.push({ round: r, fix, check })
    if (!check) { log(`tables ${key}: skeptic round ${r} returned nothing`); break }
    const open = (check.defects || []).filter((d) => d.severity === 'HIGH' || d.severity === 'MEDIUM').length
    log(`tables ${key}: round ${r} clean=${check.clean}, ${open} HIGH/MEDIUM open`)
    if (check.clean) break
    defects = check.defects
    prev = fix
  }
  return rounds
}

const order = ['pun', 'ori', 'tel', 'kan', 'tam', 'mal']
let next = 0
const tables = {}
async function worker(name) {
  while (next < order.length) {
    const key = order[next++]
    const cap = await ready[key]
    if (!captured[key]) { log(`${name}: ${key} has no capture; skipped`); continue }
    tables[key] = await tableStream(key, cap)
  }
}

await parallel([
  async () => { await captureLane(); await worker('worker2') },
  () => worker('worker1'),
])

// ---------------------------------------------------------------- final merge into main

const cut = (s, n) => (typeof s === 'string' && s.length > n ? s.slice(0, n) + ' [...]' : s)
const summaries = {}
for (const k of order) {
  const rounds = tables[k] || []
  const lastR = rounds[rounds.length - 1]
  if (!lastR) { summaries[k] = 'NO TABLE BUILT'; continue }
  summaries[k] = {
    copy: `${R3}\\tbl-${k}\\repo (MERGE.md there)`,
    rounds: rounds.length,
    buildNumbers: cut(lastR.fix && lastR.fix.numbers, 2500),
    buildNotes: cut(lastR.fix && lastR.fix.notes, 1500),
    checkClean: lastR.check ? lastR.check.clean : null,
    checkVerdict: cut(lastR.check && lastR.check.verdict, 1200),
    openDefects: lastR.check ? lastR.check.defects.filter((d) => d.severity === 'HIGH' || d.severity === 'MEDIUM').map((d) => cut(d.text, 600)) : [],
  }
}
const built = order.filter((k) => summaries[k] !== 'NO TABLE BUILT')
log(`final merge: ${built.length} scripts built (${built.join(', ')})`)

const MAIN_STATE = `
STATE OF THE MAIN CHECKOUT ${REPO}: since ${SNAP} was taken (08:30 on 2026-10-18) main received the Gujarati 15-bug fix (engine: Backspace undo stack, waiting half forms, one virama not two; layouts: chart 'A or B' runs split in every script, Eng/Ekl keyboards; FileConversion run scope for .docx/.rtf; ConverterViewModel/MainViewModel font lists; tools/Shared/ShreeNonIndic.cs with the P2-P4 + P3b policy and its overloads) and the Bangla/Assamese work (tools/BanTruth, ten tables, raw-byte .txt writer), all merged and independently checked clean (961/961 tests, ConfigCheck 0 errors). Its reports: ${WFDIR.replace('wf_d2d50cfe-4e5', 'wf_67bf1827-89f')}\\journal.jsonl (the last two result lines are the merge fix and its clean check).
Each script's full build/check reports: ${WFDIR.replace('wf_d2d50cfe-4e5', 'wf_7ed24c18-f3d')}\\journal.jsonl.`

const finalMergePrompt = (round, defects, prev) => `${COMMON}
${NO_COMPOSER}
${POLICY}${P3B}
${MAIN_STATE}
YOU ARE THE FINAL MERGE AGENT. You edit and build the MAIN checkout ${REPO}; nobody else edits it during your run. Scratch: ${R3}\\final-merge${round}.
${round === 1 ? `TASK: bring the new Shree-Lipi 7 and 4/5/6 work of ${built.join(', ')} into main, from their copies ${R3}\\tbl-<key>\\repo (each based on ${SNAP}; read each copy's MERGE.md). For every file a copy added or changed relative to the base: if main's file equals the base, take the copy's file; if main changed it too (languages.json, encoding-limits.json, the test csproj, ShreeNonIndicTests/ShreeFontCmapTests, ConfigCheck, RegisterFonts, layouts the Gujarati fix also touched), run git merge-file -p <main> <base> <copy> into a temp file and resolve every conflict by understanding both intents; never drop either side. Merge the copies one at a time and build after each. Then re-run EVERY table generator in main (Guj7Truth, ShreeDevTruth dev7 and san7, BanTruth, and each new script's generator) so all tables use main's shared policy (P1-P6 + P3b), and confirm each generator's own checks. Engine-wide bugs the script agents listed but did not fix: check whether main's engine already fixes them (Backspace in runs, runs starting with a key that types nothing); fix what is still open.
SECONDARY, after the merge is green (LOW items left by the Gujarati/Bangla check; fix each only if it is safe, with a test): (1) Gujarati reph with a visarga: ધર્મઃ is written 5E 64 3A AE and Word draws the reph over ઃ; compare with what Shree's composer writes in ${PREV}\\scratchpad\\shree7\\guj7-groundtruth.json and r3\\guj-audit captures, and follow the composer; (2) a reph combined with anusvara/candrabindu after ૢ/ૣ lands on the fallback dotted circle; (3) the B7 bullet stand-in reads back as · but is reported as unmapped in the Bangla reverses, and as • in Gujarati/Devanagari: make it consistent; (4) the bytes the shipped interim build (merge 1, ${R3}\\merge-check1\\repo) wrote with a reph after a passed-through sign must read back right (P5).
SCRIPT REPORTS: ${JSON.stringify(summaries)}` : `TASK (round ${round}): the final skeptic found these defects. Fix them in main (merge mistakes first).
DEFECTS: ${JSON.stringify(defects)}
PREVIOUS REPORT: ${JSON.stringify(prev)}`}
At the end: the whole suite once, ConfigCheck, and a typing sweep (19,000+ lines over all layouts, as the Gujarati merge check did) diffed against the main state before your merge, explaining every difference. Report every conflict and its resolution, per-script before (the old table) -> after numbers measured in main, and what changed in which generated tables.`

const finalCheckPrompt = (round, fix) => `${COMMON}
${NO_COMPOSER}
${POLICY}${P3B}
${MAIN_STATE}
YOU ARE THE FINAL INDEPENDENT SKEPTIC. Do not edit the main checkout: copy src, tests, tools, assets, FontSoftware.sln (no bin/obj) to ${R3}\\final-check${round}\\repo and build there.
THE FINAL MERGE REPORT: ${JSON.stringify(fix)}
Check that main holds every script's work and nothing regressed: every generator regenerates every shipped table byte-identically; per script (Gujarati, Devanagari, Sanskrit, Bangla/Assamese and ${built.join(', ')}) truth from the raw composer captures, 20,000 words on two NEW seeds, reverse with no Latin or control characters, word-final virama, and the per-key layout audit against the saved composer answers (Gujarati: ${R3}\\guj-audit; others: ${R3}\\cap-<key>); the Gujarati audit's bug repros still pass; every written code point is in every installed face's cmap and drawn by Word (spot-check in Word yourself: new hidden instance, read-only docx you made, PDF export; quit only yours); languages.json wires every Shree 7 and 4/5/6 face of every script to the right table and the font lists under 'Shree-Lipi 7.0' and '4.0/5.0/6.0' show only that family; .txt/.docx/.rtf paths; the whole suite once; ConfigCheck. Report defects with severity; clean = no HIGH or MEDIUM left.`

let finalRounds = []
if (built.length) {
  let defects = null
  let prev = null
  for (let r = 1; r <= 2; r++) {
    const fix = await agent(finalMergePrompt(r, defects, prev), { label: `final:merge${r}`, phase: 'Tables', schema: FIX_SCHEMA })
    if (!fix) { log(`final merge round ${r} returned nothing`); break }
    const check = await agent(finalCheckPrompt(r, fix), { label: `final:check${r}`, phase: 'Tables', schema: CHECK_SCHEMA })
    finalRounds.push({ round: r, fix, check })
    if (!check) { log(`final check round ${r} returned nothing`); break }
    log(`final: round ${r} clean=${check.clean}`)
    if (check.clean) break
    defects = check.defects
    prev = fix
  }
}
return { captures: caps, tables, finalRounds }

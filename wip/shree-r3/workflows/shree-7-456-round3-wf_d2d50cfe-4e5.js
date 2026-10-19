export const meta = {
  name: 'shree-7-456-round3',
  description: 'Shree-Lipi 7 and 4/5/6: Gujarati end-to-end audit vs Shree composer, Word-visibility map, Bangla round 2 port+fix, Gujarati fix, skeptics; composer ground truth for Punjabi/Odia/Telugu/Kannada/Tamil/Malayalam. Two lanes, max 2 agents, composer serialized in lane A.',
  phases: [
    { title: 'Gujarati audit', detail: 'every key, layer, keyboard and typing path vs Shree composer' },
    { title: 'Word map', detail: 'which font code points Word and RichEdit draw' },
    { title: 'Bangla', detail: 'port worktree into main, round-2 fixes, skeptic' },
    { title: 'Gujarati fix', detail: 'fix audit bugs + global policy, skeptic' },
    { title: 'Capture', detail: 'composer ground truth for the other six scripts + measure current tables' },
  ],
}

const REPO = 'C:\\Users\\harde\\Desktop\\Windows Font Software'
const SP = 'C:\\Users\\harde\\AppData\\Local\\Temp\\claude\\C--Users-harde-Desktop-Windows-Font-Software\\7673923b-96b4-4287-87dd-a8e7c6533eca\\scratchpad'
const R3 = SP + '\\r3'
const SNAP = SP + '\\snap-base'
const PREV = 'C:\\Users\\harde\\AppData\\Local\\Temp\\claude\\C--Users-harde-Desktop-Windows-Font-Software\\624671f5-ee6d-402b-b810-05b52f236677'
const MEM = 'C:\\Users\\harde\\.claude\\projects\\C--Users-harde-Desktop-Windows-Font-Software\\memory'
const WT = REPO + '\\.claude\\worktrees\\wf_607a9ebd-c6c-2'

const COMMON = `
PROJECT: TextSoft, a .NET 8 WPF app that types Indian languages into any Windows program through keyboard layouts, and converts text/files between Unicode and legacy 8-bit fonts. Repo: ${REPO} (git branch master). The working tree holds a week of UNCOMMITTED, verified work: never run git stash, checkout, reset, restore, clean or commit, and never delete files you did not create.
How legacy output works: assets/config/mappings/unicode-to-shree-<enc>.json (forward) and shree-<enc>-to-unicode.json (reverse) are MappingTables (pairs + preRules/postRules, longest match; src/FontSoftware.Core/Conversion/FontConverter.cs, src/FontSoftware.Core/Models/MappingTable.cs). assets/config/languages.json wires each font to an encoding; each language has font layouts 'Shree-Lipi 7.0' (SHREE-<SCR>7-* faces, files S<dddd>892.TTF and W<SCR>700x.TTF) and 'Shree-Lipi 4.0/5.0/6.0' (SHREE-<SCR>-* faces, S<dddd>0F2.TTF). The typing engine is src/FontSoftware.Engine/TypingEngine.cs (layouts in assets/config/layouts/*.json, keyboard order rules in src/FontSoftware.Core/Keyboards/TypingOrder.cs).
GROUND TRUTH is Shree-Lipi's own composer DLLs in C:\\Shree-Lipi nxt\\ (S8<Scr><Fam>64.dll: U = Unicode, 7 = Shree-Lipi 7 raw bytes; the 4/5/6 fonts share the 7 codes except where their glyphs differ). Before starting read these memory notes in ${MEM}: shree-tutor-api.md, shree-7-vs-456-split.md, shree-keyboard-scheme-files.md, shree-escape-layers.md, shree-keyboards.md, keyboard-typing-order.md, shree-import-legacy-codes.md, legacy-byte-0xad-trap.md, live-testing-rules.md; and docs/CONVERTER.md and docs/KEYBOARDS.md in the repo.
EVIDENCE FROM THE LAST ROUND: ${PREV}\\scratchpad\\shree7\\ (guj7/dev7/san7/ban7-groundtruth.json, family-diff-456-vs-7.json, audit.json); the capture pipelines C:\\Users\\harde\\AppData\\Local\\Temp\\tsk7g (Gujarati), tsk-dev (Devanagari/Sanskrit), tsk-ban (Bangla/Assamese, the most evolved: probe keyboards whose conjunct slots hold code runs, a C# probe exe that loads the U and 7 composers, run twice for stability); the table generators tools/Guj7Truth, tools/ShreeDevTruth (main checkout) and tools/BanTruth (in the worktree ${WT}); shared policy tools/Shared/ShreeNonIndic.cs and ShreeWordFinal.cs; tests ShreeComposerTruthTests, ShreeDevanagariTruthTests, ShreeSanskritTruthTests, ShreeNonIndicTests, ShreeFontCmapTests, TypingEngineTests.
THE CLIENT: says TextSoft's Shree-Lipi 7 and 4/5/6 Gujarati output has 'many bugs' (not listed) and has been angry that 'tests pass' while the real product fails. Green unit tests are NOT evidence: measure the real FontConverter and TypingEngine against Shree's own output, byte for byte, and say exactly what you measured.
PC LIMITS: 7.5 GB RAM, about 3 GB free; one other agent works beside you. Run at most ONE dotnet build or test process at a time; use filtered tests while iterating and the whole suite at most once, at the end. If something dies out of memory, wait a minute and retry. Kill only dotnet/testhost/VBCSCompiler/your own exe processes that YOU started; never kill TextSoft.exe, SLNXT.exe, WINWORD.EXE you did not start, or any other user program.
NO UI: never type into, click, or focus the user's windows; no TypeProbe, SendInput, clipboard or keyboard-hook tests in this run (the live test comes later, when the user is away). Word automation only where your task says so: a NEW hidden instance via COM, documents opened read-only, never the user's documents, and Quit only the instance you started.
Write all your scratch files under your own directory (given below). Be exact: hex bytes, counts, file paths. Your final answer is the structured output.`

const COMPOSER_OWNER = `
COMPOSER RULES: during your run you are the ONLY agent that may load Shree's composer DLLs. They share SLNXT's live shared memory (SL8_DLL_DATA) with the SLNXT.exe the user may have running, so: one composer process at a time; short backslash paths and NON-vendor file names for probe keyboards; record SLNXT's current keyboard first and restore it when you finish or abort (see C:\\Users\\harde\\AppData\\Local\\Temp\\tsk7g\\restore.js and tsk-ban). Save every composer answer you collect to files so later agents can use them WITHOUT the composer.`

const NO_COMPOSER = `
COMPOSER RULES: do NOT load Shree's composer DLLs (another agent owns them during your run). Use the saved captures listed in your task.`

const POLICY = `
GLOBAL OUTPUT POLICY for every Shree-Lipi 7 and 4/5/6 table (Gujarati, Devanagari, Sanskrit, Bangla/Assamese now; the other scripts later). Apply it through the shared generator code (tools/Shared/ShreeNonIndic.cs and the per-script generators), never by hand-editing generated tables:
P1. Indic text: write exactly what Shree's composer writes (the truth), syllable for syllable; a word-final virama is the visible halant (tools/Shared/ShreeWordFinal.cs).
P2. A character the target fonts draw as itself at its own code (or the script's own digit at an ASCII digit code, as Shree does) is written as that code and NOT reported as unmapped (e.g. , . ( ) ? ! - and digits).
P3. A character whose own code the fonts draw as something else (e.g. ASCII '[' at 5B drawing an i-sign, ':' drawing a visarga, '"' drawing a vowel letter) is never written as that code: write the fonts' look-alike if they have one, otherwise leave it out and report it (MappingPair.Unmapped, which main's Core already supports). This covers ASCII as well as non-ASCII.
P4. Word must show what we write. If a code point we write is one Word or RichEdit draws as nothing (see the Word-visibility map), write instead: another code point of the same fonts that reaches the same glyph; else an equivalent spelling from glyphs Word does draw that reads as the same text (e.g. half form + letter, letter + explicit virama); else, for punctuation, the ASCII look-alike the fonts draw; else leave it out and report it. Never drop anything silently.
P5. Reading legacy text back accepts every spelling we or Shree ever wrote (including the old C1 code points in existing documents).
P6. Every output path carries the same code points: typing, RTF/clipboard, .docx, and .txt (a .txt must write U+0080-U+00FF as their raw bytes, never '?').`

const AUDIT_SCHEMA = {
  type: 'object',
  properties: {
    summary: { type: 'string' },
    counts: { type: 'string', description: 'what was measured per lens, with numbers' },
    bugs: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          lens: { type: 'string' },
          severity: { type: 'string', enum: ['HIGH', 'MEDIUM', 'LOW'] },
          title: { type: 'string' },
          repro: { type: 'string', description: 'exact layout, keys or text, font family' },
          textsoft: { type: 'string', description: 'what TextSoft writes (hex) and how it looks' },
          shree: { type: 'string', description: 'what Shree writes (hex) and how it looks' },
          cause: { type: 'string', description: 'file:line or table/rule responsible' },
          fix: { type: 'string' },
          evidence: { type: 'string' },
        },
        required: ['id', 'severity', 'title', 'repro', 'textsoft', 'shree', 'cause', 'fix'],
      },
    },
    dataFiles: { type: 'array', items: { type: 'string' }, description: 'saved composer answers and harnesses, with what each holds' },
    notes: { type: 'string' },
  },
  required: ['summary', 'counts', 'bugs', 'dataFiles'],
}

const WORD_SCHEMA = {
  type: 'object',
  properties: {
    summary: { type: 'string' },
    files: { type: 'array', items: { type: 'string' } },
    invisible: { type: 'string', description: 'per family: code points Word and/or RichEdit fail to draw, and what each shipped table writes there' },
    alternatives: { type: 'string', description: 'per invisible code point: other code points reaching the same glyph, or none' },
    asciiAndPunct: { type: 'string', description: 'per family: what each ASCII 0x20-0x7E and cp1252 punctuation code point draws (itself, look-alike, script digit, Indic glyph, nothing)' },
    notes: { type: 'string' },
  },
  required: ['summary', 'files', 'invisible', 'alternatives', 'asciiAndPunct'],
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

const CAPTURE_SCHEMA = {
  type: 'object',
  properties: {
    script: { type: 'string' },
    files: { type: 'array', items: { type: 'string' } },
    truthRows: { type: 'number' },
    coverage: { type: 'string', description: 'what the probe set covers, and what it does not' },
    stability: { type: 'string', description: 'run-to-run agreement, quirks' },
    family: { type: 'string', description: '7 vs 4/5/6 faces: cmaps and glyph differences; can one table serve both' },
    keyboards: { type: 'string', description: 'per-key captures of the vendor keyboards saved' },
    currentTables: { type: 'string', description: 'the shipped tables measured against the truth: forward/reverse %, failure categories with examples' },
    notes: { type: 'string' },
  },
  required: ['script', 'files', 'truthRows', 'coverage', 'stability', 'family', 'currentTables'],
}

// ---------------------------------------------------------------- prompts

const AUDIT_PROMPT = `${COMMON}
${COMPOSER_OWNER}
YOUR DIRECTORY: ${R3}\\guj-audit (create it). A pristine snapshot of the repo sources is at ${SNAP}: copy it to ${R3}\\guj-audit\\repo and build ONLY there (another agent edits and builds the main checkout). Do not edit the main checkout at all.

TASK: AUDIT, do not fix. Find every way TextSoft's Gujarati output in Shree-Lipi 7.0 (SHREE-GUJ7-*, Shree-Guj7-00x) and 4.0/5.0/6.0 (SHREE-GUJ-*) fonts differs from what Shree-Lipi itself writes, end to end, as a typist and a converter user experience it. The Gujarati syllable tables already match the composer on 17,335 syllables and 20,000 joined words, so look beyond that. Cover all six lenses and report numbers for each:

L1 PER-KEY: for every Gujarati vendor keyboard in C:\\Shree-Lipi nxt\\*.GUJ (AKSHAR ASTERIX BPMK CRTRONIC DOE ENG INDICA INSCRIPT ITR MTNK NEWROMA PHONET86 PHONETIC RAMING RAMING1 RAMING2 ROMA SCRIPT TYPEWRIT) and its TextSoft layout (assets/config/layouts/guj-*.json, gujarati-inscript.json; chart-only ones guj-modular/guj-ekl have no vendor file: check them against their charts and the U composer only), ask the Tutor API of S8GujU64 (Unicode) and S8Guj764 (Shree-Lipi 7 raw bytes) what every key types on every layer (normal and shifted ASCII keys, the Ctrl+Space layer, the Ctrl+Shift+1 symbol layer; the tools/KbdCapture code shows the call shapes; also the 406-byte file itself, which gives the code per key). Then drive TextSoft's REAL TypingEngine (as TypingEngineTests do) with that layout and a Shree-Lipi 7 font profile, and again with a 4/5/6 font profile, press the same key, and collect the exact text TextSoft emits. Compare: (a) TextSoft types nothing where Shree types something, (b) different bytes for the same Unicode (table bug), (c) different Unicode (layout bug), (d) composer quirks (carriers etc., per the memory notes). Save all composer answers to files.
L2 TYPING DYNAMICS: type realistic Gujarati words key by key through the TypingEngine with each layout (at least the 8 most used: INSCRIPT, BPMK, ITR, TYPEWRIT, PHONETIC, AKSHAR, TERAFONT-like if present, NEWROMA; plus every layout for a smaller set), in a Shree-Lipi 7 and a 4/5/6 profile. Use a few hundred real common Gujarati words (write a list; include i-sign words, reph words like ધર્મ કાર્ય, conjuncts ક્ષ જ્ઞ શ્ર ત્ર દ્ધ દ્ય, nukta, candrabindu, anusvara, રુ રૂ, ₹, numbers, punctuation , . ? ! - ( ) " ' : ; / %, spaces, Enter, Backspace in the middle of a word, a word typed after a mouse click). Simulate the target app's text buffer applying TextSoft's emitted backspaces and text, and check the final buffer equals FontConverter(final Unicode) and equals the composer's bytes for those syllables; report wrong intermediate states, wrong backspace counts, leftovers, and results that depend on typing order.
L3 PUNCTUATION, DIGITS, SYMBOLS: for every printable ASCII character and – — … “ ” ‘ ’ • ₹ % ° × ÷ । ॥ ૐ ઽ ૰ ૱: what the forward tables write in each family, what the fonts DRAW at that code (render by glyph index with GDI and look; compare to the intended character), what Shree's composer types for the key that produces it on its keyboards, and whether the converter reports it. Known suspicions to confirm or refute: ASCII '[' ']' '{' '}' '@' '\\\\' '^' '_' '\`' '|' '&' '$' '>' ':' are written as codes the fonts draw as Gujarati glyphs or nothing; – — are written as U+0081, ÷ as U+008D, ૰ as U+008F (C1 control code points, which Word was seen drawing as nothing for Bangla); ', . ( ) 1 ? !' are reported as 'had no mapping'.
L4 READING BACK (legacy -> Unicode): read the composer's own bytes (truth spellings, per-key outputs from L1, joined words) and the bytes TextSoft wrote in L2/L3 through shree-guj7-to-unicode and shree-guj456-to-unicode; report wrong readings. Known open items to confirm: an in-word ZWNJ halant is dropped (4C B9 24 4C 24 reads ક્ક), 22 22 / 27 27 read back as ‘‘ ’’ instead of “ ”, … comes back as '...', U+FDD3 leaks for a virama with no letter before ન with an i-sign or nukta, B7 reads as •.
L5 FONT LISTS AND PROFILES: in the app's view models (MainViewModel FillFonts, FontLayout.Includes, ConverterViewModel) check which fonts appear under Gujarati 'Shree-Lipi 7.0' and '4.0/5.0/6.0' for typing and for the converter, against the faces really installed in C:\\Windows\\Fonts (read their name tables); the default font per layout; names shown; what happens with a saved setting from an older version; and that a 7 face never gets the 456 table or vice versa.
L6 FILE AND OUTPUT PATHS: for every code point the Gujarati tables can write, what reaches a .txt (FileConversion.WriteTxt uses Windows-1252), .rtf, .docx, and the typing injector (KEYEVENTF_UNICODE / RTF paste in InputInjector): flag anything that becomes '?', is dropped, or changes. Do NOT open Word.

For every bug give the exact repro, TextSoft's output (hex), Shree's output (hex), the cause (file:line or rule), and a concrete fix. Rank HIGH = a typist or converter user sees wrong/missing text in normal use; MEDIUM = visible in less common use; LOW = edge cases. Do not report the same root cause twice: group instances under one bug and give the count.`

const WORD_PROMPT = `${COMMON}
${NO_COMPOSER}
YOUR DIRECTORY: ${R3}\\word (create it). Do not edit the repo.

TASK: build the WORD-VISIBILITY MAP the fix agents need. For every installed non-Ex Shree font family (one representative face each, and confirm every face of the family has the same cmap): SHREE-GUJ7, SHREE-GUJ, Shree-Guj7-00x (WGUJ700x.TTF), SHREE-DEV7, SHREE-DEV, Shree-Dev7-00x, SHREE-SAN7, SHREE-BAN7, SHREE-BAN, Shree-Ban7-00x, SHREE-ASS7, SHREE-ASS, Shree-Ass7-00x, SHREE-ORI7, SHREE-ORI, SHREE-PUN7, SHREE-PUN, SHREE-TAM7, SHREE-TAM, SHREE-TEL7, SHREE-TEL, SHREE-KAN7, SHREE-KAN, SHREE-MAL7 (skip any not installed and say so; read C:\\Windows\\Fonts and the user font folder %LOCALAPPDATA%\\Microsoft\\Windows\\Fonts):
1. Read the (3,1) cmap: every mapped code point and its glyph id; find code points that reach the same glyph id or an identical outline.
2. For every mapped code point decide whether Microsoft Word on this PC draws it: write a .docx yourself (OpenXML; one line per code point, in that font, with a visible marker before and after), open it in a NEW hidden Word instance via COM read-only (Word here is the free edition: it opens documents read-only, which is all you need), export to PDF, render the PDF (tools/PdfRender in the repo, or another local method), and compare each line's ink with the glyph drawn by glyph index (GDI ExtTextOut ETO_GLYPH_INDEX). Quit only the Word instance you started. Do the same for RichEdit (msftedit.dll in your own hidden window, both typed WM_CHAR and RTF \\uN? paste) because the typing injector pastes RTF.
3. Describe what each of these code points draws: every ASCII 0x20-0x7E, the cp1252 punctuation (U+2013 U+2014 U+2018-U+201E U+2022 U+2026 U+00B7 U+00A0 U+00AD), and every code point Word or RichEdit fails to draw: itself, a look-alike (which), the script's digit, an Indic glyph (which, using the composer truth files in ${PREV}\\scratchpad\\shree7\\*-groundtruth.json and the shipped reverse tables to name it), or nothing.
4. For every code point Word or RichEdit fails to draw, list what each shipped forward table writes there (assets/config/mappings/unicode-to-shree-*.json in the main checkout, and the Bangla/Assamese tables in ${WT}\\assets\\config\\mappings), and the visible alternatives (other code points reaching the same glyph, or none).
Write per-family JSON files ({family, faces, codepoints:[{cp, gid, word, richedit, sameGlyphAs, draws}]}) and one summary file; return the summary in the structured output. Known so far (verify, do not assume): Word gave U+009E, U+0081, U+0090 an advance but drew nothing in SHREE-BAN faces; U+017E fell back to Cambria; U+00AD is hidden by WPF but drawn by Word.
RESUME: an earlier attempt of this exact task ran 01:25-03:15 and was cut off by an internet outage while writing its answer. Its files are in your directory: summary.json, fam/*.json (295 files), final-invisible.txt, final-ascii.txt, final-alternatives.txt, findings.js and the harnesses. Read them first, check they are complete and consistent with the four steps above (fill only real gaps; do not redo the Word/RichEdit runs unless something is missing or wrong), make sure no Word or helper process of yours is left running, and return the structured summary.`

const banglaFixPrompt = (round, defects, prev, word) => `${COMMON}
${NO_COMPOSER}
${POLICY}
YOUR DIRECTORY: ${R3}\\bangla-fix${round} (scratch). You edit and build the MAIN checkout ${REPO}; you are the only agent editing or building there during your run.

WORD-VISIBILITY MAP (from the agent before you; files listed there): ${word ? JSON.stringify(word) : 'NOT AVAILABLE - the map agent failed; do the Word check for the Bangla/Assamese code points you write yourself, as the last Bangla skeptic did (new hidden Word instance via COM, read-only docx, PDF export, compare ink; quit only your instance).'}

${round === 1 ? `TASK (round 1): bring the Bangla/Assamese Shree-Lipi 7 and 4/5/6 work into the main checkout and finish it.
SOURCE: the worktree ${WT} (based on commit b96047d, WITHOUT main's uncommitted work). Its round-1 report and the skeptic's report are in ${PREV}\\tasks\\wps8wzzb3.output (read it all). It changed: tools/BanTruth/*, ten tables (unicode-to-shree-{ban7,ban456,ass7,ass456,ass7w}.json and their reverses), assets/config/encoding-limits.json (five keys), assets/config/languages.json (bn/as/mni font->encoding, eight mapping entries), tools/RegisterFonts/Program.cs (--by-series), tests/FontSoftware.Tests/ShreeBanglaTruthTests.cs, Data/shree-ban7-truth.json, Data/shree-ban-cmaps.json, the test csproj. A round-2 agent then worked in the same worktree for 117 tool calls and died (weekly limit) without reporting: its transcript is under ${PREV}\\subagents\\workflows\\wf_7ad28609-595\\ (agent-*.jsonl); find out what it changed (git -C the worktree diff b96047d, compare file times with the round-1 report) and keep what is good.
MAIN since gained: MappingPair.Unmapped (Core), tools/Shared/ShreeNonIndic.cs and ShreeWordFinal.cs, FontConverter/ConversionResult changes, ConfigCheck changes, Gujarati/Devanagari/Sanskrit tables and tests, MouseClickWatch, etc. Merge file by file (git -C <worktree> diff b96047d -- <file> shows exactly what the worktree changed); never overwrite main's changes in languages.json, encoding-limits.json, the csproj, RegisterFonts or anything else. Make BanTruth build against main's Core and follow main's shared policy (P1-P6 above).
THEN FIX every open defect from the last skeptic:
 - HIGH: ৱ written as U+009E (ban7, ban456): Word and RichEdit draw nothing; and FileConversion.WriteTxt (Encoding 1252) turns U+009E into '?' silently. Apply P4 and P6 (the .txt fix is in src/FontSoftware.App/Services/FileConversion.cs and applies to all scripts; add a test).
 - MEDIUM: ব্ঢ (U+0081), ব্ড (U+008D, Bangla families), চ্ঞ (U+0090) written as C1 code points Word draws as nothing: apply P4.
 - MEDIUM regression: the composer's own ra + ra-phala bytes read back with Latin ø (41 F4 A1 B9 F8 = ক্ৰ্র reads ক্রø; B9 F8 reads রø; B9 E7 A1 F8 reads রুø; 62 composer spellings per family) because the 127 rra-no-ra-twin rows were dropped; fix the reverse and test it with the composer's spellings, not only the fixture.
 - LOW: those 127 strings are written without a test: ক্ৰ্র / ক্র্র become 41 F6 F6 A1 (two ra-phala glyphs on one spot, one ra lost visually); write what the composer writes (41 F4 A1 B9 F8) or another spelling that draws both.
 - LOW: control codes 09-0D are kept as whitespace on purpose (keep that).
 - LOW: pass-through characters draw letters (/ = ; + * [ and ASCII " ' draw ব-base, থ, ৎ, ঔ, ও, i-sign, অ, ঐ): apply P2/P3 (curly quotes already map to 91/92; ASCII quotes can map there too if that is the look-alike).
` : `TASK (round ${round}): the skeptic found these defects in round ${round - 1}. Fix every HIGH and MEDIUM one, and the LOW ones that are cheap and safe; for each one you decide not to fix, say why with evidence.
DEFECTS: ${JSON.stringify(defects)}
PREVIOUS FIX REPORT: ${JSON.stringify(prev)}`}
Rebuild the tables with the BanTruth generator (the generator is the only writer of the ten tables and the Bangla encoding-limits keys), measure everything (truth per family, 20,000 words on two seeds, reverse on the fixture AND on every composer spelling in ${PREV}\\scratchpad\\shree7\\ban7-groundtruth.json, word-final hasanta, the BMP sweep, cmaps against every installed face, Word visibility of every code point written), run the Bangla tests while iterating, then the WHOLE suite once and tools/ConfigCheck at the end. Do not change the Gujarati, Devanagari or Sanskrit tables. Report before -> after in hex for every item.`

const banglaCheckPrompt = (round, fix, word) => `${COMMON}
${NO_COMPOSER}
${POLICY}
YOUR DIRECTORY: ${R3}\\bangla-check${round}. You are the independent SKEPTIC. Do not edit the main checkout: copy what you need (src, tests, tools, assets, FontSoftware.sln) to your directory and build there, one build at a time.
THE FIX AGENT REPORTS: ${JSON.stringify(fix)}
WORD-VISIBILITY MAP: ${word ? JSON.stringify(word) : 'not available'}
Re-measure everything yourself with the real FontConverter (and the TypingEngine for a Bangla and an Assamese layout in a 7 and a 4/5/6 profile): regenerate the ten tables from tools/BanTruth into a scratch folder and compare byte for byte with the shipped ones; truth per family (25,741 composer rows in ${PREV}\\scratchpad\\shree7\\ban7-groundtruth.json, every row composer-preferred, composer-accepted or a justified decision); 20,000 words on two NEW seeds; reverse on every composer spelling (no Latin or control characters may come out); the claimed fixes one by one with the exact inputs; every code point written is in every installed face's cmap and is drawn by Word and RichEdit (do your own Word check on a sample of the alternatives the fix chose: a new hidden instance, read-only, PDF export; quit only yours); the .txt writer; that languages.json, encoding-limits.json, RegisterFonts and the csproj kept main's other changes (compare with ${SNAP}); the whole suite once; ConfigCheck. Hunt for regressions in the Gujarati/Devanagari/Sanskrit numbers too (their truth tests). Report defects with severity; clean = no HIGH or MEDIUM left.`

const gujFixPrompt = (round, defects, prev, audit, word) => `${COMMON}
${NO_COMPOSER}
${POLICY}
YOUR DIRECTORY: ${R3}\\guj-fix${round} (scratch). You edit and build the MAIN checkout ${REPO}; you are the only agent editing or building there during your run. The Bangla/Assamese work was merged into main just before you: do not break it (its tests must stay green).
THE GUJARATI AUDIT (bugs, saved composer answers and harnesses in ${R3}\\guj-audit): ${audit ? JSON.stringify(audit) : 'NOT AVAILABLE'}
WORD-VISIBILITY MAP: ${word ? JSON.stringify(word) : 'not available'}
${round === 1 ? `TASK (round 1): fix every bug in the audit, HIGH first, then MEDIUM, then LOW, at the narrowest responsible layer: tables through their generators (tools/Guj7Truth for guj7/guj456; the generators are the only writers of those tables), layouts through the documented keyboard tools or careful JSON edits with tests, engine/app code in src with tests. Apply P1-P6 to the Gujarati tables AND to the Devanagari/Sanskrit tables (tools/ShreeDevTruth: dev7, dev456 and san7 write × ÷ – — ऄ ऎ ऒ ॆ at C1 code points U+0081 U+008F U+008D U+0090; fix only the P2-P6 policy items there, not their syllables). Every fix gets a test that fails before and passes after, built from the audit's exact repro and the composer's saved answers (not from our own output). For a bug you conclude is not a bug, give the evidence.` : `TASK (round ${round}): the skeptic found these defects in round ${round - 1}. Fix every HIGH and MEDIUM one and the cheap safe LOW ones; for each you leave, give the evidence.
DEFECTS: ${JSON.stringify(defects)}
PREVIOUS FIX REPORT: ${JSON.stringify(prev)}`}
Measure at the end: guj7truth build (tables regenerate byte-identical from the generator), truth 17,335/17,335 for guj7 and guj456, 20,000 words on two seeds, reverse, the audit's L1/L2 harness re-run against the saved composer answers (report the before/after mismatch counts per lens), Devanagari/Sanskrit verify, ShreeNonIndicTests and ShreeFontCmapTests, the whole suite once, ConfigCheck. Report before -> after in hex for every item.`

const gujCheckPrompt = (round, fix, audit, word) => `${COMMON}
${NO_COMPOSER}
${POLICY}
YOUR DIRECTORY: ${R3}\\guj-check${round}. You are the independent SKEPTIC. Do not edit the main checkout: copy src, tests, tools, assets and FontSoftware.sln to your directory and build there, one build at a time.
THE AUDIT: ${audit ? JSON.stringify(audit) : 'not available'}
THE FIX AGENT REPORTS: ${JSON.stringify(fix)}
WORD-VISIBILITY MAP: ${word ? JSON.stringify(word) : 'not available'}
Re-measure everything yourself: regenerate the guj7/guj456 (and dev7/dev456/san7) tables from their generators into a scratch folder and compare byte for byte with the shipped ones; truth (17,335 Gujarati, 27,175 Devanagari, 25,468 Sanskrit rows) with the real FontConverter; 20,000 words on two NEW seeds; reverse; re-run the audit's L1 per-key and L2 typing harnesses (in ${R3}\\guj-audit) against the saved composer answers and report the remaining mismatch counts per lens; check each audit bug with its exact repro; every code point written is in every installed face's cmap and is drawn by Word (your own Word check on a sample: new hidden instance, read-only docx, PDF export; quit only yours); the .txt/.rtf/.docx paths; the Bangla/Assamese numbers did not regress; the whole suite once; ConfigCheck. Look hard for regressions and for fixes that only make a test pass. Report defects with severity; clean = no HIGH or MEDIUM left.`

const SCRIPTS = [
  { key: 'pun', name: 'Punjabi (Gurmukhi)', dll: 'Pun', faces: 'SHREE-PUN7-0951/0952 (WPUN7001/2.TTF) and SHREE-PUN-0950/0951/0952/0955/0956 (S09xx0F2.TTF)', table: 'shree-pun7', notes: 'Gurmukhi specifics: addak, tippi vs bindi, subjoined ra/ha/va/ya (pairin), half ya, nukta letters, i-sign before its consonant, udaat/yakash if present. The last round found 7 and 4/5/6 glyphs identical except ₹ at 0x3C (7 only). Shree\'s Converter returns nothing for Punjabi "Shree-Lipi 7.0" (memory textsoft-coverage-status), but the composers are a different component: check they answer.' },
  { key: 'ori', name: 'Odia', dll: 'Ori', faces: 'SHREE-ORI7-* (incl. WORI7001/2) and SHREE-ORI-* (0600-0604)', table: 'shree-ori7', notes: 'Odia specifics: pre-base e/ai, split o/au, reph, ya-phala, ra-phala, many conjunct ligatures, ୱ ୟ, ୰ and ଽ (the shipped table writes them at C1 code points U+008F/U+0090). 7 and 4/5/6 identical except ₹ at 0x3C in WORI7001/2.' },
  { key: 'tel', name: 'Telugu', dll: 'Tel', faces: 'SHREE-TEL7-Apoorva/Asmitha (M/N...890.TTF), Shree-Tel7-001/002, SHREE-TEL-Apoorva/Asmitha (...0F0.TTF); some faces are split over two files (M and N): check which codes live in which', table: 'shree-tel7', notes: 'Telugu specifics: consonant-specific vowel-sign shapes (gunintham), vattu (subjoined) forms below and after, reph (ర్ as a vattu/ra-half), many ligatures; the shipped table has 18,235 pairs from an older pipeline. 7 and 4/5/6 glyphs identical per the last round.' },
  { key: 'kan', name: 'Kannada', dll: 'Kan', faces: 'SHREE-KAN7-* and SHREE-KAN-* (0850-0855, 0899)', table: 'shree-kan7', notes: 'Kannada specifics: consonant-specific vowel signs, ottakshara (subjoined) forms, arkavattu reph written after its syllable, ligatures; the reph comes back on a carrier ಕ in composer answers (memory). 7 and 4/5/6 identical per the last round.' },
  { key: 'tam', name: 'Tamil', dll: 'Tam', faces: 'SHREE-TAM7-* and SHREE-TAM-* (0800-0803, 1316, 3883)', table: 'shree-tam7', notes: 'Tamil specifics: pre-base e/ee/ai, split o/oo/au, the unique consonant+i/ii/u/uu glyphs, pulli, ஸ்ரீ, Grantha letters, the Tamil e/ai signs that once came back as a bare "." (memory). 7 and 4/5/6 differ only by redrawn ரி ரீ ர் (same characters).' },
  { key: 'mal', name: 'Malayalam', dll: 'Mal', faces: 'SHREE-MAL7-* only (no 4/5/6 face installed)', table: 'shree-mal7', notes: 'Malayalam specifics: pre-base e/ee/ai, split o/oo/au, chillu letters (atomic and with ZWJ), reph (dot reph vs ര് ), traditional conjunct ligatures, u/uu forms per consonant, ya/ra/va/la signs. Only 7-family faces are installed.' },
]

const capturePrompt = (s) => `${COMMON}
${COMPOSER_OWNER}
YOUR DIRECTORY: ${R3}\\cap-${s.key} (create it; keep everything resumable: write the probe set, every raw capture and progress to files so a retry can continue where you stopped). A pristine snapshot of the repo is at ${SNAP}: copy it to your directory if you need to build anything from the repo, and build only there. Do not edit the main checkout.

TASK: capture Shree's own ground truth for ${s.name} in the Shree-Lipi 7 family, as the last round did for Gujarati, Devanagari and Bangla, and measure TextSoft's current tables (${s.table}) against it. Composers: S8${s.dll}U64.dll (Unicode) and S8${s.dll}764.dll (Shree-Lipi 7, raw bytes). Faces: ${s.faces}. ${s.notes}
1. Learn the script's internal code set: what each code means in the U composer and what bytes the 7 composer writes for it (the vendor keyboard files C:\\Shree-Lipi nxt\\*.${s.key === 'pun' ? 'PUN' : s.key === 'ori' ? 'ORI' : s.key === 'tel' ? 'TEL' : s.key === 'kan' ? 'KAN' : s.key === 'tam' ? 'TAM' : 'MAL'} are 406-byte code tables; the Tutor answers name each code). Follow C:\\Users\\harde\\AppData\\Local\\Temp\\tsk-ban (explore*.js, gen.js, build.js, probe exe, groundtruth.js) and adapt it.
2. Generate probe sequences (each at most four codes per conjunct slot; load probe keyboards with LoadKbdOverlay using the full path with extension, ANSI) covering: every consonant and independent vowel alone; every consonant with every vowel sign, with anusvara/candrabindu/visarga after a sign, with the virama/half/link forms; every two-consonant cluster C1+virama+C2 for all consonant pairs, alone and with each vowel sign; the subjoined/ra/ya/va forms; the reph in every typing order; nukta letters; the script's special ligatures; the frequent three-consonant clusters (take them from the vendor keyboards' conjunct slots, the CHM charts in C:\\Shree-Lipi nxt\\data and common words); digits, punctuation and the symbol layer. Run every probe set TWICE and compare (stability); record quirks (carriers, placeholders 'Lk'/'Hf', answers given to many keys).
3. Also capture every vendor keyboard of this script key by key on all layers (layer 0 normal/shift, layer 1 Ctrl+Space, and the Ctrl+Shift+1 symbol layer) in BOTH families (U and 7) and save it, so a later per-key audit needs no composer.
4. Write ${R3}\\cap-${s.key}\\${s.key}7-groundtruth.json in the same shape as ${PREV}\\scratchpad\\shree7\\ban7-groundtruth.json (rows of {unicode, bytes, source, kind} plus 'truth' {preferred, accepted, alternatives} per Unicode string and 'counts'), and a patterns summary.
5. Family check: read the (3,1) cmaps of every installed 7 and 4/5/6 face of this script and compare glyphs code by code (outline or rendering by glyph index), to confirm whether one table can serve both families or which codes differ.
6. Measure the CURRENT shipped tables assets/config/mappings/unicode-to-${s.table}.json and ${s.table}-to-unicode.json with the real FontConverter (built from your snapshot copy) against your truth: forward exact %, forward accepted %, reverse exact/same-text %, unmapped counts, and the main failure categories with examples (Unicode, ours hex, Shree hex).
Restore SLNXT's keyboard at the end. Report numbers, files and anything that blocks a perfect table.${s.key === 'pun' ? '' : `
REUSE: the Punjabi capture of this round is finished in ${R3}\\cap-pun (probe2 C# tool that also dumps/restores SLNXT's shared memory, gen*.js, build.js, collectv.js, groundtruth.js, measure/ harness, shmdump/): start from it rather than from scratch. If your own directory already holds files from an earlier attempt (an internet outage stopped the first try), continue from them.`}`

// ---------------------------------------------------------------- run

let resolveAudit
const auditDone = new Promise((r) => { resolveAudit = r })

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

async function laneA() {
  phase('Gujarati audit')
  let audit = null
  try {
    audit = await agent(AUDIT_PROMPT, { label: 'guj:audit', phase: 'Gujarati audit', schema: AUDIT_SCHEMA })
  } finally {
    resolveAudit(audit)
  }
  if (audit) log(`Gujarati audit: ${audit.bugs.length} bugs (${audit.bugs.filter((b) => b.severity === 'HIGH').length} HIGH)`)
  const captures = {}
  for (const s of SCRIPTS) {
    const c = await agent(capturePrompt(s), { label: `capture:${s.key}`, phase: 'Capture', schema: CAPTURE_SCHEMA })
    captures[s.key] = c
    log(`capture ${s.key}: ${c ? c.truthRows + ' truth rows' : 'FAILED'}`)
  }
  return { audit, captures }
}

async function laneB() {
  const word = await agent(WORD_PROMPT, { label: 'word:map', phase: 'Word map', schema: WORD_SCHEMA })
  log(word ? 'Word-visibility map done' : 'Word-visibility map FAILED; fix agents will check Word themselves')
  const bangla = await fixLoop('bangla', 'Bangla',
    (r, d, p) => banglaFixPrompt(r, d, p, word),
    (r, f) => banglaCheckPrompt(r, f, word), 2)
  const audit = await auditDone
  let gujarati = []
  if (audit) {
    gujarati = await fixLoop('gujarati', 'Gujarati fix',
      (r, d, p) => gujFixPrompt(r, d, p, audit, word),
      (r, f) => gujCheckPrompt(r, f, audit, word), 2)
  } else {
    log('Gujarati audit missing: Gujarati fix skipped')
  }
  return { word, bangla, gujarati }
}

const [a, b] = await parallel([() => laneA(), () => laneB()])
return { laneA: a, laneB: b }

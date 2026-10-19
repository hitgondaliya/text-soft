const fs = require("fs");
const path = require("path");
const root = __dirname;
const p = path.join(root, "summary.json");
const S = JSON.parse(fs.readFileSync(p, "utf8"));
S.measured = {
  families: 31, faces: 143, testLines: 6241, mappedCodePointsTested: 6147, unmappedTested: 94,
  notTested: "C0 controls in the SHREE-ASS and SHREE-BAN cmaps (U+0008-U+000F, U+001A-U+001F; 14 each): XML 1.0 forbids them in .docx and WM_CHAR treats them as keys. No shipped forward table writes any C0 control.",
  word: "Microsoft Word 16.0.18429 (free edition) via COM, a NEW hidden instance per document (Documents.Open read-only, 3 arguments), ExportAsFixedFormat to PDF, Range.EnhMetaFileBits per paragraph; every instance quit, never a user document. Lines identified by their own blue label in the PDF content stream and in each EMF.",
  pdfMethod: "PDF content stream parsed (font, codes, embedded subset glyph outline compared with the installed font's outline) + page rendered by Windows.Data.Pdf at 225 dpi, black ink compared (1px-tolerant F1) with the glyph drawn by GDI ExtTextOut ETO_GLYPH_INDEX at 75 px/em; F1 is bimodal (5922 inked lines >=0.91, 56 <=0.72, none between).",
  emfMethod: "EMR_EXTTEXTOUTW records of Word's EMF picture (font face, code) + the EMF played back with GDI PlayEnhMetaFile (black ink count).",
  richedit: "msftedit.dll RICHEDIT50W in a hidden WS_POPUP window (never shown or focused), langOptions 0x182 (IMF_AUTOFONT|IMF_DUALFONT|0x100). typed = EM_SETCHARFORMAT(face, charset 0) at the caret then WM_CHAR; rtf = RichTextClipboard.BuildRtf bytes via EM_SETTEXTEX(ST_SELECTION). EM_GETCHARFORMAT per character, EM_FORMATRANGE into a 96-dpi DIB, ink vs GDI glyph-index reference (ClearType edges counted).",
};
S.findings = [
  "Every face of every family has the same (3,1) cmap (code point -> glyph id). No two code points share a glyph id in any font; identical outlines exist (sameOutlineAs in fam/<family>.json).",
  "Word and Word-from-RTF agree: the product's RTF run (\\fnil, \\uN?) opened by Word gives the same PDF verdict as the .docx for 658/658 lines (SHREE-GUJ7, SHREE-BAN7, SHREE-TEL7) and the same EMF font+code for the 577 labelled pictures.",
  "C1 code points U+0081 U+008D U+008F U+0090 U+009D U+009E: Word keeps the Shree font and the code (EMF), but GDI draws NO ink for them (ExtTextOutW by character: 0 px; the same glyph by index: 27-379 px); Word's PDF export writes them as WinAnsi codes, so PDF viewers draw the font's U+2022 glyph (81 8D 8F 90 9D) or the .notdef box (9E = zcaron, not in the fonts); RichEdit keeps the advance and draws nothing. No other code point reaches these glyphs. Tables writing them: guj7/guj456 U+0081 (– and —), U+008D (÷), U+008F (૰); dev7/dev456 U+0081 (×), U+008D (– —), U+008F (÷), U+0090 (blank glyph, 5 pairs; PDF shows the U+2022 glyph); main ban7 U+0081 U+008D U+0090 (13 pairs each; U+008D is not even in the SHREE-ASS/ASS7/Ass7-00x cmaps); worktree ban7/ban456 U+0081 U+008D U+0090 U+009E, ass7/ass456/ass7w U+0081 U+0090; ori7 U+008F (୰) U+0090 (ଽ).",
  "RichEdit (typed and RTF alike) sets charset 238 on U+0120-U+012F U+0152 U+0153 U+0160 U+0161 U+0178 U+0192; GDI then binds Arial and the Latin letter is drawn (e.g. U+0161 -> š). Word draws all of them correctly. Heavy table use: main ban7 U+0161 (প) 886 pairs, U+0192 (দ) 848; tel7 U+0153 868, U+0192 504, U+0160 448, U+0178 396, U+0152 32; kan7 U+0161 54, U+0178 41, U+0153 35. \\fcharset0 in the RTF font table changes nothing; \\fcharset2 turns them into C1 bytes and hides them (rtf-variants.json).",
  "RichEdit moves U+2219 to Cambria Math (no table writes it; U+00B7 has the identical outline and is drawn everywhere).",
  "U+00AD: blank glyph in most S fonts; Word's PDF draws the font's '-' glyph there (WinAnsi 0xAD = hyphen). Where U+00AD has ink (TAM, TAM7, Tam7-00x, MAL7, Mal7-00x, Ass7-00x, Ban7-00x) Word's GDI picture draws it, Word's PDF draws U+002D's glyph (blank in TAM/MAL -> nothing) and RichEdit hides it (0 advance). Not in the KAN/KAN7/Kan7-00x/SAN7 cmaps -> Word uses Cambria. No table writes U+00AD.",
  "U+00A0 is written by Word as U+0020 (both blank in every font). U+017E and U+017D fall back to Cambria and U+20AC to Times New Roman in Word (extra-ban/extra-guj7 test); no shipped table writes them.",
  "SHREE-DEV U+0219-U+021C have malformed glyf entries (bbox 20000,20000,-20000,-20000): nothing is drawn by anyone; no table writes them.",
  "Name collision: SHREE-ORI7-0603/0604 are both S0603892/S0604892.TTF and WORI7001/7002.TTF; GDI binds the name to WORI7001.TTF (53,248 bytes, head checksumAdjustment 05AFB4F1), whose cmap adds U+003C.",
];
S.files = {
  perFamily: "fam/<family>.json (codepoints[]: cp, gid, word, richedit, sameGlyphAs, sameOutlineAs, draws, fails, tables, alternatives)",
  rawEvidence: "fam/<family>.word.json (PDF), .emf.json (Word EMF records), .richedit.json, emfplay/<family>/emfplay.json, crops/<family>/<cp>.word.png|.re-typed.png|.re-rtf.png, fam/<family>.word.pdf, emf/<family>/NNNN.emf",
  digests: "final-invisible.txt, final-ascii.txt, rtf-variants.json",
  tool: "tool/ (wordvis.exe: scan docx refs pdf emf emfplay richedit report gdichar rtfdoc docxcustom), wordpdf/exportall.ps1 + export1.ps1, emfall.ps1 + emf1.ps1",
};
fs.writeFileSync(p, JSON.stringify(S, null, 2));
console.log("ok");

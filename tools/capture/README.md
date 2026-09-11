# Ground-truth capture — build VERIFIED converter tables from Shree-Lipi

This is the reliable way to add a legacy font-layout (encoding) to the converter:
capture one sample through your licensed Shree-Lipi, and the build tool derives an
exact byte↔Unicode table (it even auto-detects the pre-base i-matra reorder).

## Files

- `dev.unicode.txt` — a Devanagari sample: **one unit per line** (every consonant,
  vowel, matra, half-form, conjunct, reph example, digit, punctuation, plus whole
  words for validation). One file per script (dev first; more added as needed).

## Steps (per encoding you want, e.g. DevLys 040, Chanakya, Bhasha Bharati…)

1. Open `dev.unicode.txt` in Shree-Lipi (it is Unicode / Mangal).
2. Convert **Unicode → the target legacy encoding** (e.g. "Walkman Chanakya").
   Keep every line — do **not** add or remove lines; the line count must stay the same.
3. Save the converted result as a plain-text file, e.g. `chanakya.legacy.txt`
   (UTF-8, so the legacy bytes round-trip as characters).
4. Send me that file (or drop it in this folder) and tell me the encoding name.

## What I run

```bash
dotnet run --project tools/CaptureBuild -- \
  --unicode tools/capture/dev.unicode.txt \
  --legacy <your chanakya.legacy.txt> \
  --source chanakya --target mangal --script dev \
  --out assets/config/mappings/chanakya-to-unicode.json
```

It aligns the two files line-by-line, extracts atomic legacy→Unicode pairs
(decomposing matras, detecting whether the i-matra is stored pre-base), writes the
forward **and** reverse tables, and validates against the whole-word lines
(reports any mismatch). Then I register the font in `languages.json` and round-trip
test with `tools/ConvTest`.

## Notes

- Reverse direction (Unicode → legacy) is generated automatically.
- If a few words mismatch, it usually means a rare conjunct ligature isn't in the
  sample — tell me and I add that unit, you re-capture (or just send a second small
  sample with those words).
- Pipeline self-tested end-to-end against the existing Shree-DevX table: 24/25 words,
  i-matra pre-base correctly auto-detected.

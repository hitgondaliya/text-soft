# Shree-Lipi 7 and 4/5/6, round 3: unfinished work (stopped 2026-10-19)

This branch carries the in-progress work so it can be finished on another PC.
`master` holds everything finished: Gujarati, Devanagari, Sanskrit and
Bangla/Assamese, all merged and checked clean (961 tests, ConfigCheck 0 errors).

## State per script

| Script | Ground truth | Tables | Where |
|---|---|---|---|
| Punjabi | `cap-pun/pun7-groundtruth.json` | done, independent check clean (round 2) | `tbl-pun/changed/` |
| Odia | `cap-ori/ori7-groundtruth.json` | round 2 built; its check was cut off | `tbl-ori/changed/` |
| Telugu | `cap-tel/tel7-groundtruth.json` | round-2 build cut off | `tbl-tel/changed/` |
| Kannada | `cap-kan/kan7-groundtruth.json` | not started | - |
| Tamil | `cap-tam/tam7-groundtruth.json` | not started | - |
| Malayalam | `cap-mal/mal7-groundtruth.json` | not started | - |

The tables shipped in `master` for these six scripts are the old ones. Measured
against Shree's composer they match: Odia 32%, Tamil 55%, Punjabi 91% of syllables.

## What is where

- `tbl-<key>/changed/`: every file the script's copy added or changed. Each copy
  was based on the repo as of 2026-10-18 08:30 (commit b96047d plus the uncommitted
  work then). `tbl-<key>/MERGE.md` lists each file and the exact edits to shared
  files. `round1/` and `round2/` hold the agents' notes and audit results.
- `cap-<key>/`: Shree composer captures. They hold the ground truth (`*7-groundtruth.json`),
  the per-key captures of every vendor keyboard (`*-vendor-keys.json`), the capture
  scripts (`gen*.js`, `build.js`, `groundtruth.js`) and `probe2/`, the C# tool that
  loads the composers and saves and restores SLNXT's shared memory.
- `guj-audit/`: the key-by-key Gujarati audit and its harness, with the composer's answers.
- `word/`: the Word-visibility map, saying which code points of each Shree font
  Word and RichEdit draw.
- `shree7-prev/`: the ground truth for Gujarati, Devanagari, Sanskrit and Bangla
  from the round before.
- `result-*.json`: the reports of the audit, the Word map and the captures.
- `workflows/`: the Claude Code workflow scripts used.
  `six-scripts-tables-*.js` builds the remaining scripts and then merges them into main.
- `claude-memory/`: Claude's project notes. Copy them into the new PC's
  `%USERPROFILE%\.claude\projects\<project>\memory\` so the history is known.

## To finish

1. Build the tables for each script the way `tools/BanTruth` and `tools/Guj7Truth` do.
   Each script gets its own `tools/<Scr>Truth` generator, from its ground truth;
   see `tbl-pun/changed/tools/PunTruth` for a finished one. Bring the files into
   main with a 3-way merge, using the 2026-10-18 snapshot as the base.
2. Re-run every generator, then the whole test suite, then `tools/ConfigCheck`.
3. Run a live test with `tools/TypeProbe`, then `installer/build-release.ps1`.

Capturing again needs Shree-Lipi nxt installed at `C:\Shree-Lipi nxt\`. The captures
are already saved here, so the tables can be built without it.

## Not in git (keep a separate backup)

- The production licence **private key** and the server secrets. They live only in
  `Desktop\textsoft-admin-deploy.zip` (its `.env`) and on the VPS. Never commit them.
- Client font packs (`Gujarati font/`, `hindi/`, `*.rar`): commercial fonts.
- Installers: rebuild them with `installer/build-release.ps1`.

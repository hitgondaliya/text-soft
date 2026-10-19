# TextSoft backup (not code)

Files kept here so they survive a change of laptop. The code is on `master`.

- `fonts-by-language/<Language>/<Font layout>/`: every font on the old PC except
  Microsoft's own Windows fonts, each once (deduplicated by content), sorted the way
  TextSoft assigns fonts: `Shree-Lipi 7.0`, `Shree-Lipi 4.0-5.0-6.0`, `ShreeLipi-Ex`,
  Bhasha Bharati, ISFOC, Akruti, Terafont, Unicode. `English (Shree-Lipi)` and `Symbols`
  hold Shree's English and symbol fonts; `Other` holds Latin, Adobe, Office and web fonts.
  `FONTS-INDEX.csv` lists each file with its family name and where it came from.
  `_archives/` holds the client's original `.rar` font packs.
  These are commercial fonts: keep this repository private and never ship them.
- `installers/`: the last released installer (2026-10-18, Gujarati fixes, live test 13/13).
- `docs/`: the capability and website PDFs and the test-font zips sent to the client.

To install them on a new PC: select the font files in a folder, right-click, and choose "Install for all users".

NOT here: the licence-server secrets (`TextSoft-Backup-Secrets.zip` on the old Desktop,
with the production licence private key). Keep that on a pen drive or in a password manager.

# TextSoft backup (not code)

Files kept here so they survive a change of laptop. The code is on `master`.

- `fonts/`: the client's font packs used for analysis (`Gujarati font`, `hindi`,
  their `.rar` files, and the `D:\fonts` folder). They are commercial fonts: keep this
  repository private and never ship them.
- `installers/`: the last released installer (2026-10-18, Gujarati fixes, live test 13/13).
  Rebuild any other version with `installer/build-release.ps1` on `master`.
- `docs/`: the capability and website PDFs and the test-font zips sent to the client.

NOT here: the licence-server secrets (`textsoft-admin-deploy*.zip` with the production
licence private key, and `server/.env.local`). Keep those on a pen drive or in a
password manager.

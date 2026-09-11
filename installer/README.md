# Installer (FRD Non-Functional Requirements)

Packages the app into a Windows setup `.exe` with Start Menu + desktop shortcuts,
optional auto-start, and clean uninstall. Built with [Inno Setup 6](https://jrsoftware.org/isdl.php).

## Prerequisites (on a Windows machine)
- .NET 8 SDK
- Inno Setup 6 (`ISCC.exe`)

## Build
```powershell
powershell -ExecutionPolicy Bypass -File build-installer.ps1
```
This publishes the app self-contained (so end users need **no** .NET install) and
compiles the installer into `installer\output\MultilingualTyping-Setup-1.0.0.exe`.

To build the installer only (after publishing yourself):
```powershell
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" setup.iss
```

## Release build (obfuscated) — for shipping licensed builds
`build-installer.ps1` makes a quick single-file build with **no** obfuscation. For a
real release use `build-release.ps1`, which publishes as a folder and runs Obfuscar
over `FontSoftware.Licensing.dll` (the assembly holding the Ed25519 verification and
the expiry / clock-rollback / grace logic) before packaging — see `obfuscar.xml`.

```powershell
# 1) one-time tool
dotnet tool install --global Obfuscar.GlobalTool
# 2) stamp production license config (public key + HTTPS API URL)
powershell -File ..\scripts\set-prod-config.ps1 -PublicKey "<b64>" -ApiUrl "https://api.yourdomain.com"
# 3) build the obfuscated installer
powershell -ExecutionPolicy Bypass -File build-release.ps1
```
`set-prod-config.ps1` swaps the dev Ed25519 public key + `localhost` API URL for your
production values (get the key from `server`: `npm run genkeys`). Obfuscation keeps
the library's public API readable (so the WPF app still binds) and renames the private
internals + encrypts string literals. **Test-launch the installed app after building**
— the release script does not UI-test the obfuscated build.

## What it does
- Installs to `Program Files\Multilingual Typing` (admin) — bundles the app, the
  .NET runtime, and the `config\` assets.
- **Installs the bundled legacy fonts into Windows** (`SHREE-DEVX-0700` for Hindi,
  `SHREE-GUJX-1120` for Gujarati) so they render on a fresh PC with no manual step.
  Uses `onlyifdoesntexist` (won't overwrite a user copy) and `uninsneveruninstall`
  (left in place on uninstall so existing documents still render). The app only
  lists fonts that are actually installed, so this is what makes them appear.
- Start Menu shortcut always; desktop shortcut and auto-start are optional tasks.
- Auto-start uses a per-user `Run` registry value, removed on uninstall.
- Uninstall removes the program folder. User settings and license under
  `%AppData%\MultilingualTyping` are intentionally left in place.

## Code signing (FRD NFR: signed installer, fewer AV false positives)
The client supplies the code-signing certificate. To sign:
1. In the Inno Setup IDE: **Tools ▸ Configure Sign Tools…**, add one named `signtool`, e.g.
   ```
   signtool sign /f "$p" /fd sha256 /tr http://timestamp.digicert.com /td sha256 $f
   ```
2. Uncomment `SignTool=signtool` and `SignedUninstaller=yes` in `setup.iss`.
3. Also sign `MultilingualTyping.exe` (the app itself) before packaging, or via a
   post-publish step, so the running app is trusted too.

## Placeholders to update
| In `setup.iss` | Replace |
|---|---|
| `AppUrl` | Real product URL |
| `SetupIconFile` | A branded `.ico` (currently commented out) |
| `AppVersion` | Bump per release |

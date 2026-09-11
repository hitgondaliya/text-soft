; Inno Setup script for TextSoft — Beyond Typing (FRD NFR: installer).
; Build with Inno Setup 6+ (ISCC.exe). See README.md in this folder.

#define AppName        "TextSoft"
#define AppTagline     "Beyond Typing"
#define AppVersion     "1.0.0"
#define AppPublisher   "Hit Gondaliya"
#define AppExe         "TextSoft.exe"
#define AppUrl         "https://example.com/"           ; TODO(client): real site URL
; Path to the framework-independent, self-contained publish output.
#define PublishDir     "..\src\FontSoftware.App\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
; A fixed AppId keeps upgrades/uninstall tied to the same product. Keep it stable.
AppId={{7E6D2C1A-4B9F-4E2D-9A31-1C7F0A2B5D80}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
OutputBaseFilename=TextSoft-Setup-{#AppVersion}
OutputDir=output
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; 64-bit only (FRD: Windows 10/11 64-bit).
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Installs to Program Files → needs admin. Set to "lowest" for a per-user install.
PrivilegesRequired=admin
SetupIconFile=..\src\FontSoftware.App\app.ico

; --- Code signing (FRD NFR: signed installer, reduce AV false positives) ---
; Provide the client certificate to Inno via a signing tool named "signtool":
;   Tools > Configure Sign Tools... > add:  signtool  ->  "C:\...\signtool.exe" sign /f "$q$p$q" ...
; then uncomment:
; SignTool=signtool
; SignedUninstaller=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"
Name: "autostart";   Description: "Start {#AppName} automatically when I sign in"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; Everything from the publish folder (exe, .NET runtime, and config\ assets).
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

; --- Bundled legacy fonts, installed into Windows so the app can render them ---
; These are the client fonts shipped with the product. Registering them here means
; SHREE-DEVX-0700 (Hindi) and SHREE-GUJX-1120 (Gujarati) are available system-wide
; on a fresh PC without the user installing anything by hand. The FontInstall name
; MUST match the font's internal family name exactly. onlyifdoesntexist avoids
; clobbering a copy the user already installed; uninsneveruninstall leaves the font
; in place on uninstall so documents that use it still render.
Source: "{#PublishDir}\config\fonts\S0700810.TTF"; DestDir: "{autofonts}"; \
    FontInstall: "SHREE-DEVX-0700"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#PublishDir}\config\fonts\S1120810.TTF"; DestDir: "{autofonts}"; \
    FontInstall: "SHREE-GUJX-1120"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{group}\{#AppName}";        Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Optional auto-start at login (per-user Run key; removed on uninstall).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean uninstall: remove anything the app writes next to itself. User settings and
; license live under %AppData%\TextSoft and are intentionally left in place.
Type: filesandordirs; Name: "{app}"

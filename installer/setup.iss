; Inno Setup script for TextSoft — Beyond Typing (FRD NFR: installer).
; Build with Inno Setup 6+ (ISCC.exe). See README.md in this folder.

#define AppName        "TextSoft"
#define AppTagline     "Beyond Typing"
#define AppVersion     "1.0.0"
#define AppPublisher   "Hit Gondaliya"
#define AppExe         "TextSoft.exe"
#define AppUrl         "https://example.com/"           ; TODO(client): real site URL
; Path to the framework-independent, self-contained publish output.
#ifndef PublishDir
#define PublishDir     "..\src\FontSoftware.App\bin\Release\net8.0-windows\win-x64\publish"
#endif
; Licence server. scripts\set-prod-config.ps1 stamps the production HTTPS URL here
; (it must match LicenseEndpoints.BaseUrl in the app).
#define ApiUrl         "https://admin.text-soft.com"

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

[InstallDelete]
; An upgrade overwrites every file it ships but cannot know about one it has
; stopped shipping: a keyboard layout we dropped would sit in the folder for ever.
; Clear the layouts and mappings first so what is installed is exactly what this
; build carries. The user's own keyboards live in %AppData%, not here.
Type: filesandordirs; Name: "{app}\config\layouts"
Type: filesandordirs; Name: "{app}\config\mappings"

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

[Dirs]
; The installer leaves the verified serial+key here for the app's first launch,
; and the app (running as the user) deletes the file once it has activated.
Name: "{commonappdata}\{#AppName}"; Permissions: users-modify

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
Type: files; Name: "{commonappdata}\{#AppName}\pending-activation.json"

; ============================================================================
; Licence page: serial number + key, verified with the server BEFORE anything is
; copied. The server refuses a key that is already bound to another PC (and logs
; the attempt), a key that does not belong to the serial, a revoked or expired
; key. A reinstall on the same PC is recognised by the MachineGuid hint.
; The app does the actual binding on first launch (it computes the fingerprint).
; ============================================================================
[Code]
var
  LicPage: TInputQueryWizardPage;
  VerifiedSerial, VerifiedKey: String;

function Trim2(const S: String): String;
begin
  Result := Uppercase(Trim(S));
  StringChangeEx(Result, ' ', '', True);
end;

function IsDigit(C: Char): Boolean;
begin
  Result := (C >= '0') and (C <= '9');
end;

function IsKeyChar(C: Char): Boolean;
begin
  Result := IsDigit(C) or ((C >= 'A') and (C <= 'Z'));
end;

{ TXS-2026-0001 }
function SerialLooksRight(const S: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  if Length(S) <> 13 then Exit;
  if Copy(S, 1, 4) <> 'TXS-' then Exit;
  if S[9] <> '-' then Exit;
  for I := 5 to 8 do if not IsDigit(S[I]) then Exit;
  for I := 10 to 13 do if not IsDigit(S[I]) then Exit;
  Result := True;
end;

{ TXSF-XXXX-XXXX-XXXX }
function KeyLooksRight(const S: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  if Length(S) <> 19 then Exit;
  if Copy(S, 1, 5) <> 'TXSF-' then Exit;
  for I := 6 to 19 do
  begin
    if (I = 10) or (I = 15) then
    begin
      if S[I] <> '-' then Exit;
    end
    else if not IsKeyChar(S[I]) then Exit;
  end;
  Result := True;
end;

{ Pull "key":"value" out of a flat JSON object; '' when absent. }
function JsonStr(const Body, Key: String): String;
var
  P, Q: Integer;
  Needle: String;
begin
  Result := '';
  Needle := '"' + Key + '":"';
  P := Pos(Needle, Body);
  if P = 0 then Exit;
  P := P + Length(Needle);
  Q := P;
  while (Q <= Length(Body)) and (Body[Q] <> '"') do Q := Q + 1;
  Result := Copy(Body, P, Q - P);
end;

function MachineGuid(): String;
begin
  if not RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\Cryptography', 'MachineGuid', Result) then
    Result := '';
end;

{ Ask the server. Returns '' when the pair is good for this PC, else a message. }
function PrecheckLicence(const Serial, Key: String): String;
var
  Http: Variant;
  Body, Resp, Err, Bound: String;
  Status: Integer;
begin
  Result := '';
  Body := '{"serial":"' + Serial + '","key":"' + Key + '","machineHint":"' + MachineGuid() +
          '","deviceName":"' + GetComputerNameString() + '","installerVersion":"{#AppVersion}"}';
  try
    Http := CreateOleObject('WinHttp.WinHttpRequest.5.1');
    Http.SetTimeouts(8000, 8000, 15000, 15000);
    Http.Open('POST', '{#ApiUrl}/api/precheck', False);
    Http.SetRequestHeader('Content-Type', 'application/json');
    Http.SetRequestHeader('User-Agent', 'TextSoft-Setup/{#AppVersion}');
    Http.Send(Body);
    Status := Http.Status;
    Resp := Http.ResponseText;
  except
    Result := 'Could not reach the licence server. Check your internet connection and try again.';
    Exit;
  end;

  if (Status = 200) and (Pos('"ok":true', Resp) > 0) then Exit;

  Err := JsonStr(Resp, 'error');
  Bound := JsonStr(Resp, 'boundDeviceName');
  if Err = 'already_bound' then
  begin
    if Bound <> '' then
      Result := 'This licence key is already activated on another computer ("' + Bound + '").' + #13#10#13#10 +
                'A key works on one PC only. To move it to this PC, contact support with your serial number.'
    else
      Result := 'This licence key is already activated on another computer.' + #13#10#13#10 +
                'A key works on one PC only. To move it to this PC, contact support with your serial number.';
  end
  else if Err = 'serial_mismatch' then
    Result := 'That key does not belong to serial number ' + Serial + '. Check both against your purchase confirmation.'
  else if Err = 'invalid' then
    Result := 'That licence key was not recognised. Check it and try again.'
  else if Err = 'revoked' then
    Result := 'This licence has been revoked. Please contact support.'
  else if Err = 'expired' then
    Result := 'This licence has expired. Please contact support to renew it.'
  else if Err = 'rate_limited' then
    Result := 'Too many attempts. Wait a few minutes and try again.'
  else if Status >= 500 then
    Result := 'The licence server is not available right now. Try again in a few minutes.'
  else
    Result := 'The licence could not be verified (' + IntToStr(Status) + ' ' + Err + '). Try again.';
end;

procedure InitializeWizard();
begin
  LicPage := CreateInputQueryPage(wpWelcome,
    'Licence', 'Enter your serial number and licence key',
    'Both are on your purchase confirmation. The key will be bound to this computer and cannot be used on another one.');
  LicPage.Add('Serial number (TXS-2026-0001):', False);
  LicPage.Add('Licence key (TXSF-XXXX-XXXX-XXXX):', False);
  { Silent / scripted installs can pass them on the command line. }
  LicPage.Values[0] := ExpandConstant('{param:SERIAL}');
  LicPage.Values[1] := ExpandConstant('{param:KEY}');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  S, K, Msg: String;
begin
  Result := True;
  if CurPageID <> LicPage.ID then Exit;

  S := Trim2(LicPage.Values[0]);
  K := Trim2(LicPage.Values[1]);
  if not SerialLooksRight(S) then
  begin
    MsgBox('The serial number looks like TXS-2026-0001.', mbError, MB_OK);
    Result := False; Exit;
  end;
  if not KeyLooksRight(K) then
  begin
    MsgBox('The licence key looks like TXSF-XXXX-XXXX-XXXX (letters and digits).', mbError, MB_OK);
    Result := False; Exit;
  end;

  Msg := PrecheckLicence(S, K);
  if Msg <> '' then
  begin
    MsgBox(Msg, mbError, MB_OK);
    Result := False; Exit;
  end;
  VerifiedSerial := S;
  VerifiedKey := K;
end;

{ Silent install: the page is never shown, so verify here instead. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  S, K: String;
begin
  Result := '';
  if not WizardSilent() then Exit;
  S := Trim2(ExpandConstant('{param:SERIAL}'));
  K := Trim2(ExpandConstant('{param:KEY}'));
  if (S = '') and (K = '') then Exit; { the app will ask on first launch }
  if (not SerialLooksRight(S)) or (not KeyLooksRight(K)) then
  begin
    Result := 'SERIAL / KEY parameters are not in the expected format.'; Exit;
  end;
  Result := PrecheckLicence(S, K);
  if Result = '' then
  begin
    VerifiedSerial := S;
    VerifiedKey := K;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Path: String;
begin
  if CurStep <> ssPostInstall then Exit;
  if VerifiedSerial = '' then Exit;
  Path := ExpandConstant('{commonappdata}\{#AppName}\pending-activation.json');
  ForceDirectories(ExtractFileDir(Path));
  SaveStringToFile(Path, '{"serial":"' + VerifiedSerial + '","key":"' + VerifiedKey + '"}', False);
end;


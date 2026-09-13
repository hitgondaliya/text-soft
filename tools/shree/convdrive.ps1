# convdrive.ps1 — drive Shree-Lipi's "Converter nxt" as the mapping oracle.
#
# Converter nxt converts text between ANY two font layouts Shree knows, clipboard
# in and clipboard out. That is what covers the third-party layouts (Bhasha
# Bharati, Akruti, ISFOC, Anu, Terafont) which have no Unicode twin font, and any
# script whose twin is missing. It is slower than the shape join, so use it only
# where the shape join cannot reach.
#
#   convdrive.ps1 -Proc SLNXT -Script BANGLA -From Unicode -To "Shree-Lipi 7.0" `
#                 -InFile units-ban.txt -OutFile out-ban7.txt
param(
  [string]$Proc = 'SLNXT',
  [string]$Script = 'DEVANAGARI',
  [string]$From = 'Unicode',
  [string]$To = 'ShreeLipi-Ex',
  [string]$InFile,
  [string]$OutFile,
  # How long to wait for the output box to fill. A layout Shree cannot produce
  # never fills it at all, so a sweep across candidate layouts spends the whole
  # budget on every miss — drop this to a few tens of seconds when the run is
  # only asking "does this layout exist", and leave it alone for a real capture.
  [int]$MaxWaitSeconds = 300
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms | Out-Null
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]

$root = $AE::RootElement
$app = $null
foreach ($k in $root.FindAll($TS::Children, [System.Windows.Automation.Condition]::TrueCondition)) {
  $pn = ''
  try { $pn = (Get-Process -Id $k.Current.ProcessId -ErrorAction SilentlyContinue).ProcessName } catch {}
  if ($pn -eq $Proc) { $app = $k }
}
if (-not $app) { Write-Output "$Proc not running"; exit 1 }

function ById($id) {
  $c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
  return $app.FindFirst($TS::Descendants, $c)
}
function SetCombo($id, $item) {
  $cb = ById $id
  if (-not $cb) { return "no combo $id" }
  try { $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 400 } catch {}
  $c = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $item)
  $it = $cb.FindFirst($TS::Descendants, $c)
  if (-not $it) { try { $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse() } catch {}; return "no item '$item'" }
  $it.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
  try { $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse() } catch {}
  return 'ok'
}
function Press($id) {
  $e = ById $id
  if (-not $e) { return "no $id" }
  if (-not $e.Current.IsEnabled) { return "$id disabled" }
  try { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); return 'ok' } catch {}
  try { $e.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); return 'ok' } catch {}
  return "$id not invokable"
}

# "Start Over" drops the app back to its home screen, so every run has to reopen
# the tile. Skipping this leaves the panel's controls absent and the script would
# read a stale clipboard instead of a conversion.
function OpenConverter {
  for ($t = 0; $t -lt 6; $t++) {
    if (ById 'cmbScript') { return $true }
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, 'Converter nxt')
    $lbl = $app.FindFirst($TS::Descendants, $c)
    if ($lbl) {
      $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
      $cur = $lbl
      for ($u = 0; $u -lt 6 -and $cur; $u++) {
        try { $cur.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); break } catch { $cur = $walker.GetParent($cur) }
      }
    }
    Start-Sleep -Seconds 2
  }
  return [bool](ById 'cmbScript')
}

$so = Press 'StartOver'
if ($so -eq 'ok') { Start-Sleep -Seconds 2 }
if (-not (OpenConverter)) { Write-Output 'FAILED: could not open Converter nxt'; exit 1 }
Start-Sleep -Milliseconds 800

Write-Output ('script       : ' + (SetCombo 'cmbScript' $Script))
Start-Sleep -Milliseconds 600
Write-Output ('input layout : ' + (SetCombo 'cmbInputLayout' $From))
Start-Sleep -Milliseconds 600
Write-Output ('output layout: ' + (SetCombo 'cmbOutputLayout' $To))
Start-Sleep -Milliseconds 600
Write-Output ('in=clipboard : ' + (Press 's_Clipboard'))
Write-Output ('out=clipboard: ' + (Press 'd_Clipboard'))
Start-Sleep -Milliseconds 400

# the clipboard can only be set from an STA thread
$text = [IO.File]::ReadAllText($InFile, [Text.Encoding]::UTF8)
$sta = [PowerShell]::Create()
$null = $sta.AddScript({ param($t) Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Clipboard]::SetText($t) }).AddArgument($text)
$sta.Runspace = [RunspaceFactory]::CreateRunspace()
$sta.Runspace.ApartmentState = 'STA'
$sta.Runspace.Open()
$null = $sta.Invoke()
$sta.Runspace.Close()
Start-Sleep -Milliseconds 600

$g = Press 'btnGetClipData'
Write-Output ('get data     : ' + $g)
if ($g -ne 'ok') { Write-Output 'FAILED: converter panel not ready'; exit 2 }
Start-Sleep -Seconds 2
$cv = Press 'BtnConvert'
Write-Output ('convert      : ' + $cv)
if ($cv -ne 'ok') { Write-Output 'FAILED: convert button not available'; exit 2 }

# A long unit list takes a while; poll the output box until it has as many lines
# as we sent rather than guessing a sleep and reading a half-finished result.
$wantLines = ($text -split "`r?`n" | Where-Object { $_.Length -gt 0 }).Count
for ($t = 0; $t -lt [int]($MaxWaitSeconds / 2); $t++) {
  Start-Sleep -Seconds 2
  $ob = ById 'rtbOPData'
  if (-not $ob) { continue }
  $cur = ''
  try { $cur = $ob.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern).DocumentRange.GetText(-1) } catch { continue }
  $got = ($cur -split "`r?`n" | Where-Object { $_.Length -gt 0 }).Count
  if ($got -ge $wantLines) { Write-Output ("converted    : $got/$wantLines lines after " + (($t + 1) * 2) + "s"); break }
}

$out = ById 'rtbOPData'
$result = ''
if ($out) {
  try { $result = $out.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern).DocumentRange.GetText(-1) } catch {}
}
if ($result.Trim().Length -eq 0) {
  $get = [PowerShell]::Create()
  $null = $get.AddScript({ Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Clipboard]::GetText() })
  $get.Runspace = [RunspaceFactory]::CreateRunspace()
  $get.Runspace.ApartmentState = 'STA'
  $get.Runspace.Open()
  $result = ($get.Invoke() | Out-String)
  $get.Runspace.Close()
  # the clipboard still holds the INPUT until the converter replaces it, and
  # writing that out would look like a perfect table while being a pure echo
  if ($result.TrimEnd() -eq $text.TrimEnd()) { Write-Output 'FAILED: output is the input echoed back'; exit 3 }
}

[IO.File]::WriteAllText($OutFile, $result, [Text.Encoding]::UTF8)
Write-Output ("wrote $OutFile  chars=" + $result.Length + " lines=" + (($result -split "`r?`n").Count))

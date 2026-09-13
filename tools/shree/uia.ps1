# uia.ps1 — inspect and drive a Shree-Lipi window through UI Automation.
#
# Shree-Lipi has no command line: Font Manager and Converter nxt are tiles inside
# SLNXT.exe / SLNXTPLUS.exe. Everything we take from Shree goes through this.
#
#   uia.ps1 -Proc SLNXT -Action tree   [-Depth 8]
#   uia.ps1 -Proc SLNXT -Action combos
#   uia.ps1 -Proc SLNXT -Action items  -Arg1 cmbScript
#   uia.ps1 -Proc SLNXT -Action set    -Arg1 cmbScript -Arg2 DEVANAGARI
#   uia.ps1 -Proc SLNXT -Action press  -Arg1 btnGetClipData
#   uia.ps1 -Proc SLNXT -Action tile   -Arg1 "Converter nxt"
param(
  [string]$Proc = 'SLNXT',
  [string]$Action = 'tree',
  [string]$Arg1 = '',
  [string]$Arg2 = '',
  [int]$Depth = 8
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes | Out-Null
Add-Type @"
using System;using System.Runtime.InteropServices;
public class DpiA { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }
"@ -ErrorAction SilentlyContinue
try { [DpiA]::SetProcessDPIAware() | Out-Null } catch {}

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]

$root = $AE::RootElement
$app = $null
foreach ($k in $root.FindAll($TS::Children, [System.Windows.Automation.Condition]::TrueCondition)) {
  $pn = ''
  try { $pn = (Get-Process -Id $k.Current.ProcessId -ErrorAction SilentlyContinue).ProcessName } catch {}
  if ($pn -eq $Proc) { $app = $k }
}
if (-not $app) { Write-Output "$Proc has no window"; exit 1 }

function ById($id) {
  $c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
  return $app.FindFirst($TS::Descendants, $c)
}
function ByName($n) {
  $c = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $n)
  return $app.FindFirst($TS::Descendants, $c)
}
function Expand($cb) { try { $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 400 } catch {} }
function Collapse($cb) { try { $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse() } catch {} }

function Dump($el, [int]$d, [string]$pad) {
  if ($d -gt $Depth) { return }
  $c = $el.Current
  $line = $pad + '[' + $c.ControlType.ProgrammaticName.Replace('ControlType.', '') + '] '
  if ($c.Name) { $line += 'name="' + $c.Name + '" ' }
  if ($c.AutomationId) { $line += 'id="' + $c.AutomationId + '" ' }
  if (-not $c.IsEnabled) { $line += '(disabled) ' }
  Write-Output $line
  foreach ($k in $el.FindAll($TS::Children, [System.Windows.Automation.Condition]::TrueCondition)) { Dump $k ($d + 1) ($pad + '  ') }
}

switch ($Action) {
  'tree' { Dump $app 0 '' }
  'combos' {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
    foreach ($e in $app.FindAll($TS::Descendants, $c)) {
      $sel = ''
      try { $sel = ($e.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern).Current.GetSelection() | ForEach-Object { $_.Current.Name }) -join ',' } catch {}
      Write-Output ('COMBO id=' + $e.Current.AutomationId + ' sel=[' + $sel + ']')
    }
  }
  'items' {
    $cb = ById $Arg1
    if (-not $cb) { Write-Output "no combo $Arg1"; exit 1 }
    Expand $cb
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    foreach ($it in $cb.FindAll($TS::Descendants, $c)) { Write-Output $it.Current.Name }
    Collapse $cb
  }
  'set' {
    $cb = ById $Arg1
    if (-not $cb) { Write-Output "no combo $Arg1"; exit 1 }
    Expand $cb
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $Arg2)
    $it = $cb.FindFirst($TS::Descendants, $c)
    if (-not $it) { Collapse $cb; Write-Output "no item '$Arg2'"; exit 1 }
    $it.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Collapse $cb
    Write-Output "set $Arg1 = $Arg2"
  }
  'press' {
    $e = ById $Arg1; if (-not $e) { $e = ByName $Arg1 }
    if (-not $e) { Write-Output "no element $Arg1"; exit 1 }
    if (-not $e.Current.IsEnabled) { Write-Output "$Arg1 disabled"; exit 1 }
    try { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Output "invoked $Arg1"; exit 0 } catch {}
    try { $e.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); Write-Output "toggled $Arg1"; exit 0 } catch {}
    try { $e.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Write-Output "selected $Arg1"; exit 0 } catch {}
    Write-Output "$Arg1 not actionable"; exit 1
  }
  'tile' {
    # a tile is a Button whose caption sits in a Text child
    $lbl = ByName $Arg1
    if (-not $lbl) { Write-Output "no tile '$Arg1'"; exit 1 }
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $cur = $lbl
    for ($i = 0; $i -lt 6 -and $cur; $i++) {
      try { $cur.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Output "opened '$Arg1'"; exit 0 } catch { $cur = $walker.GetParent($cur) }
    }
    Write-Output "tile '$Arg1' not invokable"; exit 1
  }
  default { Write-Output "unknown action $Action" }
}

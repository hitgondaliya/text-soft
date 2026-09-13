# copyfonts.ps1 — dump Shree-Lipi's fonts as DECRYPTED TrueType files.
#
# Shree ships its fonts encrypted; Font Manager decrypts them on the way out. Its
# "Copy Fonts" button writes plain TTFs to a folder with no dialog, which is what
# makes the whole mapping pipeline file-based: once the legacy font and the Unicode
# font of the same design are both on disk, GlyphJoin can match them by shape
# without driving any GUI per encoding.
#
# Font Manager must already be open (tools/shree/uia.ps1 -Action tile -Arg1 "Font Manager").
#
#   copyfonts.ps1 -Proc SLNXT -Scripts DEVANAGARI,GUJARATI -Layouts Unicode,ShreeLipi-Ex
param(
  [string]$Proc = 'SLNXT',
  [string[]]$Scripts = @('DEVANAGARI','BANGLA','ASSAMESE','MANIPURI','GUJARATI','KANNADA','MALAYALAM','ODIA','PUNJABI','TAMIL','TELUGU','SANSKRIT'),
  [string[]]$Layouts = @('Unicode','ShreeLipi-Ex')
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes | Out-Null
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]

function App {
  $root = $AE::RootElement
  foreach ($k in $root.FindAll($TS::Children, [System.Windows.Automation.Condition]::TrueCondition)) {
    $pn = ''
    try { $pn = (Get-Process -Id $k.Current.ProcessId -ErrorAction SilentlyContinue).ProcessName } catch {}
    if ($pn -eq $Proc) { return $k }
  }
  return $null
}

function SetCombo($app, $id, $item) {
  $c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
  $cb = $app.FindFirst($TS::Descendants, $c)
  if (-not $cb) { return "no combo $id" }
  try { $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 300 } catch {}
  $ic = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $item)
  $it = $cb.FindFirst($TS::Descendants, $ic)
  if (-not $it) { try { $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse() } catch {}; return "no '$item'" }
  $it.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
  try { $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse() } catch {}
  return 'ok'
}

foreach ($s in $Scripts) {
  foreach ($l in $Layouts) {
    $app = App
    if (-not $app) { Write-Output 'app window gone'; exit 1 }

    $r1 = SetCombo $app 'Script' $s
    Start-Sleep -Milliseconds 600
    $r2 = SetCombo $app 'Layout' $l
    Start-Sleep -Seconds 2
    if ($r1 -ne 'ok' -or $r2 -ne 'ok') { Write-Output "$s / $l : skip ($r1 / $r2)"; continue }

    $app = App
    $tc = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, 'lbTotalFonts')
    $tot = $app.FindFirst($TS::Descendants, $tc)
    $n = if ($tot) { $tot.Current.Name } else { '?' }
    if ($n -eq '0') { Write-Output "$s / $l : none"; continue }

    # tick every font family, then copy
    $cc = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, 'CheckItem')
    $ticked = 0
    foreach ($b in $app.FindAll($TS::Descendants, $cc)) {
      try {
        $tp = $b.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
        if ($tp.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) { $tp.Toggle(); $ticked++ }
      } catch {}
    }
    Start-Sleep -Milliseconds 500

    $bc = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, 'Copy Fonts')
    $btn = $app.FindFirst($TS::Descendants, $bc)
    if (-not $btn -or -not $btn.Current.IsEnabled) { Write-Output "$s / $l : families=$n ticked=$ticked COPY DISABLED"; continue }
    $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 4
    Write-Output "$s / $l : families=$n ticked=$ticked copied"
  }
}

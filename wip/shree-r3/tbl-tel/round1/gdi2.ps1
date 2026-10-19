param([string]$out)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 1400, 700
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::White)
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$lab = New-Object System.Drawing.Font('Arial', 11)
$uni = New-Object System.Drawing.Font('Nirmala UI', 48)
$leg = New-Object System.Drawing.Font('SHREE-TEL7-Apoorva-0900', 48)
$b = [System.Drawing.Brushes]::Black
function U([int[]]$cps) { -join ($cps | ForEach-Object { [char]$_ }) }
function B([string]$hex) { -join ($hex.Split(' ') | ForEach-Object { [char][Convert]::ToInt32($_, 16) }) }
$rows = @(
 @('Nirmala: ka+RRA vattu | ka+ka-ssa vattu | ka+ssa vattu | ka+nga vattu', 'U', ((U @(0x0C15,0x0C4D,0x0C31)) + '  ' + (U @(0x0C15,0x0C4D,0x0C15,0x0C4D,0x0C37)) + '  ' + (U @(0x0C15,0x0C4D,0x0C37)) + '  ' + (U @(0x0C15,0x0C4D,0x0C19)))),
 @('Shree: 4D E6 FC CA | 4D E6 FC 3C | 4D E6 FC DB | 4D E6 FC 94', 'L', ((B '4D E6 FC CA') + '  ' + (B '4D E6 FC 3C') + '  ' + (B '4D E6 FC DB') + '  ' + (B '4D E6 FC 94'))),
 @('Nirmala: RRA | ssa | ka-ssa', 'U', ((U @(0x0C31)) + '  ' + (U @(0x0C37)) + '  ' + (U @(0x0C15,0x0C4D,0x0C37)))),
 @('Shree: 82 FD | E7 D9 | 84 E6 FC', 'L', ((B '82 FD') + '  ' + (B 'E7 D9') + '  ' + (B '84 E6 FC')))
)
$y = 10
foreach ($r in $rows) {
  $g.DrawString($r[0], $lab, $b, 10, $y)
  $f = if ($r[1] -eq 'U') { $uni } else { $leg }
  $g.DrawString($r[2], $f, $b, 10, $y + 18)
  $y += 165
}
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)

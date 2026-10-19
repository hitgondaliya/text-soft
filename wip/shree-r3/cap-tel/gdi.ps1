param([string]$out)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 1400, 900
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::White)
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$lab = New-Object System.Drawing.Font('Arial', 11)
$uni = New-Object System.Drawing.Font('Nirmala UI', 40)
$leg = New-Object System.Drawing.Font('SHREE-TEL7-Apoorva-0900', 40)
$b = [System.Drawing.Brushes]::Black
function U([int[]]$cps) { -join ($cps | ForEach-Object { [char]$_ }) }
function B([string]$hex) { -join ($hex.Split(' ') | ForEach-Object { [char][Convert]::ToInt32($_, 16) }) }
$rows = @(
 @('Unicode ka+visarga, ka+candrabindu, ka+RR, ka+R (Nirmala UI)', 'U', (U @(0x0C15,0x0C03,0x20,0x0C15,0x0C01,0x20,0x0C15,0x0C44,0x20,0x0C15,0x0C43))),
 @('Shree composer: 4D E6 FC 40 | 4D E6 FC B6 | 4D E6 FC 97 | 4D E6 FC 96', 'L', ((B '4D E6 FC 40') + ' ' + (B '4D E6 FC B6') + ' ' + (B '4D E6 FC 97') + ' ' + (B '4D E6 FC 96'))),
 @('shipped table: 4D E6 FC 5D | 4D E6 FC 97', 'L', ((B '4D E6 FC 5D') + ' ' + (B '4D E6 FC 97'))),
 @('Unicode kai, kra, shri, stra (Nirmala UI)', 'U', (U @(0x0C15,0x0C48,0x20,0x0C15,0x0C4D,0x0C30,0x20,0x0C36,0x0C4D,0x0C30,0x0C40,0x20,0x0C38,0x0C4D,0x0C24,0x0C4D,0x0C30))),
 @('Shree: kai 4D F0 FC 4F | kra 7B 4D E6 FC | pending 4D E6 FC 7B | 7D | E7 DC 88', 'L', ((B '4D F0 FC 4F') + ' ' + (B '7B 4D E6 FC') + ' ' + (B '4D E6 FC 7B') + ' ' + (B '7D') + ' ' + (B 'E7 DC 88'))),
 @('shipped: kai 4F 4D F0 FC | kra 93 4D E6 FC', 'L', ((B '4F 4D F0 FC') + ' ' + (B '93 4D E6 FC')))
)
$y = 10
foreach ($r in $rows) {
  $g.DrawString($r[0], $lab, $b, 10, $y)
  $f = if ($r[1] -eq 'U') { $uni } else { $leg }
  $g.DrawString($r[2], $f, $b, 10, $y + 18)
  $y += 140
}
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
"font used for legacy: " + $leg.Name + " / unicode: " + $uni.Name

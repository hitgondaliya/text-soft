param([string]$out)
Add-Type -AssemblyName System.Drawing
$enc = [System.Text.Encoding]::GetEncoding(1252)
$bmp = New-Object System.Drawing.Bitmap 1400, 520
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::White)
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$lab = New-Object System.Drawing.Font('Arial', 11)
$uni = New-Object System.Drawing.Font('Nirmala UI', 48)
$leg = New-Object System.Drawing.Font('SHREE-TEL7-Apoorva-0900', 48)
$b = [System.Drawing.Brushes]::Black
function U([int[]]$cps) { -join ($cps | ForEach-Object { [char]$_ }) }
function B([string]$hex) { $bytes = [byte[]]($hex.Split(' ') | ForEach-Object { [Convert]::ToByte($_, 16) }); $enc.GetString($bytes) }
$rows = @(
 @('Nirmala: vocalic R, vocalic RR | ka+RRA vattu, ra+RRA vattu', 'U', ((U @(0x0C0B)) + '  ' + (U @(0x0C60)) + '    ' + (U @(0x0C15,0x0C4D,0x0C31)) + '  ' + (U @(0x0C30,0x0C4D,0x0C31)))),
 @('Shree: BA 24 24 | BA 24 2A | BA CA || 4D E6 FC CA | 4D E6 FC 3C | C6 E6 FF 3C | C6 E6 FF CA', 'L', ((B 'BA 24 24') + '  ' + (B 'BA 24 2A') + '  ' + (B 'BA CA') + '    ' + (B '4D E6 FC CA') + '  ' + (B '4D E6 FC 3C') + '  ' + (B 'C6 E6 FF 3C') + '  ' + (B 'C6 E6 FF CA'))),
 @('Nirmala: ka-uu, ka-ka-ssa', 'U', ((U @(0x0C15,0x0C42)) + '  ' + (U @(0x0C15,0x0C4D,0x0C15,0x0C4D,0x0C37)))),
 @('Shree: 4D E6 FC 2A | 4D E6 FC 50 DB | 4D 8A FC 84 E6 FC', 'L', ((B '4D E6 FC 2A') + '  ' + (B '4D E6 FC 50 DB') + '  ' + (B '4D 8A FC 84 E6 FC')))
)
$y = 10
foreach ($r in $rows) {
  $g.DrawString($r[0], $lab, $b, 10, $y)
  $f = if ($r[1] -eq 'U') { $uni } else { $leg }
  $g.DrawString($r[2], $f, $b, 10, $y + 18)
  $y += 125
}
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)

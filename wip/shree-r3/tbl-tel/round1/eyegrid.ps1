param([string]$in, [string]$out, [int]$cols = 12)
# Each input line: <unicode text>\t<hex bytes>. Draws Nirmala UI (top) over SHREE-TEL7-Apoorva-0900 (bottom) per cell.
Add-Type -AssemblyName System.Drawing
$enc = [System.Text.Encoding]::GetEncoding(1252)
$lines = [System.IO.File]::ReadAllLines($in, [System.Text.Encoding]::UTF8)
$cw = 110; $ch = 150
$rows = [Math]::Ceiling($lines.Count / $cols)
$bmp = New-Object System.Drawing.Bitmap ($cw * $cols), ($ch * $rows)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::White)
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$lab = New-Object System.Drawing.Font('Arial', 7)
$uni = New-Object System.Drawing.Font('Nirmala UI', 26)
$leg = New-Object System.Drawing.Font('SHREE-TEL7-Apoorva-0900', 30)
$b = [System.Drawing.Brushes]::Black
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::LightGray)
for ($i = 0; $i -lt $lines.Count; $i++) {
  $p = $lines[$i].Split("`t")
  $x = ($i % $cols) * $cw; $y = [Math]::Floor($i / $cols) * $ch
  $g.DrawRectangle($pen, $x, $y, $cw - 1, $ch - 1)
  $g.DrawString($p[1], $lab, $b, $x + 2, $y + 2)
  $g.DrawString($p[0], $uni, $b, $x + 8, $y + 10)
  $bytes = [byte[]]($p[1].Split(' ') | ForEach-Object { [Convert]::ToByte($_, 16) })
  $s = $enc.GetString($bytes).Replace([char]0x90, [char]0x90)
  $g.DrawString($s, $leg, $b, $x + 8, $y + 75)
}
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)

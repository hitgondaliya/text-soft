param([string]$rowsFile, [string]$outPrefix, [string]$legacyFont = 'SHREE-ORI7-0600', [int]$perImage = 60)
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$cp = [Text.Encoding]::GetEncoding(1252)
$rows = Get-Content -Raw -Encoding UTF8 $rowsFile | ConvertFrom-Json
$uni = New-Object System.Windows.Media.Typeface('Nirmala UI')
$leg = New-Object System.Windows.Media.Typeface($legacyFont)
$lab = New-Object System.Windows.Media.Typeface('Consolas')
$cols = 4; $cellW = 300; $cellH = 90
for ($start = 0; $start -lt $rows.Count; $start += $perImage) {
  $chunk = $rows[$start..([Math]::Min($rows.Count, $start + $perImage) - 1)]
  $H = [Math]::Ceiling($chunk.Count / $cols) * $cellH; $W = $cols * $cellW
  $dv = New-Object System.Windows.Media.DrawingVisual; $dc = $dv.RenderOpen()
  $dc.DrawRectangle([System.Windows.Media.Brushes]::White, $null, (New-Object System.Windows.Rect(0,0,$W,$H)))
  for ($i = 0; $i -lt $chunk.Count; $i++) {
    $u = [string]$chunk[$i][0]; $hex = [string]$chunk[$i][1]
    $bytes = [byte[]]($hex.Split(' ') | Where-Object { $_ } | ForEach-Object { [Convert]::ToByte($_, 16) })
    $l = $cp.GetString($bytes)
    $x = ($i % $cols) * $cellW; $y = [Math]::Floor($i / $cols) * $cellH
    $dc.DrawRectangle($null, (New-Object System.Windows.Media.Pen([System.Windows.Media.Brushes]::LightGray, 1)), (New-Object System.Windows.Rect($x,$y,$cellW,$cellH)))
    $f1 = New-Object System.Windows.Media.FormattedText($u, [Globalization.CultureInfo]::InvariantCulture, 'LeftToRight', $uni, 40.0, [System.Windows.Media.Brushes]::Black, 1.0)
    $f2 = New-Object System.Windows.Media.FormattedText($l, [Globalization.CultureInfo]::InvariantCulture, 'LeftToRight', $leg, 40.0, [System.Windows.Media.Brushes]::DarkBlue, 1.0)
    $f3 = New-Object System.Windows.Media.FormattedText(("{0} {1}" -f ($start+$i), $hex), [Globalization.CultureInfo]::InvariantCulture, 'LeftToRight', $lab, 11.0, [System.Windows.Media.Brushes]::Gray, 1.0)
    $dc.DrawText($f1, (New-Object System.Windows.Point(($x+8),($y+18))))
    $dc.DrawText($f2, (New-Object System.Windows.Point(($x+150),($y+18))))
    $dc.DrawText($f3, (New-Object System.Windows.Point(($x+4),($y+2))))
  }
  $dc.Close()
  $bmp = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($W,$H,96,96,[System.Windows.Media.PixelFormats]::Pbgra32); $bmp.Render($dv)
  $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder; $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
  $fs = [IO.File]::Create($outPrefix + "-" + ([int][Math]::Floor($start/$perImage)).ToString("00") + ".png"); $enc.Save($fs); $fs.Close()
}
"rows $($rows.Count)"

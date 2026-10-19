param([int]$Start = 0, [int]$Count = 50, [string]$Out)
# Contact sheet: each cell = the code point drawn in the family's face (big, black) beside the same character in Arial (gray).
Add-Type -AssemblyName System.Drawing
$root = 'C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3\word'
$items = Get-Content (Join-Path $root 'eye-items.tsv') -Encoding UTF8 | Select-Object -Skip $Start -First $Count
$cols = 10; $cw = 150; $ch = 120
$rows = [Math]::Ceiling($items.Count / $cols)
$bmp = New-Object System.Drawing.Bitmap ($cols * $cw), ($rows * $ch)
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::White)
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$lab = New-Object System.Drawing.Font('Arial', 8)
$ar = New-Object System.Drawing.Font('Arial', 40, [System.Drawing.GraphicsUnit]::Pixel)
$i = 0
foreach ($line in $items) {
    $p = $line -split "`t"
    $x = ($i % $cols) * $cw; $y = [Math]::Floor($i / $cols) * $ch
    $f = New-Object System.Drawing.Font($p[1], 64, [System.Drawing.GraphicsUnit]::Pixel)
    $cp = [Convert]::ToInt32($p[2], 16)
    $g.DrawRectangle([System.Drawing.Pens]::LightGray, $x, $y, $cw - 1, $ch - 1)
    $g.DrawString(("{0} {1} {2}" -f ($Start + $i), $p[0], $p[2]), $lab, [System.Drawing.Brushes]::Blue, $x + 2, $y + 2)
    $g.DrawString([string][char]$cp, $f, [System.Drawing.Brushes]::Black, $x + 10, $y + 22)
    $g.DrawString([string][char]$cp, $ar, [System.Drawing.Brushes]::Gray, $x + 100, $y + 40)
    $f.Dispose()
    $i++
}
$bmp.Save($Out); $g.Dispose(); $bmp.Dispose()
"saved $($items.Count) to $Out"

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$tests = @(
  @('ka+virama+YA(0B2F)', [string]::new([char[]]@(0x0B15,0x0B4D,0x0B2F))),
  @('ka+virama+YYA(0B5F)', [string]::new([char[]]@(0x0B15,0x0B4D,0x0B5F))),
  @('ka+virama+ZWNJ+YA', [string]::new([char[]]@(0x0B15,0x0B4D,0x200C,0x0B2F))),
  @('ka+virama+ZWNJ+YYA', [string]::new([char[]]@(0x0B15,0x0B4D,0x200C,0x0B5F))),
  @('ka+YA', [string]::new([char[]]@(0x0B15,0x0B2F))),
  @('ka+virama+BA(0B2C)', [string]::new([char[]]@(0x0B15,0x0B4D,0x0B2C))),
  @('ka+virama+WA(0B71)', [string]::new([char[]]@(0x0B15,0x0B4D,0x0B71))),
  @('ka+virama+ZWJ', [string]::new([char[]]@(0x0B15,0x0B4D,0x200D))),
  @('ka+virama+ZWNJ', [string]::new([char[]]@(0x0B15,0x0B4D,0x200C))),
  @('ka+virama', [string]::new([char[]]@(0x0B15,0x0B4D)))
)
foreach ($fam in @('Nirmala UI','Kalinga')) {
  $tf = New-Object System.Windows.Media.Typeface($fam)
  foreach ($t in $tests) {
    $ft = New-Object System.Windows.Media.FormattedText($t[1], [Globalization.CultureInfo]::InvariantCulture, [System.Windows.FlowDirection]::LeftToRight, $tf, 100.0, [System.Windows.Media.Brushes]::Black, 1.0)
    $g = $ft.BuildGeometry((New-Object System.Windows.Point(0,0)))
    $b = $g.Bounds
    "{0,-12} {1,-24} width {2,7:F1}  inkW {3,7:F1} inkH {4,7:F1} inkTop {5,7:F1}" -f $fam, $t[0], $ft.WidthIncludingTrailingWhitespace, $b.Width, $b.Height, $b.Top
  }
}

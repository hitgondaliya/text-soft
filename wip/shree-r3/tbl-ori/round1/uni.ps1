param([string]$out, [string]$text, [string]$font = 'Nirmala UI')
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$tf = New-Object System.Windows.Media.Typeface($font)
$ft = New-Object System.Windows.Media.FormattedText($text, [Globalization.CultureInfo]::InvariantCulture, [System.Windows.FlowDirection]::LeftToRight, $tf, 64.0, [System.Windows.Media.Brushes]::Black, 1.0)
$dv = New-Object System.Windows.Media.DrawingVisual
$dc = $dv.RenderOpen()
$dc.DrawRectangle([System.Windows.Media.Brushes]::White, $null, (New-Object System.Windows.Rect(0,0,1200,120)))
$dc.DrawText($ft, (New-Object System.Windows.Point(10,10)))
$dc.Close()
$bmp = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(1200,120,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
$bmp.Render($dv)
$enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
$fs = [IO.File]::Create($out); $enc.Save($fs); $fs.Close()

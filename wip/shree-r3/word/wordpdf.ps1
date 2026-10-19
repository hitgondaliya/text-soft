param([string]$List)
# Export each family's test .docx to PDF in a NEW hidden Word instance (read-only open). ASCII only.
$ErrorActionPreference = 'Continue'
$root = 'C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3\word'
$fam = Join-Path $root 'fam'
$log = Join-Path $root 'wordpdf.log'
function L($m) { $s = (Get-Date).ToString('HH:mm:ss.fff') + ' ' + $m; Add-Content -Path $log -Value $s -Encoding ascii; $s }
$fams = $List -split ','
$before = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$w = New-Object -ComObject Word.Application
Start-Sleep -Seconds 2
$after = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$mine = @($after | Where-Object { $before -notcontains $_ })
Set-Content -Path (Join-Path $root 'word-pid.txt') -Value ($mine -join ',') -Encoding ascii
L "started mine=[$($mine -join ',')] before=[$($before -join ',')] version=$($w.Version) build=$($w.Build)"
try {
    $w.Visible = $false
    $w.DisplayAlerts = 0
    foreach ($f in $fams) {
        $docx = Join-Path $fam ($f + '.docx')
        $pdf = Join-Path $fam ($f + '.word.pdf')
        if (Test-Path $pdf) { Remove-Item $pdf -Force }
        $t0 = Get-Date
        $d = $w.Documents.Open($docx, $false, $true)
        if ($null -eq $d) { L "$f OPEN FAILED"; continue }
        L "$f opened paras=$($d.Paragraphs.Count)"
        $d.ExportAsFixedFormat($pdf, 17)
        $d.Close([ref]0)
        $ms = [int]((Get-Date) - $t0).TotalMilliseconds
        L "$f exported=$(Test-Path $pdf) ms=$ms"
    }
}
catch { L "ERROR: $($_.Exception.Message)" }
finally {
    try { $w.Quit([ref]0) } catch { L "quit failed: $($_.Exception.Message)" }
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($w)
    Start-Sleep -Seconds 3
    foreach ($q in $mine) {
        if (Get-Process -Id $q -ErrorAction SilentlyContinue) { L "pid $q still running after Quit; stopping it"; Stop-Process -Id $q -Force }
        else { L "pid $q exited" }
    }
}

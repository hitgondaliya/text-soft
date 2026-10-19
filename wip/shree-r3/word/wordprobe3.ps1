param([string]$Files)
$ErrorActionPreference = 'Continue'
$root = 'C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3\word'
$log = Join-Path $root 'wordprobe3.log'
function L($m) { $s = (Get-Date).ToString('HH:mm:ss.fff') + ' ' + $m; Add-Content -Path $log -Value $s -Encoding ascii; $s }
$before = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$w = New-Object -ComObject Word.Application
Start-Sleep -Seconds 2
$after = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$mine = @($after | Where-Object { $before -notcontains $_ })
Set-Content -Path (Join-Path $root 'word-pid.txt') -Value ($mine -join ',') -Encoding ascii
L "started mine=[$($mine -join ',')]"
try {
    $w.Visible = $false
    $w.DisplayAlerts = 0
    foreach ($f in ($Files -split ',')) {
        $d = $w.Documents.Open($f, $false, $true)
        L "$f open null=$($null -eq $d)"
        if ($null -eq $d) { continue }
        $pdf = $f + '.pdf'
        L "export start -> $pdf"
        $d.ExportAsFixedFormat($pdf, 17)
        L "export done exists=$(Test-Path $pdf)"
        $d.Close([ref]0)
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

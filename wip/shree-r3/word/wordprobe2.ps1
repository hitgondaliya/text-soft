$ErrorActionPreference = 'Continue'
$root = 'C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3\word'
$log = Join-Path $root 'wordprobe2.log'
function L($m) { $s = (Get-Date).ToString('HH:mm:ss.fff') + ' ' + $m; Add-Content -Path $log -Value $s -Encoding ascii; $s }
$rtf = Join-Path $root 'probe.rtf'
Set-Content -Path $rtf -Value '{\rtf1\ansi\deff0{\fonttbl{\f0\fnil Arial;}}\f0 Hello\par}' -Encoding ascii
$before = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$w = New-Object -ComObject Word.Application
Start-Sleep -Seconds 2
$after = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$mine = @($after | Where-Object { $before -notcontains $_ })
Set-Content -Path (Join-Path $root 'word-pid.txt') -Value ($mine -join ',') -Encoding ascii
L "started mine=[$($mine -join ',')] docs=$($w.Documents.Count)"
try {
    $w.Visible = $false
    $w.DisplayAlerts = 0
    $d1 = $w.Documents.Open($rtf, $false, $true)
    L "rtf: null=$($null -eq $d1) docs=$($w.Documents.Count) text=$(if ($d1) { $d1.Content.Text })"
    if ($d1) { $d1.Close([ref]0) }
    $docx = Join-Path $root 'fam\probe1.docx'
    $d2 = $w.Documents.Open($docx, $false, $true)
    L "docx 3 args: null=$($null -eq $d2) docs=$($w.Documents.Count)"
    if ($d2) { L "paras=$($d2.Paragraphs.Count)"; $d2.Close([ref]0) }
    $d3 = $w.Documents.OpenNoRepairDialog($docx, $false, $true)
    L "docx norepair: null=$($null -eq $d3) docs=$($w.Documents.Count)"
    if ($d3) { L "paras=$($d3.Paragraphs.Count)"; $d3.Close([ref]0) }
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

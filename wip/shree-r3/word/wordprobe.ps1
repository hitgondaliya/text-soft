param([string]$Fam, [int]$TryExport = 0)
# Step-logged probe of what the free Word edition allows through COM. ASCII only.
$ErrorActionPreference = 'Stop'
$root = 'C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3\word'
$fam = Join-Path $root 'fam'
$log = Join-Path $root 'wordprobe.log'
function L($m) { $s = (Get-Date).ToString('HH:mm:ss.fff') + ' ' + $m; Add-Content -Path $log -Value $s -Encoding ascii; $s }
$pidFile = Join-Path $root 'word-pid.txt'
$before = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$w = New-Object -ComObject Word.Application
Start-Sleep -Seconds 3
$after = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$mine = @($after | Where-Object { $before -notcontains $_ })
Set-Content -Path $pidFile -Value ($mine -join ',') -Encoding ascii
L "started mine=[$($mine -join ',')]"
try {
    $w.Visible = $false
    $w.DisplayAlerts = 0
    $docx = Join-Path $fam ($Fam + '.docx')
    $ret = $w.Documents.Open($docx, $false, $true, $false)
    L "open returned null=$($null -eq $ret) docs=$($w.Documents.Count) pv=$($w.ProtectedViewWindows.Count)"
    $doc = $ret
    for ($i = 0; $i -lt 40 -and $null -eq $doc -and $w.Documents.Count -eq 0 -and $w.ProtectedViewWindows.Count -eq 0; $i++) { Start-Sleep -Milliseconds 500 }
    L "after wait docs=$($w.Documents.Count) pv=$($w.ProtectedViewWindows.Count) windows=$($w.Windows.Count)"
    if ($null -eq $doc -and $w.Documents.Count -gt 0) { $doc = $w.Documents.Item(1) }
    if ($null -eq $doc -and $w.ProtectedViewWindows.Count -gt 0) { $doc = $w.ProtectedViewWindows.Item(1).Document; L "using protected view document" }
    L "doc name=$($doc.Name) paragraphs=$($doc.Paragraphs.Count) ro=$($doc.ReadOnly)"
    $p = $doc.Paragraphs.Item(1)
    $bits = $p.Range.EnhMetaFileBits
    L "emf type=$($bits.GetType().FullName) len=$($bits.Length)"
    [System.IO.File]::WriteAllBytes((Join-Path $fam ($Fam + '.p1.emf')), [byte[]]$bits)
    L "emf saved"
    if ($TryExport -eq 1) {
        $pdf = Join-Path $fam ($Fam + '.word.pdf')
        L "export start"
        $doc.ExportAsFixedFormat($pdf, 17)
        L "export done exists=$(Test-Path $pdf)"
    }
    $doc.Close()
    L "closed"
}
catch { L "ERROR: $($_.Exception.Message)" }
finally {
    try { $w.Quit() } catch { L "quit failed: $($_.Exception.Message)" }
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($w) | Out-Null
    Start-Sleep -Seconds 3
    foreach ($q in $mine) {
        if (Get-Process -Id $q -ErrorAction SilentlyContinue) { L "pid $q still running after Quit; stopping it"; Stop-Process -Id $q -Force }
        else { L "pid $q exited" }
    }
}

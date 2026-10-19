param([string]$List, [string]$PidFile)
# ONE new hidden Word instance for the xa-* documents: open read-only, export PDF, save each paragraph's EMF picture.
# Quits only the instance it started. ASCII only.
$ErrorActionPreference = 'Continue'
$root = 'C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3\word'
$fam = Join-Path $root 'fam'
$before = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$w = New-Object -ComObject Word.Application
Start-Sleep -Milliseconds 1500
$after = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$mine = @($after | Where-Object { $before -notcontains $_ })
Set-Content -Path $PidFile -Value ($mine -join ',') -Encoding ascii
try {
    $w.Visible = $false
    $w.DisplayAlerts = 0
    'version ' + $w.Version + ' build ' + $w.Build
    foreach ($f in ($List -split ',')) {
        try {
            $docx = Join-Path $fam ($f + '.docx')
            $pdf = Join-Path $fam ($f + '.word.pdf')
            if (Test-Path $pdf) { Remove-Item $pdf -Force }
            $outDir = Join-Path $root ('emf\' + $f)
            New-Item -ItemType Directory -Force $outDir | Out-Null
            $d = $w.Documents.Open($docx, $false, $true)
            if ($null -eq $d) { "$f OPEN FAILED"; continue }
            $ro = $d.ReadOnly
            $d.ExportAsFixedFormat($pdf, 17)
            $n = $d.Paragraphs.Count
            for ($i = 1; $i -le $n; $i++) {
                $r = $d.Paragraphs.Item($i).Range
                $bits = [byte[]]$r.EnhMetaFileBits
                [System.IO.File]::WriteAllBytes((Join-Path $outDir ('{0:D4}.emf' -f $i)), $bits)
            }
            $d.Close([ref]0)
            "$f OK readOnly=$ro paragraphs=$n pdf=$(Test-Path $pdf)"
        }
        catch { "$f ERROR: " + $_.Exception.Message }
    }
}
catch { 'ERROR: ' + $_.Exception.Message }
finally {
    try { $w.Quit([ref]0) } catch { }
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($w)
    Start-Sleep -Seconds 2
    foreach ($q in $mine) { if (Get-Process -Id $q -ErrorAction SilentlyContinue) { Stop-Process -Id $q -Force; "stopped leftover word pid $q" } }
}

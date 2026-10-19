param([string]$Docx, [string]$OutDir, [string]$PidFile)
# Save Word's own picture (Range.EnhMetaFileBits) of every paragraph of ONE docx, in a new hidden Word instance. ASCII only.
$ErrorActionPreference = 'Continue'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$before = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$w = New-Object -ComObject Word.Application
Start-Sleep -Milliseconds 1500
$after = @(Get-Process WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$mine = @($after | Where-Object { $before -notcontains $_ })
Set-Content -Path $PidFile -Value ($mine -join ',') -Encoding ascii
try {
    $w.Visible = $false
    $w.DisplayAlerts = 0
    $d = $w.Documents.Open($Docx, $false, $true)
    if ($null -eq $d) { 'OPEN FAILED' }
    else {
        $n = $d.Paragraphs.Count
        for ($i = 1; $i -le $n; $i++) {
            $r = $d.Paragraphs.Item($i).Range
            $bits = [byte[]]$r.EnhMetaFileBits
            [System.IO.File]::WriteAllBytes((Join-Path $OutDir ('{0:D4}.emf' -f $i)), $bits)
        }
        $d.Close([ref]0)
        "OK $n"
    }
}
catch { 'ERROR: ' + $_.Exception.Message }
finally {
    try { $w.Quit([ref]0) } catch { }
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($w)
    Start-Sleep -Seconds 2
    foreach ($q in $mine) { if (Get-Process -Id $q -ErrorAction SilentlyContinue) { Stop-Process -Id $q -Force } }
}

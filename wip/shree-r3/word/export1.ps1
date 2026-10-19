param([string]$Docx, [string]$Pdf, [string]$PidFile)
# Export ONE docx to PDF in its own new hidden Word instance. ASCII only.
$ErrorActionPreference = 'Continue'
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
        $d.ExportAsFixedFormat($Pdf, 17)
        $d.Close([ref]0)
        'OK'
    }
}
catch { 'ERROR: ' + $_.Exception.Message }
finally {
    try { $w.Quit([ref]0) } catch { }
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($w)
    Start-Sleep -Seconds 2
    foreach ($q in $mine) { if (Get-Process -Id $q -ErrorAction SilentlyContinue) { Stop-Process -Id $q -Force } }
}

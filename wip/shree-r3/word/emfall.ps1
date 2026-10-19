param([string]$List, [int]$TimeoutSec = 300)
# Drive emf1.ps1 per family with a timeout; a hung run is stopped (only the Word instance it started). ASCII only.
$root = 'C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3\word'
$fam = Join-Path $root 'fam'
$log = Join-Path $root 'emfall.log'
function L($m) { $s = (Get-Date).ToString('HH:mm:ss') + ' ' + $m; Add-Content -Path $log -Value $s -Encoding ascii; $s }
foreach ($f in ($List -split ',')) {
    $docx = Join-Path $fam ($f + '.docx')
    $outDir = Join-Path $root ('emf\' + $f)
    $pidFile = Join-Path $root ('pidemf-' + $f + '.txt')
    $out = Join-Path $root ('emf-' + $f + '.out')
    $t0 = Get-Date
    $p = Start-Process -FilePath powershell.exe -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root 'emf1.ps1'), '-Docx', $docx, '-OutDir', $outDir, '-PidFile', $pidFile) -WindowStyle Hidden -PassThru -RedirectStandardOutput $out
    $done = $p.WaitForExit($TimeoutSec * 1000)
    $secs = [int]((Get-Date) - $t0).TotalSeconds
    if (-not $done) {
        $wp = if (Test-Path $pidFile) { (Get-Content $pidFile) -split ',' } else { @() }
        foreach ($q in $wp) { if ($q) { Stop-Process -Id ([int]$q) -Force -ErrorAction SilentlyContinue } }
        Start-Sleep -Seconds 2
        if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
        L "$f TIMEOUT after $secs s (stopped word pid $($wp -join ','))"
    }
    else {
        $res = if (Test-Path $out) { (Get-Content $out) -join ' ' } else { '' }
        L "$f $res $secs s"
    }
}

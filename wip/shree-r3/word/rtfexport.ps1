$root = 'C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3\word'
foreach ($f in @('SHREE-GUJ7', 'SHREE-BAN7', 'SHREE-TEL7')) {
    $src = Join-Path $root ('fam\rtf-' + $f + '.rtf')
    $pdf = Join-Path $root ('fam\rtf-' + $f + '.word.pdf')
    foreach ($job in @(@('export1.ps1', @('-Docx', $src, '-Pdf', $pdf, '-PidFile', (Join-Path $root ('pid-rtf-' + $f + '.txt')))), @('emf1.ps1', @('-Docx', $src, '-OutDir', (Join-Path $root ('emf\rtf-' + $f)), '-PidFile', (Join-Path $root ('pidemf-rtf-' + $f + '.txt')))))) {
        $out = Join-Path $root ('rtfexport-' + $f + '-' + $job[0] + '.out')
        $pidFile = $job[1][5]
        $p = Start-Process -FilePath powershell.exe -ArgumentList (@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $root $job[0])) + $job[1]) -WindowStyle Hidden -PassThru -RedirectStandardOutput $out
        if (-not $p.WaitForExit(180000)) {
            $wp = if (Test-Path $pidFile) { (Get-Content $pidFile) -split ',' } else { @() }
            foreach ($q in $wp) { if ($q) { Stop-Process -Id ([int]$q) -Force -ErrorAction SilentlyContinue } }
            if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
            "$f $($job[0]) TIMEOUT"
        }
        else { "$f $($job[0]) " + ((Get-Content $out) -join ' ') }
    }
}

$r = 'C:\Users\harde\AppData\Local\Temp\claude\C--Users-harde-Desktop-Windows-Font-Software\7673923b-96b4-4287-87dd-a8e7c6533eca\scratchpad\r3\word'
$exe = Join-Path $r 'tool\bin\Release\net8.0-windows10.0.19041.0\wordvis.exe'
Remove-Item (Join-Path $r 'fam\*.richedit.fcharset0.json'), (Join-Path $r 'fam\*.richedit.fcharset2.json') -ErrorAction SilentlyContinue
# product run again for the five families whose product crops the first variant run overwrote
if (Test-Path Env:\WV_RTFVAR) { Remove-Item Env:\WV_RTFVAR }
& $exe richedit SHREE-GUJ7 SHREE-BAN7 SHREE-DEV SHREE-TAM7 SHREE-ASS
foreach ($v in @('fs', 'fcharset0fs', 'fcharset2fs')) {
    $env:WV_RTFVAR = $v
    & $exe richedit SHREE-GUJ7 SHREE-BAN7 SHREE-DEV SHREE-TAM7 SHREE-ASS
}
Remove-Item Env:\WV_RTFVAR

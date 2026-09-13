# units.ps1 — the probe text for Shree's converter: one text unit per line.
#
# Base letters alone only reach half a legacy table. A legacy font gives its own
# code to every half form, every consonant+matra cluster and every conjunct, so
# each of those has to be asked for by name. The conjunct+matra crosses matter
# too: without them the matra's position after a conjunct cannot be reconstructed.
#
#   units.ps1 -Block 0x0980 -Out units-ban.txt
param([int]$Block = 0x0900, [string]$Out = 'units.txt', [switch]$NoConjunctMatra)

$sb = New-Object Text.StringBuilder
$assigned = @()
for ($c = $Block; $c -lt $Block + 0x80; $c++) {
  if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory([char]$c) -eq 'OtherNotAssigned') { continue }
  $assigned += $c
  [void]$sb.AppendLine([string][char]$c)
}
[void]$sb.AppendLine([string][char]0x0964)
[void]$sb.AppendLine([string][char]0x0965)

$virama = [char]($Block + 0x4D)
$cons = @()
for ($c = $Block + 0x15; $c -le $Block + 0x39; $c++) { if ($assigned -contains $c) { $cons += [char]$c } }
for ($c = $Block + 0x58; $c -le $Block + 0x5F; $c++) { if ($assigned -contains $c) { $cons += [char]$c } }
$matra = @()
for ($c = $Block + 0x3E; $c -le $Block + 0x4C; $c++) { if ($assigned -contains $c) { $matra += [char]$c } }

foreach ($a in $cons) { [void]$sb.AppendLine([string]$a + $virama) }
foreach ($a in $cons) { foreach ($m in $matra) { [void]$sb.AppendLine([string]$a + $m) } }
foreach ($a in $cons) { foreach ($b in $cons) { [void]$sb.AppendLine([string]$a + $virama + $b) } }
if (-not $NoConjunctMatra) {
  foreach ($a in $cons) { foreach ($b in $cons) { foreach ($m in $matra) { [void]$sb.AppendLine([string]$a + $virama + $b + $m) } } }
}

[IO.File]::WriteAllText($Out, $sb.ToString(), [Text.Encoding]::UTF8)
Write-Output ('consonants=' + $cons.Count + ' matras=' + $matra.Count + ' lines=' + (($sb.ToString() -split "`r?`n").Count - 1))

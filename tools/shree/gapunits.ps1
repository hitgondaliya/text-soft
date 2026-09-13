# gapunits.ps1 — probe text for the handful of characters a table is missing.
#
# The big captures ask Shree's converter for every letter and cluster of a script.
# When a table is later found to be short of a few characters, re-running that is
# hours of GUI driving for a dozen answers. This writes the small probe instead:
# each missing character on its own, and — for a combining mark, which a legacy
# font may only ever encode as part of a cluster — the mark on every consonant.
#
#   gapunits.ps1 -Block 0x0A80 -Chars 0A86,0A8D,0ABC -Out gap-guj.txt
param(
  [int]$Block = 0x0900,
  [Parameter(Mandatory = $true)][string[]]$Chars,
  [string]$Out = 'gap.txt',
  # A unit the layout cannot represent comes back as a blank line, and Shree's
  # output box drops blank lines — so the answers no longer line up with the
  # questions and the whole capture has to be thrown away. Fencing every unit
  # between copies of a letter that always converts makes the run self-syncing:
  # two fences in a row mean "that one produced nothing".
  [string]$Marker = ''
)

# -File passes "0A86,0A8D" as one argument, -Command as several; accept both.
$codes = $Chars | ForEach-Object { $_ -split '[,\s]+' } | Where-Object { $_ } | ForEach-Object { [Convert]::ToInt32($_, 16) }

$cons = @()
for ($c = $Block + 0x15; $c -le $Block + 0x39; $c++) {
  if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory([char]$c) -ne 'OtherNotAssigned') { $cons += [char]$c }
}

# The fence has to be something no probe unit can answer with. A consonant fails
# that test: when a layout cannot represent "ક઼" it answers plain "ક", which is
# the fence itself, and the count comes out one short. The script's digits 1-2-3
# convert cleanly in every layout and no consonant-plus-mark unit produces them.
$fence = if ($Marker) { -join ($Marker -split '[,\s]+' | Where-Object { $_ } | ForEach-Object { [char][Convert]::ToInt32($_, 16) }) }
         else { [string][char]($Block + 0x67) + [string][char]($Block + 0x68) + [string][char]($Block + 0x69) }

$sb = New-Object Text.StringBuilder
$units = @()
function Add-Unit($u) {
  [void]$script:sb.AppendLine($fence)
  [void]$script:sb.AppendLine($u)
  $script:units += $u
}

foreach ($c in $codes) {
  $ch = [char]$c
  Add-Unit ([string]$ch)
  # A mark has no standing of its own: Shree may answer a bare one with nothing,
  # so also ask for it where it actually occurs.
  $cat = [Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch)
  if ($cat -eq 'NonSpacingMark' -or $cat -eq 'SpacingCombiningMark') {
    foreach ($k in $cons) { Add-Unit ([string]$k + [string]$ch) }
  }
}
[void]$sb.AppendLine($fence)   # closing fence

[IO.File]::WriteAllText($Out, $sb.ToString(), [Text.Encoding]::UTF8)
[IO.File]::WriteAllLines(($Out -replace '\.txt$', '') + '.units.txt', $units, (New-Object Text.UTF8Encoding($false)))
Write-Output ('units=' + $units.Count + ' fence=' + $fence + ' -> ' + $Out)

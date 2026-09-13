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
  [string]$Out = 'gap.txt'
)

# -File passes "0A86,0A8D" as one argument, -Command as several; accept both.
$codes = $Chars | ForEach-Object { $_ -split '[,\s]+' } | Where-Object { $_ } | ForEach-Object { [Convert]::ToInt32($_, 16) }

$cons = @()
for ($c = $Block + 0x15; $c -le $Block + 0x39; $c++) {
  if ([Globalization.CharUnicodeInfo]::GetUnicodeCategory([char]$c) -ne 'OtherNotAssigned') { $cons += [char]$c }
}

$sb = New-Object Text.StringBuilder
$units = @()
foreach ($c in $codes) {
  $ch = [char]$c
  [void]$sb.AppendLine([string]$ch)
  $units += [string]$ch
  # A mark has no standing of its own: Shree may answer a bare one with nothing,
  # so also ask for it where it actually occurs.
  $cat = [Globalization.CharUnicodeInfo]::GetUnicodeCategory($ch)
  if ($cat -eq 'NonSpacingMark' -or $cat -eq 'SpacingCombiningMark') {
    foreach ($k in $cons) {
      [void]$sb.AppendLine([string]$k + [string]$ch)
      $units += ([string]$k + [string]$ch)
    }
  }
}

[IO.File]::WriteAllText($Out, $sb.ToString(), [Text.Encoding]::UTF8)
Write-Output ('units=' + $units.Count + ' -> ' + $Out)

---
name: pc-cryptominer-2026-10
description: "Malware found on the dev PC 2026-10-18 (hidden \"Window Update\" task, cmd.exe miner, 2.4 GB) — explains the PC's low RAM; user asked to remove it"
metadata:
  node_type: memory
  type: project
  originSessionId: 7673923b-96b4-4287-87dd-a8e7c6533eca
  modified: 2026-10-17T19:40:03.533Z
---

Found 2026-10-18 while the user asked "why only 318 MB free?": a hidden, elevated
scheduled task `\Window Update` (logon trigger, since 2022) runs
`%LOCALAPPDATA%\Updates\Run.vbs`; the folder holds `WindowsService.exe` (unsigned,
SHA256 5AF9E89A…76BC7), an encrypted `Resource.bin` (rewritten 2026-10-11), and
`updatebackend.log` holding the PID of a hidden `cmd.exe` (2.4 GB private = RandomX
miner size, ~40% of a core, TCP to 31.7.60.154:85 hostedby.privatelayer.com).
Its batch script `Updates\Windows` disables UAC/Defender/SmartScreen and adds
Defender exclusions for .exe, .bat and its own folder. On 2026-10-18 Defender was
running again and UAC was on, Tamper Protection off, exclusions unreadable without admin.

**Why:** it is the main reason the 7.5 GB PC was short of memory during the Shree
table work (see [[shree-7-vs-456-split]]); and anything secret on this PC (licence
signing key, SMTP, VPS logins) may have been exposed.

**How to apply:** if RAM is low again, first check `Get-ScheduledTask -TaskName 'Window Update'`
and the `Updates` folder. Never remove it ourselves (security settings and deletes
are the user's to do); give the user the admin commands.

# App size — why ours is small, and how to keep it that way

## The concern
The reference product (Shree-Lipi NXT Plus) installer is ~1.1 GB. We must not ship anything near that.

## Why Shree-Lipi is 1.1 GB
It bundles *everything for everyone*: hundreds of fonts (84 legacy + Unicode for 12 scripts + extended sets), the full .NET Framework redistributable, device drivers, CHM help for every language, font catalogs, and multiple language packs — all in one installer.

## Why ours is not
Our program is data-driven: languages/fonts/layouts load from `config/` at runtime. **Our own compiled code is ~150 KB.** Everything else in a build is just the .NET runtime.

## Measured build sizes (this app)

| Packaging | Size | End-user needs |
|---|---:|---|
| Self-contained, plain folder | 162 MB | nothing |
| **Self-contained, single-file + compressed** | **~74 MB** | nothing — one `.exe` |
| Framework-dependent | **~0.7 MB** | .NET 8 Desktop Runtime installed (~55 MB one-time) |

Even the largest is **7× smaller** than the reference; the recommended option is **~15× smaller**.

## Recommendation
**Ship self-contained, single-file, compressed (~74 MB).** Zero prerequisites for the user, one executable, clean uninstall. Build:

```bash
dotnet publish src/FontSoftware.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

Ultra-small alternative (~0.7 MB app) = framework-dependent, but then the installer must ensure the .NET 8 Desktop Runtime is present (bundle it, or download on first run).

## Keep it small — rules
1. **Only bundle fonts the client licensed** for the shipped scripts. Each `.ttf` is ~150 KB, so even 50 fonts ≈ 7 MB — fonts are never the problem *if you don't bundle hundreds*.
2. Prefer **Unicode fonts already on Windows** (Mangal, Shruti, …) — zero bytes to ship.
3. Don't bundle help videos / PDF catalogs inside the app; host them on the website instead.
4. Layouts and mapping tables are tiny JSON — negligible.

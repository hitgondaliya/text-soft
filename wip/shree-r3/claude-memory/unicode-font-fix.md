---
name: unicode-font-fix
description: "Why the client saw no Unicode / only one font, and the Nirmala UI fix on the Type screen."
metadata: 
  node_type: memory
  type: project
  originSessionId: 950e994d-182b-4119-bc3b-a8bb7f55e31a
  modified: 2026-09-08T16:51:21.858Z
---

Client complaint (2026-09): font layout showed only one font and "Unicode was missing". Root cause: `languages.json` listed per-script Unicode fonts (Mangal, Vrinda, Shruti, Gautami, Tunga, Kartika, Latha, Raavi, Kalinga) that **do not exist on Windows 10/11** — Microsoft replaced them all with a single font, **Nirmala UI**, covering all 9 Indic scripts. Those dead entries were filtered out by the installed-fonts check, leaving only the bundled Shree legacy font.

Key facts:
- Every keyboard layout JSON already emits **Unicode** (e.g. dev-akruti base = ०,१,म,ो). Layout = key arrangement; output = Unicode. So typing is Unicode app-wide already.
- Fix (Type screen only): `MainViewModel` now always prepends a synthetic `UnicodeFont` (id `unicode`, family `Nirmala UI`) as the default per language; `FontResolver.Fallback` = "Nirmala UI, Segoe UI" so any absent Unicode font still renders Indic glyphs.
- FONT dropdown now shows the FULL supported font list; not-installed fonts are marked "— not installed" (via `FontAsset.AsNotInstalled()`/`Label`, bound in MainWindow.xaml), never hidden. The big scheme list (Akruti/Godrej/Crtronic…) is the KEYBOARD LAYOUT dropdown, not FONT.
- KbdGen (`tools/KbdGen`) generates Unicode keyboard layouts from Shree scheme files via InScript Rosetta decode. Extended its `scripts` array to all 10 (added KAN/MAL/ORI/TAM). Re-run: `dotnet run --project tools/KbdGen -- "C:\Shree-Lipi nxt Plus"`. Now every language has full layouts (Kannada 21, Malayalam 16, Odia 8, Tamil 22).
- Font Layout axis (Shree's top Font-Layout dropdown: Unicode, ShreeLipi-Ex, Shree-Lipi 7.0, Terafont, ISFOC, Anu, STM, BIJOY…) added: `FontLayout` model + `LanguageProfile.FontLayouts`, seeded per language by `tools/LayoutSeed` from client screenshots (source list is `data\SL_FontDetails.enc`, DES-encrypted, so copied from live UI). UI now LANGUAGE | FONT LAYOUT | KEYBOARD LAYOUT + a RENDER/OWN FONT picker. Third-party families are listed but need their .ttf + encoding map to actually type/render — see FONT-LAYOUTS.md at repo root. Assamese/Manipuri/Diacritical are Shree languages not yet in our config.
- Live legacy typing: selecting a legacy Font Layout (ShreeLipi-Ex all scripts; Shree-Lipi 7.0 for hi/mr/sa/gu) makes `TypingEngine` buffer the Unicode word and inject the legacy-encoded bytes via `AssetBundle.CreateConverterToLegacy(encoding)` (reuses the unicode→shree-*x maps; ConvTest 38/38). Keyboard preview forced to a Unicode font for legacy layouts (SelectedFontFamily) so it doesn't tofu. `LegacyTypingActive` flag + status " (legacy)". Third-party families (Terafont/ISFOC/Anu…) still list but type Unicode (no map/font) — status " (font not provided — types Unicode)".
- Shree fonts at `D:\font software`: only `FONTS\Default\*.TTF` are plain TTF; ShreeLipi-Ex (`SLX_####`) and Unicode (`*._TF`) and sl7Fonts are DRM-encrypted — do NOT decrypt/bundle Shree's commercial fonts (copyright/TPM). Legit path: client installs fonts via their licensed Shree; our app uses whatever is installed.
- Converter still uses named unicode endpoint ids (mangal/shruti/...) to match mapping-table `targetFontId`; not changed — separate task. Render fallback fix also helps its output box.
- Shree-Lipi source data lives at `C:\Shree-Lipi nxt Plus`; extraction in [[textsoft-brand]]-adjacent doc SHREE-LIPI-EXTRACTION.md at repo root. `.enc` lists encrypted — see [[converter-encoding-findings]].

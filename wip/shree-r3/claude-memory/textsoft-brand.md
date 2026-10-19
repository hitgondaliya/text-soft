---
name: textsoft-brand
description: "TextSoft app brand — name, tagline, logo, and theme colors"
metadata: 
  node_type: memory
  type: project
  originSessionId: a826f409-bec5-4867-bc5f-3cac51c643da
  modified: 2026-08-26T19:22:20.217Z
---

The Windows typing app is branded **TextSoft**, tagline/headline **"Beyond Typing"**. Window title: "TextSoft — Beyond Typing". (Renamed from the old "Multilingual Typing"; exe is now `TextSoft.exe`, app-data folder `%AppData%\TextSoft`.)

Logo lives at repo root `logo.png` (a TS monogram, orange + charcoal on off-white). `app.ico` is generated from it. Theme colors derived from the logo, defined in `src/FontSoftware.App/App.xaml`:
- Primary (orange) `#EA580C`, hover `#C2410C`, deep-text `#9A3412`, light tint `#FDECE1`
- Sidebar charcoal `#23262B`, ink `#24262B`, warm off-white bg `#F6F5F3`

The website is intentionally kept separate — do NOT change `website/` when working on the app. See [[ui-text-contrast-rule]].

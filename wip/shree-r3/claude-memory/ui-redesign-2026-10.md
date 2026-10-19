---
name: ui-redesign-2026-10
description: TextSoft premium redesign (Oct 2026): dark sidebar, own title bar via WindowChrome, cards; where the pieces live and the traps met.
metadata:
  type: project
---

Client asked for a premium look (logo was cut, sidebar weak). Chosen: dark charcoal sidebar,
brand orange/charcoal kept. Design system in `src/FontSoftware.App/App.xaml`: tokens (Primary,
Sidebar*, Surface, KeyFace…), `Card` (ContentControl; shadow on a separate layer so text stays
crisp), `Switch` (CheckBox), `Segment`+`SegmentBar` (RadioButtons), `Chip`, `Ghost`,
`CaptionButton/CaptionClose`, `IconFont` = "Segoe Fluent Icons, Segoe MDL2 Assets".
MainWindow draws its own title bar (WindowChrome, CaptionHeight 68); maximise inset computed in
`MainWindow.xaml.cs` (SM_CXSIZEFRAME+SM_CXPADDEDBORDER). Caption buttons sit OUTSIDE the
licence-disabled panel and above the lock overlay.
Logo: `Assets/logo-mark(-light).png` cut from logo.png (transparent); `app.ico` = mark on a
white rounded tile (tools: scratchpad/logoprep).
Traps: implicit app-level TextBlock style overrides FontFamily/FontSize of string content inside
templates (icon glyphs showed as boxes) → use a ContentTemplate TextBlock; TextBox template must
not bind PART_ContentHost Margin to Padding (doubles height). `Controls/PageHost` exposes page
content to UI Automation (stock TabControl hid it). Window fits the work area on open (FitToWorkArea; MinHeight 560), maximise inset uses
GetSystemMetricsForDpi(GetDpiForWindow); page heads are DockPanels so subtitles trim; converter
status sits on its own line above Start Over/Convert (beside them it broke letter by letter at
900 DIP). See [[textsoft-brand]] [[ui-text-contrast-rule]].

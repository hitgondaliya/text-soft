---
name: ui-text-contrast-rule
description: WPF text must contrast its background — never invisible text on colored buttons/sidebar
metadata: 
  node_type: memory
  type: feedback
  originSessionId: a826f409-bec5-4867-bc5f-3cac51c643da
  modified: 2026-08-26T19:22:29.019Z
---

The user was burned twice by text rendering invisible: nav labels black-on-navy, and button captions black-on-colored. Both had the same root cause: a global implicit `TextBlock` style in App.xaml that hard-coded `Foreground` to a dark color, which bled into button/nav string content.

**Why:** a hard-coded color on the base `TextBlock` style overrides inheritance everywhere, so white-on-color surfaces silently get dark text.

**How to apply:** the base `TextBlock` style must NOT set `Foreground`. Let text inherit `TextElement.Foreground` from its container — set it once on each region root (dark on light content area, light on the charcoal sidebar), and controls like buttons set their own (white on orange). Rule of thumb: text on Primary/Sidebar → white/OnPrimary; on Surface/AppBg → Text/Muted; on a pale tint (PrimaryLight) → the deep-orange text, never white. Verify by screenshotting the running app, not just building. See [[textsoft-brand]].

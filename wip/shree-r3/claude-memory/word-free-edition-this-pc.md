---
name: word-free-edition-this-pc
description: Word on the dev PC is the free read-only Microsoft 365 edition — nothing can be typed into it, so Word typing cannot be tested here
metadata:
  type: project
---

Found 2026-09-28: Word on this machine shows "ENABLE EDITING — To edit files in this free version, save to OneDrive". Every document is read-only until saved to OneDrive, so no keyboard software (TextSoft, Shree, the plain keyboard) can type into it. SaveAs from automation opens the "Save this file" OneDrive dialog and blocks the COM call.

**Why it matters:** earlier sessions blamed "Document Recovery" / COM registration for Word test failures; the real cause is the free edition. The client's machine is where Word typing gets verified.

**How to apply:** do not claim Word end-to-end results from this PC. Never click "Save to OneDrive" (uploads to the user's account) without explicit permission. The product's Word path (WordAutomation: OBJID_NATIVEOM on the focused _WwG page, font set on the selection) is unit-tested and logic-reviewed only. A WINWORD process with an AutoRecovered document may be left running — do not kill it (it may hold the user's text). See [[live-testing-rules]].

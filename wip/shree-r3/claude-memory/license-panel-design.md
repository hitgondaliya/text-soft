---
name: license-panel-design
description: Licence admin panel UI (rebuilt compact 2026-10-06): design brief, shared components, recoverable keys UI, settings page; client hates bulky layouts.
metadata:
  type: project
---

`server/` UI was rebuilt 2026-09-13 on **Tailwind 3 + shadcn/ui primitives**
(`src/components/ui/*`), **next-themes** (light / dark / system, class strategy),
**lucide-react** icons, **sonner** toasts, Inter + JetBrains Mono via next/font.

- Tokens: `src/app/globals.css` — HSL variables on `:root` and `.dark`, brand
  orange as `--primary`, plus `--success`/`--warning` and a `--sidebar` set. Keep
  the sidebar dark in both themes; that's deliberate.
- Shell: `src/components/app-sidebar.tsx` (desktop rail + mobile Sheet with
  hamburger), `page-header.tsx` (title + actions + theme toggle),
  `status-badge.tsx` (single source for Active/Expired/Revoked and Full/Demo).
- Logo: `server/public/logo.webp` (copied from Downloads) — used in sidebar,
  mobile bar, login brand panel, favicon. `logo.png` (740×592) is also there.
- Purchases create flow is a Dialog (`admin/orders/new-purchase-dialog.tsx`);
  `?new=1` on `/admin/orders` opens it, which the dashboard/keys buttons use.
- Tables hide columns by breakpoint (`hidden md:table-cell`) rather than
  squeezing; serial/key/date cells are `whitespace-nowrap`.
- The user was blunt that the old UI was unacceptable and wanted "modern,
  stunning, responsive, launch-ready" — keep polish high on this surface.

**2026-10-06 rebuild (commit 139b1bb)** after the client called the old one "the worst"
(8 big stat cards wrapping, max-w-7xl with big padding, keys visible only once):
- Brief: content `w-full max-w-[1600px] px-4 py-4 lg:px-6 lg:py-5` left-aligned, sidebar w-60
  grouped (Overview/Sales/Customers/Admin, Settings owner-only), cards border-only with
  px-4 py-3 headers, tables 13px with 36px muted header, stat strip = ONE card of 8 cells,
  one row from 1024 px. Light primary deepened to 21 90% 41% for AA (white text 4.7:1).
- Shared components: stat-strip, section-card, copy-button, secret-key (masked key, reveal,
  copy, "not stored" + Reissue), data-toolbar, kv-list, empty-state, ui/tooltip, ui/tabs, ui/alert.
- Purchase page: Copy all keys, Download .txt, Email keys (resend dialog), reissue dialog.
- Settings page: SMTP form with presets + test + reset, Server card, Activity log (audit).
- Screens at full resolution: scratchpad shots/cdp-shots.mjs (headless Edge over CDP,
  set MSYS_NO_PATHCONV=1 in Git Bash or /admin becomes a Windows path; it kills Edge with
  taskkill /T — leftover headless Edges ate the RAM once and made next build OOM).

Related: [[license-server-purchases]], [[textsoft-brand]], [[license-panel-deploy-2026-10]].

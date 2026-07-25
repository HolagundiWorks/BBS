# WCAG 2.2 accessibility audit — BBS Studio

**Scope:** Win32 desktop UI mapped to WCAG 2.2 Level AA success criteria.  
**Date:** 2026-07-25  
**Build audited:** post feature expansion (aggregated Nos, walls, footing types).

Legend: **Pass** / **Partial** / **Fail** / **N/A**

## Summary

Targeted remediations in this pass: keyboard nav for the painted rail (F6 + arrows), visible focus ring on nav items, stronger text contrast (light/dark), cue banners on form fields, semantic HTML report (`lang`, `main`, `th scope`, captions), larger nav hit targets (44px), Nos column for schedule clarity.

Residual risk: custom GDI+ chrome does not expose a full UIA tree for painted labels/nav glyphs; native EDIT/BUTTON/COMBOBOX/LISTVIEW remain the accessible surface.

## Criteria (mapped)

| Criterion | Result | Notes |
|-----------|--------|-------|
| 1.1.1 Non-text content | Partial | Buttons have text (+ optional glyph). Nav icons accompanied by text labels (except compact rail &lt;960px — labels hidden; rely on tooltips via page title after activation). |
| 1.3.1 Info and relationships | Partial | ListView columns named; form labels painted adjacent + cue banners. No ARIA equivalents for painted cards. |
| 1.4.1 Use of color | Pass | Status uses text (“OK” / message), not color alone. |
| 1.4.3 Contrast (minimum) | Pass* | Primary/secondary/tertiary tokens adjusted to ≥4.5:1 on card/app backgrounds in light mode; dark tertiary raised. Accent-on-accent buttons use `textOnAccent` by luminance. *System accent color may vary — verify if OS accent is very light/dark. |
| 1.4.11 Non-text contrast | Partial | Focus underline/ring uses accent; control borders improved vs prior tertiary. |
| 2.1.1 Keyboard | Pass* | Tab via `IsDialogMessage`; accelerators; F6 + arrows for nav rail; Enter activates. Footer New/Open/Save still mouse-primary (accelerators cover New/Open/Save). |
| 2.1.2 No keyboard trap | Pass | Esc leaves nav keyboard mode; Tab cycles native controls. |
| 2.4.3 Focus order | Pass | Dialog-message tab order follows control creation / Z-order within page. |
| 2.4.7 Focus visible | Pass | Owner-draw buttons draw focus; edits use Fluent underline; nav keyboard mode draws 2px accent ring. |
| 2.5.5 Target size (enhanced) / 2.5.8 Target size (minimum) | Pass | Nav items 44px tall; primary actions ≥36px. |
| 3.2.1 On focus | Pass | Focus does not submit/generate. |
| 3.3.1 / 3.3.2 Labels or instructions | Partial | Visible painted labels + cue banners; generate errors via message boxes with text. |
| 4.1.2 Name, Role, Value | Partial | Native controls expose name/role/value. Painted nav items lack UIA peers (documented residual). |
| HTML export 1.3.1 / 1.4.3 | Pass | `lang="en"`, headings, `th scope="col"`, captions, stronger ink/muted CSS. |

## Remediations implemented

1. `MainWindow`: F6 toggles nav keyboard focus; arrows move; Enter/Space activate; focus ring in `paintNav`.
2. `Theme.cpp`: darker secondary/tertiary in light mode; brighter tertiary in dark mode.
3. `ElementPage`: `EM_SETCUEBANNER` with field label on edits.
4. `Export.cpp`: semantic HTML structure + contrast-safer CSS.
5. BBS grids: **Nos** column (clearer than one row per physical bar).

## Recommended follow-ups

- Add tooltips on compact nav glyphs (`TTM_ADDTOOL`).
- Expose New/Open/Save as real `BUTTON`s for UIA (or keyboard shortcuts already present).
- Optional: MSAA/UIA provider for the nav rail.
- Automated contrast check against resolved system accent.

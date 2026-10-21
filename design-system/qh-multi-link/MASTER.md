# QH Multi-Link — Windows desktop design

Based on ui-ux-pro-max searches: `desktop utility dashboard` (Minimalism & Swiss Style) and `navigation forms accessibility --stack winui` (keyboard navigation, semantic headings).
The search's marketing hero/CTA pattern is not used: this is a desktop utility. Its verified minimal style, blue/slate palette and hierarchy apply. Use Segoe UI Variable as the native Windows typography adaptation; no remote fonts.

- Framer reference (21/10/2026): light canvas #F5F7FB, sidebar #EFF0F4, white cards, text #191D28, secondary #535A6B, borders #DFE2E9.
- Light accent #2864DF / white. Dark canvas #0C0E17, sidebar #05060B, cards #1D1F28, ink #F1F2F8, muted #B5B6C2, accent #9B8CE2 / dark ink; shared selection #453D67.
- Semantic brushes in App.xaml; explicit Windows high-contrast mappings.
- Spacing in multiples of 4/8; 20–24px card padding, 10px card corners, 20px page headings and 16px section headings. Simplified heading surfaces follow the newer Framer reference.
- Left sidebar: three native ToggleButtons, one shared rounded selection indicator, quick-window shortcut and native theme switch in the footer. Main window has no compact mode; quick creation lives in an independent floating Window.
- Native focus, hover, pressed and disabled states. Main buttons min 44px. Shared indicator moves over 240ms; content fades/slides 140–240ms. Stop old transitions on rapid input; use final static states for OS reduced motion/high contrast.
- Main desktop minimum 620×480, initial 1100×780 physical px. Two-column form/results above 1040 logical px, stacked below; icon sidebar below 760. Independent edge window keeps 281×87 logical pixels, tab/loading 44×52.
- Vietnamese visible labels and errors; status uses words as well as visual hierarchy.
- No fake activity, sample links, service health claims or fabricated success metrics.
- API keys never rendered after saving. Password input revealed only when enabled and cleared after each run.
- Results stream independently; cancellation preserves completed results.

QA: compile XAML, run tests for coordination/privacy/API contracts, inspect native UI at default and narrow desktop sizes when computer access is available. Mobile checklist items do not apply to Windows-only software.

## Appearance and fast creation update

- Default workflow: paste URL → Enter/Create using the saved preferred provider (initial spoo.me). Main multi-provider selection and password are behind an expander. Edge window progressively reveals optional password after a valid URL. No per-run provider confirmation.
- Explanatory hints belong in native tooltips; keyboard help/focus is retained. Validation errors stay visible. Skill searches used `tooltip keyboard focus --domain ux` (Focus States) and `animation --stack winui` (native progress controls). Reduced-motion media behavior follows Microsoft's native BitmapImage guidance.
- Page and section headings follow the simple Framer hierarchy. Theme-specific form/result surfaces preserve contrast on arbitrary backgrounds.
- User-selectable RGB background color; local GIF/MP4 loops limited to 15 seconds, 20 MiB, Full HD, GIF 450 frames. MP4 is muted, no playback timeline controls.
- Pause/play is reachable from the header. Background playback pauses on deactivation and is disabled under OS reduced motion/high contrast.

## Provider connections

- Dynamic provider catalog; distinguish missing credentials with text, never a fabricated health indicator. Native disabled states apply to unavailable multi-provider checkboxes.
- API key form selects one connection, keeps visible labels and reveals the domain field only for Short.io. Retain drafts per service when switching, clear only successfully saved drafts, and keep saved keys hidden. A nearby save button and live status distinguish unsaved changes from encrypted saved credentials.
- Skill query `disabled control explanation --domain ux` matched Interaction / Disabled States; apply native disabled styling plus a readable reason.

## Edge window

Separate native top-level Window, no title bar or resize handles. White surface, blue tab/action, Fluent glyphs and native focus/hover states. Input, busy, result and recoverable error states fit the sketch. Two-row 87px layout deliberately uses 32px control height, full keyboard operation and named icon actions; the tab remains 44px wide. Do not add a marketing header or explanatory paragraphs to this utility.

Settings has a native labeled ToggleSwitch for enabling the edge window, with immediate application/persistence and existing rounded section styling. No verified ToggleSwitch-specific match was found in the skill searches; this uses the established native settings controls as a fallback. Turning it off hides the entire auxiliary window and disables its main-window shortcut.

Skill `floating desktop utility compact --design-system` matched Minimalism & Swiss Style; its marketing Hero/CTA and remote font suggestions do not apply here. Continue the project's Segoe UI and established palette. `window presenter --stack winui` matched multi-window lifecycle and separate new Window guidance. No custom transition animation or changes to OS motion settings.

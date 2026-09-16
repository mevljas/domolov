# Adopt MudBlazor for the operator UI

## Status

Accepted

## Context

Domolov’s Blazor UI shipped on Bootstrap 5 utilities and the default Blazor template shell. That stack made dense operator workflows (Watches, ScanRuns, listing grids, confirms) slow to polish and left template leftovers (purple sidebar, About link). We needed a coherent light “forest & paper” design system with dialogs, tables, drawers, and snackbars without inventing every primitive.

Alternatives considered:

- Keep Bootstrap and hand-roll components — lower dependency cost, higher ongoing UX cost.
- Fluent UI Blazor — Microsoft-aligned look, less distinctive for a home-hunting product.
- MudBlazor — mature Blazor component set with theming, dialogs, and tables that fit Interactive Server.

## Decision

Use MudBlazor (v9+) as the primary UI library. Remove Bootstrap from the app shell. Theme via a custom `MudTheme` (forest green primary, paper background, sage drawer). Apply `InteractiveServer` at the `Routes` root so Mud layout providers (drawer, dialogs, snackbars) work — Blazor cannot serialize a layout `Body` RenderFragment when the layout alone is interactive. Keep cookie auth HTML form contracts for E2E (`POST /auth/login`, `/auth/logout`, antiforgery, `data-enhance="false"`). Keep the custom canvas price chart rather than adding a chart library.

## Consequences

- UI code depends on MudBlazor package updates and Material icon set.
- Login remains mostly plain HTML/CSS so it can stay SSR-friendly and E2E-stable.
- Scoped CSS and Bootstrap utility classes on older components must be migrated as pages are touched.

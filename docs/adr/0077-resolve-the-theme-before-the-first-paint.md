# 0077 — Resolve the theme before the first paint

- **Status:** Accepted
- **Date:** 2026-08-13

## Context

The dark theme was chosen in the browser and applied by C#. `MainLayout` read the stored choice in
`OnAfterRenderAsync`, fell back to `MudThemeProvider.GetSystemDarkModeAsync`, and set its flag. That
is the earliest a component can ask, and it is far too late.

ADR 0072 made the catalog's routes prerender, and ADR 0074 made the root one of them. So the server
now sends a complete, painted page — with the light palette, because nothing on the server knows
what the visitor chose — and the browser shows it immediately. The correction cannot happen until
the WebAssembly runtime and the application's assemblies have been fetched and started. On the three
most public pages of the product, a visitor who asked for dark gets a full-page white flash lasting
the whole time-to-interactive, not a frame.

Two smaller things were wrong with the palette itself. The dark ground was bottle green
(`#14201A`, `#1B2A22`) — dark, but read as a color scheme rather than as dark, and its surface sat
so close to its background that a row of the shelf lost its edge. And the six topic hues that are
the design's signature (ADR 0069) claimed in a comment to work on both grounds; measured, three of
the six fell below the 3:1 a non-text mark owes a reader on that green surface.

Finally, MudBlazor ships **no** palette in its stylesheet. Every `--mud-palette-*` variable is
written by `MudThemeProvider` into a `:root` block it renders as a component — which is why the
prerendered HTML carries a full light palette and why a stylesheet of ours can outrank it.

## Decision

**The theme is resolved in the document's head, before the first paint, and C# reads the answer
rather than deciding it a second time.**

- **One resolver, and it is a script in `<head>`.** It reads `localStorage`, falls back to
  `prefers-color-scheme`, and stamps `data-theme="dark|light"` on the document element. It runs
  before any stylesheet has painted anything, on prerendered and non-prerendered routes alike, and
  it is wrapped in a `try` because a browser that refuses storage must still get a page.
- **`app.css` paints from the attribute.** A `:root[data-theme="dark"]` block declares the dark
  palette. Specificity, not source order, is what makes it win over the `:root` the provider
  renders — so it holds wherever in the document that block lands.
- **The layout reads the attribute back.** `MainLayout` asks the document what it is already
  wearing, so `_isDarkMode` starts true and MudBlazor's own variables agree with the sheet's. The
  toggle writes **both** the storage and the attribute; writing only the storage would leave the
  dark block winning and a visitor who asked for light stuck in the dark.
- **The dark ground becomes neutral, and near-black.** `#0E0F10` under `#191B1D`, ink `#F2F3F4`,
  green kept for the actions and brass for the quiet seconds. A tinted dark theme reads as a color
  scheme; the hue that survives is the one doing work.
- **The palette's own contrast defects go with it.** The avatar's initials and a filled chip's
  label were white over brass and over green; both are ink now. The numbers are under
  *Consequences*.

### What was turned down

- **A cookie the server reads.** It would let the prerendered pass paint correctly, and it fails on
  the case that matters most: a first-time visitor has no cookie, and the server cannot know their
  system preference. It also moves a preference of the device into every request, against the
  argument that put the choice in `localStorage` in the first place (ADR 0009 took the credential
  out of it; a color belongs in it). The script gets both answers and costs the server nothing.
- **Following `prefers-color-scheme` live.** Out of scope, and worth naming because the code
  claimed to do it: a comment said the visitor "follows their device when it changes its mind"
  while `ObserveSystemDarkModeChange` was never set. The claim is removed rather than implemented.

## Consequences

- **The dark palette is written twice, and the duplication is the price of the first paint.** The
  sheet must know the palette before C# exists; C# must know it because MudBlazor derives the rest
  of the theme from it. `TheChosenTheme_IsResolvedBeforeTheFirstPaint` compares the two copies value
  by value, in both directions — a value that differs is a flicker, and a variable painted by only
  one of them is a color the page abandons a beat after showing it.
- **The three variables the first paint cannot do without** — background, surface, text-primary —
  are required by the same rule. The rest of the block is what the catalog's own pages paint with:
  chips, input outlines, dividers, the default action ink.
- **One set of topic hues now reads on both grounds, measured rather than asserted:** worst pairing
  3.18:1 (marketing on the dark surface), best 5.29:1. It became true because the ground stopped
  being green — the comment that claimed it was already true is now correct, and says what it was
  measured against.
- **The avatar's initials moved from white on brass (2.35:1) to ink on brass** — 5.83:1 by day,
  8.74:1 by night. A filled chip's label moved the same way, white over the dark theme's green
  reading 3.34:1 against the ink's 6.59:1, which is why `PrimaryContrastText` is set rather than
  left at MudBlazor's white.
- A visitor with JavaScript disabled gets the light palette and a working page. The resolver is
  three lines of behavior with no dependency, so there is nothing else to fall back to.

## Verification

- `TheChosenTheme_IsResolvedBeforeTheFirstPaint` was seen red five times before being green: with
  the resolver's `prefers-color-scheme` fallback removed, with the layout naming
  `GetSystemDarkModeAsync` again, with the two copies disagreeing on the ground, with a variable
  painted by the sheet alone, and with the surface no longer declared.
- The catalog's front door was loaded with `theme=dark` stored, and the body's computed background
  read at `domcontentloaded` — before the boot — is the dark ground rather than the light one.
- Both palettes were captured on the running stack: the front door, a training's page and a
  trainer's profile, in both modes.

# Plotter — Brand guidelines

This is the working reference for designers, writers and developers. The name and identity are proposals for review, not evidence of trademark clearance. See [strategy](strategy.md), [positioning](positioning.md), [voice](voice.md), [messaging](messaging.md), [campaign kit](campaign-kit.md) and [naming](naming.md) for detail.

## Foundation

**Essence:** A little city. A lot of clever choices.  
**Mission:** Give players an inviting city, clear choices and just enough rivalry to make every placement matter.  
**Personality:** Warm, tactile, clever, welcoming and quietly competitive.  
**Position:** Competitive card-driven city building on one shared board.

## Logo system

The editable vector masters in `assets/` use a drawn plot-corner mark and a custom geometric wordmark. `plotter-logo-ink.svg` is the primary horizontal lockup on light backgrounds; `plotter-logo-paper.svg` is the reversed version for deep ink; `plotter-mark.svg` is the standalone icon for compact uses. The logo is a draft creative asset, not an engine integration.

Keep clear space at least equal to the height of the mark's small square around the lockup. Start testing the full lockup at 180 px wide and the mark at 32 px; increase either if the output medium loses detail. These are practical starting points, not claims of tested legibility on every display. Preserve aspect ratio. Do not stretch, outline, bevel, add shadows, change individual letter colours or place the mark over a busy image without a solid backing.

## Colour

| Role | Name | Hex | RGB |
| --- | --- | --- | --- |
| Light background | Paper | #F2E8D3 | 242, 232, 211 |
| Text, linework, dark ground | Ink | #293A3B | 41, 58, 59 |
| Brand emphasis | Brick | #B94F3E | 185, 79, 62 |
| Secondary | City blue | #557D88 | 85, 125, 136 |
| Accent | Mustard | #D5A83D | 213, 168, 61 |
| Landscape | Park | #71855B | 113, 133, 91 |

Use paper and ink as the base. Use brick for the signature and blue, mustard and green for accents and illustrations. Colours should not be the sole carrier of gameplay information. CMYK and spot inks require printer-specific proofing; do not infer production ink specifications from screen hex values.

The following solid-colour ratios were calculated from the hex values (actual rendering, opacity and texture may change the result):

| Foreground / background | Ratio | Use for normal text at 4.5:1 |
| --- | --- | --- |
| Ink / paper, or paper / ink | 9.79:1 | Pass |
| White / brick | 4.94:1 | Pass |
| Ink / mustard | 5.39:1 | Pass |
| Paper / brick | 4.06:1 | Fails |
| Paper / city blue | 3.69:1 | Fails |
| Paper / park | 3.31:1 | Fails |

Use the passing pairs for small text and check rendered controls, focus states and disabled states separately. White here is `#FFFFFF`, not paper. Large text and non-text elements have different criteria; the table is not a substitute for an accessibility review.

## Typography

The logo is drawn geometry, independent of fonts. For a working application system, use **Atkinson Hyperlegible** for interface and rules and **Alfa Slab One** for occasional display headlines, subject to obtaining the fonts and testing engine rendering. The referenced distributions of [Atkinson Hyperlegible](https://raw.githubusercontent.com/google/fonts/main/ofl/atkinsonhyperlegible/OFL.txt) and [Alfa Slab One](https://raw.githubusercontent.com/google/fonts/main/ofl/alfaslabone/OFL.txt) use SIL OFL 1.1; include the relevant copyright and licence notices when redistributing, and check the reserved-name conditions before modifying a font. Neither font is bundled. Fallbacks: `Arial, sans-serif` for text and `Georgia, serif` for display.

Use sentence case. Start with 16–18 px body text and 1.4–1.5 line height in web references; scale to the actual display and game settings. Rules, disabled states and text scaling must be checked in the running game.

## Image and interface

Show distinct buildings, parks and roads on one shared board. Use a consistent viewpoint, broad shapes, ink outlines and subtle paper or print texture. Depict player ownership with labels or shapes as well as colour. Cards should retain their actual text, tags and timing; illustration never overrides rules.

Avoid neon, polished plastic, reward-burst effects, arbitrary ornamental details and tiny text embedded in art. Use screenshots of the real game where possible, with any conceptual visual clearly labelled as a mockup.

## Voice and messages

Sound like a friendly host who enjoys a good contest. Lead with the decision, then explain the result. Say “Choose a card or pass,” not “Grab your amazing reward!” Do not invent claims or treat playtest targets as proven facts.

**Working tagline:** One city. Plenty of plans.  
**Short description:** A competitive card-driven city builder where everyone makes their mark on one shared board.

## Applications

- **Game tile:** Use the standalone mark or full wordmark over a simple shared-city composition; test it at actual listing size.
- **Store page:** Lead with the shared city and actual screenshots, followed by the market and placement choices. Check all feature claims against the shipped build.
- **Main menu:** Let the city remain visible; keep navigation calm, high contrast and keyboard/controller accessible.
- **Cards:** Consistent zones for name, art and exact rules; use readable text alongside icons.
- **Social:** Show a specific board moment with a concise caption about a real choice. Use the logo without crowding the image.

## Files and ownership

`brand/` is the working source of truth in the game repo. `assets/` holds vector masters and `preview.html` is a visual concept for review. The project owner decides final name and visual approvals. Before release, resolve naming clearance, test rendering and contrast, include notices for any fonts actually distributed, and replace conceptual examples with verified game capture where appropriate.

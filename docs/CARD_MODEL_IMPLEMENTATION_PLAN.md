# Card model implementation plan

## Goal

Replace shared building stand-ins with readable board visuals based on the supplied Kenney City Kit packs. Use the proposed selections in [the card model preview](../../models/card-model-preview.html) as the starting point; review them in the game before treating them as final. Keep a conspicuous coloured placeholder for missing assets and retain the card name in the board inspector. Event cards do not need a building model.

## 1. Audit and prepare

- Inventory the selected GLB and FBX files, their texture dependencies and each pack's licence. Preserve source files and licence notices; the commercial pack identifies itself as CC0, but verify each other pack separately.
- Record card ID, selected source, licence, intended scale, orientation, tint and whether the choice is final, composite or a placeholder. Include all 48 drawable cards, with events explicitly marked as non-building cards.
- Inspect the existing `street_water.vmdl` and water material in the editor and in-game. If the tile fits the Lake, use it; otherwise retain the blue lake placeholder until a suitable water asset is made.

**Done when:** every card has a documented visual disposition and the selected files and licences have been checked.

## 2. Prove the s&box asset pipeline

- Start with one suburban house and one commercial building, including their bundled colour-map textures. Copy source assets into a clearly named project asset directory, preserving original names and attribution. Do not modify the original packs.
- Use the s&box editor's asset import and ModelDoc tools to create and compile `.vmdl` assets from the source models. Try the bundled FBX first, and evaluate GLB if the editor supports it; confirm the working format rather than assuming either imports correctly. Existing project `.vmdl` files and material remaps are useful references.
- Import the texture images and create s&box `.vmat` materials, mapping the source material slots to the correct colour map. Test the default texture and any desired variation; verify UVs, colour, orientation and material paths after compilation. Avoid relying on absolute paths or files outside the project at runtime.
- Determine and document the conversion scale, axis orientation, pivot, footprint, rotation and collision requirements. Fit each test asset within the 50-unit board cell and confirm its ground contact and road-facing direction. Keep collision disabled or simple unless gameplay actually requires it.
- Verify that assets compile and load in a clean game session, not just the editor preview. Record exact editor steps and resulting project paths before converting the rest. Use editor-supported tools for asset authoring; do not hand-edit asset documents while the editor is active.

**Done when:** both test buildings render with correct textures and alignment in an actual match, and the repeatable import procedure is documented. If import or texture mapping fails, investigate that before bulk conversion.

## 3. Convert selected assets in batches

1. Residential and commerce, including parasol and awning pieces.
2. Industrial and civic buildings, including containers, tanks and solar panels.
3. Green-space components, trees and paths; test Lake water separately.
4. Landmark and legendary choices, including the skyscraper.
5. Road pieces only after testing their compatibility with the existing road connection and rotation logic.

For each batch, import only the selected models and required textures, compile them, check their materials and compare the result with the preview. Compose multi-piece tiles with consistent placement and transforms. Keep source provenance and any substitutions in the mapping. Where a proposed stand-in is visually misleading, use the missing-model fallback instead.

**Done when:** each selected asset loads reliably, fits a cell, and remains recognisable from the normal camera distance.

## 4. Connect cards to visuals

- Introduce a card-ID-to-visual mapping separate from rules, including model path or composite, scale, offset, facing and optional growth state. Keep deterministic visual choices independent of gameplay randomness.
- Replace generic House/Shop/Forest choices only where a verified asset is available. Preserve road reshaping, owner/tag highlighting, placement animation, inspector names, replacement cleanup and Sapling growth.
- Use a clearly coloured cube or simple silhouette for unmapped or failed assets. Make it visually distinct from normal pieces and retain the actual card name in the inspector. Log missing or failed mappings once; ensure a missing asset cannot prevent placement or break a match. Lake can retain its blue flat placeholder if the water tile does not work.
- Mark Stadium, Fountain, Monument, Senate and other weak fits as requiring bespoke art unless a suitable asset is found. Land Claim retains a temporary marker; Grant, Ribbon Cutting, Rush Hour and Renovation need no separate building model.

**Done when:** every placeable card has a deliberate visual or fallback, and no rules or network state depend on model availability.

## 5. Verify in game

- Exercise every placeable card in a test scene or match, including multiple owners, replacement, save/restore or replica rebuild, and mature Sapling. Check for missing materials, wrong orientation, clipping, z-fighting and incorrect footprint.
- Test roads at ends, curves, junctions and board edges; verify existing connectivity and traffic still work. Check composite pieces and water at normal and zoomed camera distances.
- Verify colour and selection feedback remain legible, the inspector identifies similar buildings, and placeholders are unmistakable. Check performance with a representative full board and compare against the existing scene.
- Run the project build and existing rules suite; visually check at common resolutions. Update the preview and asset mapping to reflect actual choices, deviations and remaining art gaps.

**Done when:** the board is readable, all visuals survive placement and replacement, and any remaining temporary art is explicitly listed.

## Deliverables

- Imported source models, textures and compiled s&box assets with licence provenance.
- A maintainable card-to-visual mapping and safe missing-model fallback.
- Updated preview and import notes, including a list of bespoke assets still needed.
- A verified in-game board without changes to card rules.

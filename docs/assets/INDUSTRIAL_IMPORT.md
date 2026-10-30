# Industrial card model imports

Source: Kenney City Kit Industrial 2.0, CC0; the original license and FBX source files are retained in `assets/raw/kenney/industrial/`. The source pack's colour atlas is isolated at `assets/raw/kenney/industrial/colormap.png`; all outputs use `materials/kenney/industrial.vmat`.

Each model is imported at scale 1.0 (s&box units are inches). Actual bounds and card-specific normalization must be determined from the runtime model; this import does not bake tile placement or composite positioning. These are visual render meshes, without authored collision.

| Card | Industrial model pieces | Notes |
| --- | --- | --- |
| Farm | building-i | Temporary farm; needs farm details and suburban planter. |
| Factory | building-e | |
| Airport | building-g | Temporary terminal; needs airport details. |
| Warehouse | building-j | |
| Builders’ Yard | building-h, shipping-container-a | Composite requires placement. |
| Tax Office | building-a | Office stand-in. |
| Depot | building-s | |
| Granary | building-k, detail-tank-large | Composite requires placement. |
| Industrial Estate | building-q | |
| Eco Council | building-t, solar-panel-flat | Composite requires placement. |
| Contractor | building-p | |

Model paths follow `models/kenney/industrial/<piece>.vmdl`. The previews are proposals; missing dedicated art remains a gap. In particular, these stand-ins do not provide airport or farm-specific details. Do not substitute an unrelated industrial mesh for other missing cards.

## Verification

All 14 models and the material compiled successfully in engine 26.10.27c; each runtime load reported `IsError: false`, the industrial material and positive bounds. The factory, container and solar panel thumbnails showed the expected geometry and atlas colours. These are asset previews; camera framing and composite placement remain to be verified in game.

| Piece | Bounds size X × Y × Z (units) | Minimum Z |
| --- | --- | --- |
| building-a | 124.2 × 208.36 × 147.0 | 0.0 |
| building-e | 129.0 × 168.36 × 165.0 | 0.0 |
| building-g | 128.36 × 167.81 × 128.0 | -0.0 |
| building-h | 131.0 × 132.17 × 73.35 | -0.0 |
| building-i | 130.0 × 102.81 × 73.35 | 0.0 |
| building-j | 130.0 × 102.66 × 86.11 | 0.0 |
| building-k | 91.36 × 130.29 × 77.27 | 0.0 |
| building-p | 99.2 × 168.36 × 71.46 | 0.0 |
| building-q | 176.99 × 214.0 × 88.0 | 0.0 |
| building-s | 91.63 × 212.0 × 83.68 | 0.0 |
| building-t | 139.0 × 172.17 × 101.46 | 0.0 |
| detail-tank-large | 164.84 × 150.84 × 96.16 | -0.0 |
| shipping-container-a | 82.26 × 37.26 × 34.81 | 0.0 |
| solar-panel-flat | 71.34 × 41.82 × 6.15 | 0.0 |

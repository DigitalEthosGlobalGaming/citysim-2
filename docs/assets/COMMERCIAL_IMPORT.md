# Commercial card model import

Source: Kenney City Kit Commercial 2.1, bundled at `models/kenney_city-kit-commercial_2.1`; the original license and FBX sources are copied to `Assets/raw/kenney/commercial/`. The shared source atlas is `colormap.png`; `Assets/materials/kenney/commercial.vmat` uses that atlas. The supplied OBJ material files list one colormap material per selected mesh. This import handles these single-atlas static models only.

All model paths below are relative to `Assets/models/kenney/commercial/` and end in `.vmdl`. They were imported at scale 1. Bounds are X × Y × Z in s&box units (inches), rounded to two decimals; these are raw model dimensions, not a fitted board scale.

| Card mapping | Model | Bounds |
|---|---|---|
| Startup | building-b | 94 × 97 × 129.30 |
| Shop | building-c | 109 × 88.36 × 89.30 |
| Cafe | building-d + detail-parasol-a | 90 × 84 × 129.30; 40 × 34.64 × 45 |
| Tourist Office; Museum | building-e | 100.79 × 164 × 89.30 |
| Housing Authority | building-f | 103 × 84 × 169.30 |
| Architect’s Studio | building-h | 100.79 × 88.36 × 129.30 |
| Bank | building-i | 130.20 × 124 × 168 |
| Apartment | building-j | 134 × 208.36 × 169.30 |
| Market; School | building-k (+ detail-awning-wide for Market) | 94.20 × 208.36 × 147; awning 14.79 × 80 × 40 |
| Chamber of Commerce | building-l | 140.20 × 137 × 227 |
| Hospital | building-m | 124.20 × 124 × 315 |
| Town Hall; Central Station | building-n | 182 × 232 × 248 |
| Skyscraper | building-skyscraper-a | 136 × 136 × 288 |

On engine 26.10.27c, each of the 14 newly imported models and its material compiled successfully, loaded with `IsError` false, positive bounds and the commercial material path. Building-c was previously verified using the same pipeline. Editor thumbnails for building-j, the parasol and wide awning show the expected atlas colours; building-c was visually verified previously. The other models have not individually been visually inspected. Scene placement, front orientation, board-scale fitting and game-camera appearance remain to be verified during integration. Models have no authored collision. Cafe and Market pieces are separate models and require placement. Museum needs a distinctive facade; Hospital needs medical signage; Town Hall, School and Central Station are stand-ins, with station features still needed. No scenes or game code were modified by this import.

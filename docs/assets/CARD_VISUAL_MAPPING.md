# Card visual mapping

Paths below are relative to `models/kenney/` with `.vmdl` appended. Imported models use their pack atlas with white tint; each part is fitted from its runtime bounds to the 50-unit cell. Composites are parented to the animated root. Events do not place buildings. The cube makes missing or failed models conspicuous; the inspector retains the card name. Visuals are deterministic and do not use match RNG.

| Card | ID | Visual |
| --- | --- | --- |
| Street | `street` | Connected Kenney road meshes; see road import notes |
| House | `house` | suburban/building-type-a |
| Cottage | `cottage` | suburban/building-type-g |
| Shop | `shop` | commercial/building-c |
| Farm | `farm` | Procedural red barn, pitched roof, doors and planted crop rows |
| Park | `park` | suburban/tree-large + suburban/tree-small + suburban/path-stones-long |
| Wild Garden | `wild-garden` | suburban/planter + suburban/tree-small + suburban/path-stones-messy |
| Grant | `grant` | Event; no model |
| Avenue | `avenue` | Connected Kenney road meshes, blue tint and lined crossroads |
| Roundabout | `roundabout` | Connected Kenney road meshes with green roundabout ring |
| Orchard | `orchard` | suburban/tree-large + suburban/tree-large + suburban/planter |
| Lake | `lake` | Blue flat placeholder pending water validation |
| Vineyard | `vineyard` | suburban/building-type-i + suburban/planter |
| Startup | `startup` | commercial/building-b |
| Market | `market` | commercial/building-k + commercial/detail-awning-wide |
| Contractor | `contractor` | industrial/building-p |
| Apartment | `apartment` | commercial/building-j |
| Cafe | `cafe` | commercial/building-d + commercial/detail-parasol-a |
| Fountain | `fountain` | Magenta missing-art cube |
| Factory | `factory` | industrial/building-e |
| Sapling | `sapling` | suburban/tree-small → forest: two large trees + small tree |
| Bank | `bank` | commercial/building-i |
| Town Hall | `town-hall` | commercial/building-n |
| Museum | `museum` | commercial/building-e |
| Chamber of Commerce | `chamber-of-commerce` | commercial/building-l |
| Warehouse | `warehouse` | industrial/building-j |
| Builders' Yard | `builders-yard` | industrial/building-h + industrial/shipping-container-a |
| Hospital | `hospital` | commercial/building-m |
| Tourist Office | `tourist-office` | commercial/building-e |
| Tax Office | `tax-office` | industrial/building-a |
| Depot | `depot` | industrial/building-s |
| School | `school` | commercial/building-k |
| Granary | `granary` | industrial/building-k + industrial/detail-tank-large |
| Stadium | `stadium` | Magenta missing-art cube |
| Airport | `airport` | industrial/building-g |
| Skyscraper | `skyscraper` | commercial/building-skyscraper-a |
| Industrial Estate | `industrial-estate` | industrial/building-q |
| Housing Authority | `housing-authority` | commercial/building-f |
| Eco Council | `eco-council` | industrial/building-t + industrial/solar-panel-flat |
| Gardener's Guild | `gardeners-guild` | suburban/building-type-i + suburban/planter |
| Architect's Studio | `architects-studio` | commercial/building-h |
| Renovation | `renovation` | Event; no model |
| Ribbon Cutting | `ribbon-cutting` | Event; no model |
| Rush Hour | `rush-hour` | Event; no model |
| Land Claim | `land-claim` | Reservation marker |
| Central Station | `central-station` | commercial/building-n |
| Senate | `senate` | Magenta missing-art cube |
| Monument | `monument` | Magenta missing-art cube |

Stand-in art still needs dedicated details for Farm, Orchard fruit, Vineyard vine rows, Airport, Museum, Hospital signage, School, Town Hall, Central Station and Tax Office. Stadium, Fountain, Senate and Monument need bespoke art. Lake needs suitable validated water. Composite placement, model orientation and proportions require in-game visual review.

## Runtime verification

On engine 26.10.27c, the temporary play-mode gallery rendered all 43 placeable cards, including composites and fallback cubes. The gallery is captured in `city_sim/.build/card-model-gallery.png`; twelve road end, bend and T rotations with connected neighbours appear in `city_sim/.build/card-model-road-check.png`. Native checks confirmed a two-part Cafe, enabled child renderer under a disabled root renderer, Sapling growth to three trees, removal of the replaced Cafe root, a Shop replacement, and the conspicuous missing-model fallback. The actual Shop ghost had its imported child renderer enabled. Water inspection found the existing street-water asset unsuitable; the blue Lake fallback remains. The gallery is a visual review, not a bespoke-art signoff; facade directions and composite details remain candidates for refinement.

Visual revision: building and greenery assemblies are 20% smaller within cells; roads retain full cell coverage. Source road bend/T/end rotations are corrected for Kenney axes, and rebuilt mesh children explicitly inherit their tile rotation. Farm now uses a dedicated procedural barn and crop rows instead of an industrial stand-in. A connected loop with branches was visually verified after repeated placements.

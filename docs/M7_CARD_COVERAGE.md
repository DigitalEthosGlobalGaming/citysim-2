# M7 card coverage

Source: GDD v0.8 §5.2 and `code/Cards/CardLibrary.cs`. The section names 48 distinct drawable cards; Forest is the mature Sapling state, not a separate draw. “Present” means a catalogue entry exists, not that every interaction has been independently validated.

| Group | Present in catalogue | Remaining |
|---|---|---|
| Roads | Street, Avenue, Roundabout | — |
| Residential | House, Cottage, Apartment | — |
| Commerce | Shop, Cafe, Market | — |
| Industry | Farm, Orchard, Factory | — |
| Green | Park, Wild Garden, Fountain, Lake | — |
| Growth | Sapling, Vineyard, Startup | — |
| Landmarks | Stadium, Airport, Skyscraper | — |
| Civic | Warehouse, Builders' Yard, Bank, Town Hall, Tourist Office, Tax Office, Depot, School, Hospital, Granary | — |
| Tag engines | Chamber of Commerce, Industrial Estate, Housing Authority, Museum, Eco Council | — |
| Triggers | Gardener's Guild, Architect's Studio, Contractor | — |
| Legendary | Monument, Central Station, Senate | — |
| Economy events | Grant, Land Claim, Ribbon Cutting, Rush Hour, Renovation | — |

All 48 named cards are implemented. The M7 rule checkpoint passes 486 headless checks, including 175 M7 card checks and 28 bot checks. Builder and Renovation checks also pass inside s&box. Input and presentation verification is still being completed; catalogue coverage alone does not close M7.

## Choice and replacement conventions

- Builders’ Yard stores the owner's choice for the next round, initially Street. The compact effect tray exposes Street/Common; the choice is owner-filtered over the network, authenticated, checkpointed and copied into the bot's own evaluation. Common draws use the saved host RNG only when the hand has room. Warehouse and Yard bonuses follow the ordinary defaults in source placement order.
- Hospital now chooses one residential emergency at mid-Build, or before early Build completion. The nearest Hospital within the card's range answers; placement order breaks ties. Schedule, attempt guard and RNG state survive restore. A native timed response and one-VP feedback cue were observed.
- Renovation arms its event card, then takes a different building card and an owned non-road target. Pure validation temporarily excludes the old building. Successful commit consumes two cards, retains prior earned VP, assigns a fresh placement order/age, applies ordinary placement effects, removes old persistent effects and emits a replacement before rendering the new building. Committed building/trigger history survives replacement and checkpoint restore. Both slots share an authenticated hand version; stale, rival and replayed requests are rejected.
- Native verification replaced Bank with House through card callbacks and injected Confirm: the old model was destroyed, both cards consumed, armed state cleared, empty-cell count unchanged, ground restored, and the new model settled at scale 0.25. The Yard's Common control was activated and rendered toggled. Physical controller evidence remains separate.

## Rendering coverage

- Roads use road models; residential uses house, green and growth use forest, and remaining buildings default to shop. Names and tags remain in model state, but many distinct buildings currently share silhouettes.
- BoardView supplies procedural farm, factory, civic, Lake, Startup, Stadium, Airport and Skyscraper silhouettes. All 19 M7 additions were placed through native rules; their scene names and settled 0.25 scale were checked. A 1080p capture is saved at `.build/m7-native-cards.png`. These are placeholders, not final art.
- The existing forest model maps its tree material to `models/rust_props/portacabins/portacabin_plywood.vmat`. Dedicated art and a material pass remain; no source assets were hand-edited.

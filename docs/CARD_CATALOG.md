# Current card catalog — 30 October 2026

Implemented v0.10 rules. [Implementation and simulation results](BALANCE_IMPLEMENTATION_2026-10-30.md). [Sortable CSV](balance/2026-10-30-current-cards.csv).

All 48 cards. Market prices are paid on purchase; placement is free. Limit 0 means unlimited. Road requirements accept any adjacent road, including a neutral road. Tags are gameplay categories, not card types. Home includes House, Cottage, Apartment and Housing Authority. Adjacent means shared edges; ranges include diagonals. Forest is a mature Sapling, not an additional draw. Weight is relative within rarity. There are 10 Common cards; Street is excluded from free Common draws and guaranteed as a market option.

Park scoring: only the builder or growing Sapling's owner gains the difference between rounded-up half sizes before and after the addition, with size capped at 10. Joined regions count once; other owners gain no park-placement award. Physical tiles still determine Biggest Park Owner.

| Card | Type | Tags | Rarity | Price | Road | Limit | Weight | Park size |
|---|---|---|---|---:|---|---:|---:|---:|
| Street | Road | None | Common | 1 | False | 0 | 14 | 0 |
| House | Residential | Home | Common | 1 | True | 0 | 10 | 0 |
| Cottage | Residential | Home | Common | 1 | False | 0 | 4 | 0 |
| Shop | Commerce | Trade | Common | 1 | True | 0 | 6 | 0 |
| Farm | Industry | Nature | Common | 1 | True | 0 | 6 | 0 |
| Park | Green | Nature | Common | 1 | False | 0 | 8 | 1 |
| Wild Garden | Green | Nature | Common | 1 | False | 0 | 3 | 1 |
| Grant | Event | None | Common | 1 | False | 0 | 2 | 0 |
| Avenue | Road | None | Uncommon | 4 | False | 0 | 3 | 0 |
| Roundabout | Road | None | Uncommon | 4 | False | 0 | 2 | 0 |
| Orchard | Industry | Nature | Uncommon | 4 | True | 0 | 2 | 1 |
| Lake | Green | Nature | Uncommon | 4 | False | 0 | 2 | 0 |
| Vineyard | Growth | Trade, Nature | Uncommon | 4 | True | 0 | 2 | 0 |
| Startup | Growth | Trade | Rare | 7 | True | 0 | 2 | 0 |
| Market | Commerce | Trade | Rare | 7 | True | 0 | 3 | 0 |
| Contractor | Civic | Industry, Civic | Rare | 7 | True | 1 | 2 | 0 |
| Apartment | Residential | Home | Uncommon | 4 | True | 0 | 3 | 0 |
| Cafe | Commerce | Trade, Culture | Uncommon | 4 | True | 0 | 4 | 0 |
| Fountain | Green | Nature, Culture | Uncommon | 4 | False | 0 | 3 | 2 |
| Factory | Industry | Industry | Uncommon | 4 | True | 0 | 3 | 0 |
| Sapling | Growth | Nature | Uncommon | 4 | False | 0 | 3 | 1 |
| Bank | Civic | Trade | Rare | 7 | False | 3 | 4 | 0 |
| Town Hall | Civic | Civic | Rare | 7 | True | 1 | 2 | 0 |
| Museum | Civic | Culture, Civic | Rare | 7 | True | 1 | 2 | 0 |
| Chamber of Commerce | Civic | Trade, Civic | Rare | 7 | True | 1 | 2 | 0 |
| Warehouse | Civic | Industry | Uncommon | 4 | True | 1 | 2 | 0 |
| Builders' Yard | Civic | Industry | Rare | 7 | True | 1 | 2 | 0 |
| Hospital | Civic | Civic | Rare | 7 | True | 1 | 2 | 0 |
| Tourist Office | Civic | Culture, Civic | Rare | 7 | True | 1 | 2 | 0 |
| Tax Office | Civic | Civic | Uncommon | 4 | False | 1 | 2 | 0 |
| Depot | Civic | Industry | Uncommon | 4 | True | 1 | 2 | 0 |
| School | Civic | Culture, Civic | Uncommon | 4 | True | 1 | 2 | 0 |
| Granary | Civic | Civic | Uncommon | 4 | False | 1 | 2 | 0 |
| Stadium | Landmark | Culture, Civic | Rare | 7 | True | 0 | 2 | 0 |
| Airport | Landmark | Industry, Civic | Rare | 7 | True | 0 | 2 | 0 |
| Skyscraper | Landmark | Trade, Civic | Rare | 7 | True | 0 | 2 | 0 |
| Industrial Estate | Civic | Industry, Civic | Rare | 7 | True | 1 | 2 | 0 |
| Housing Authority | Civic | Home, Civic | Rare | 7 | True | 1 | 2 | 0 |
| Eco Council | Civic | Nature, Civic | Rare | 7 | True | 1 | 2 | 0 |
| Gardener's Guild | Civic | Nature, Civic | Uncommon | 4 | False | 1 | 2 | 0 |
| Architect's Studio | Civic | Culture, Civic | Rare | 7 | True | 1 | 2 | 0 |
| Renovation | Event | None | Uncommon | 4 | False | 0 | 2 | 0 |
| Ribbon Cutting | Event | None | Common | 1 | False | 0 | 1 | 0 |
| Rush Hour | Event | None | Common | 1 | False | 0 | 1 | 0 |
| Land Claim | Event | None | Uncommon | 4 | False | 0 | 2 | 0 |
| Central Station | Landmark | Trade, Civic | Legendary | 11 | True | 0 | 1 | 0 |
| Senate | Landmark | Civic | Legendary | 11 | True | 0 | 1 | 0 |
| Monument | Landmark | Culture | Legendary | 11 | False | 0 | 1 | 0 |

| Card | Description |
|---|---|
| Street | Payout: +1 VP per building adjacent to your Streets. Count each building once. |
| House | Place: +1 VP per adjacent Home building. Payout: +1 coin if a Shop is within 2 tiles. |
| Cottage | Place: +1 VP per adjacent park tile or Lake. Payout: +1 coin if adjacent to a park tile or Lake. |
| Shop | Payout: +1 coin per Home building within 2 tiles, max 3. |
| Farm | Payout: +1 coin if adjacent to another Farm or Orchard. |
| Park | Park size 1. Place: score the park's increase. |
| Wild Garden | Park size 1. Place: score the park's increase. |
| Grant | Play: gain 4 coins, then discard this card. |
| Avenue | Counts as 2 tiles for Longest Road. Payout: +1 coin if adjacent to at least 2 buildings. |
| Roundabout | When anyone builds beside this: gain 1 VP. |
| Orchard | Park size 1. Place: score the park's increase. Payout: +1 coin if adjacent to a Farm or another Orchard. |
| Lake | Payout: each adjacent Home building earns its owner 1 coin. Counts as a park neighbor, but is not a park tile. |
| Vineyard | Payout: nothing for its first 2 payouts; then +3 coins each payout. |
| Startup | Payout: +1 coin the first time, +2 the second, then +3 each time. |
| Market | Payout: +1 coin per Shop you own within 2 tiles, max 4. |
| Contractor | When you build a House card or Shop: gain 1 coin, up to 2 coins per round. |
| Apartment | Place: +2 VP per adjacent Home building. Payout: +2 coins if a Shop is within 2 tiles. |
| Cafe | Payout: +1 coin per adjacent Home building or Shop, max 3. End: +2 VP if adjacent to a park tile or Lake. |
| Fountain | Park size 2. Place: score the park's increase. |
| Factory | Payout: +2 coins. End: each adjacent Home building or Cafe loses its owner 1 VP, including yours. |
| Sapling | Park size 1. Place: score the park's increase. After its second payout, grows to size 3 and scores the increase again. |
| Bank | Payout: +2 coins. |
| Town Hall | Payout: +2 coins if you own at least 3 Civic buildings, including this. |
| Museum | End: +1 VP per other Culture building you own, max 8. |
| Chamber of Commerce | Payout: +1 coin per 2 Trade buildings you own, max 4 coins. Includes this. |
| Warehouse | Round start: gain a Street if your hand has room. When you build your first Street each round: gain 1 coin. |
| Builders' Yard | Round start: gain a Street or a random Common, if your hand has room. Choose which before the round starts. |
| Hospital | Each round: one random Home building has an emergency. The nearest Hospital within 4 tiles earns its owner 1 VP. Ties go to the earlier Hospital. |
| Tourist Office | Payout: +1 coin per Culture building you own, max 3. Includes this. |
| Tax Office | Payout: +1 coin per 4 Home buildings you own. |
| Depot | Payout: +1 coin per 5 road tiles you own, max 4 coins. |
| School | Payout: +1 coin per Home building you own within 2 tiles, max 4. |
| Granary | Payout: +1 coin per Farm or Orchard you own, max 3. |
| Stadium | Payout: each adjacent Home building earns its owner 1 coin; each adjacent Cafe loses its owner 1 coin. End: +1 VP per adjacent Home building. |
| Airport | Payout: +3 coins. End: each Home building within 2 tiles loses its owner 1 VP. Maximum loss: 3 VP per player across all Airports. |
| Skyscraper | Requires at least 2 adjacent buildings. End: +1 VP per other building within 2 tiles, max 6. |
| Industrial Estate | Payout: +1 coin per Industry building you own, max 5. Includes this. |
| Housing Authority | End: +1 VP per 3 Home buildings you own, max 6. Includes this. |
| Eco Council | End: +1 VP per 2 Nature buildings you own, max 8; then subtract 2 VP per Industry building you own, minimum 0. Includes this in Nature. |
| Gardener's Guild | When you build a park tile: gain 1 coin. |
| Architect's Studio | When you build your first building each round beside one of your buildings: gain 2 coins. This Studio can trigger its own effect. |
| Renovation | Play: replace one of your buildings with a different building card from your hand. Discard both cards. |
| Ribbon Cutting | Play: gain 1 coin per building you built this round, max 4, then discard this card. |
| Rush Hour | Play: this payout, gain 1 extra coin per Shop you own, max 4. Discard this card. Once per round. |
| Land Claim | Play: reserve an empty tile until next round starts. Only you may build there. Discard this card. |
| Central Station | Payout: +1 coin per Shop or Market you own, max 4. |
| Senate | End: +2 VP for each tag you have on at least 3 buildings, max 12. Includes this. |
| Monument | End: +6 VP. |

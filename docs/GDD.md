# City Sim: Game Design Document

**Version 0.10.** Prototype B removes economic linking and introduces independent local income and marginal park scoring, with free placement and revised market prices. A redesign: from a solo placement puzzle to a competitive, round-based **shared market** game on a shared city board. The first design is kept as [GDD_v0.1.md](GDD_v0.1.md). v0.4 added tap-to-bid auctions, default cards and card rarity, and made traffic purely cosmetic. v0.6 added tags, trigger and growth cards, landmarks, public goals and secret contracts. v0.7 removed every card that destroys buildings or touches the auction. v0.8 is a second balance pass, and removes every card that hurts a rival directly except Land Claim. v0.9 replaces sealed auctions with a shared, fixed-price market.

Engine: s&box (C#). Players: 1 to 6 (solo uses AI rivals). Match length: about 8-10 minutes.
All numbers marked *(tune)* are starting guesses to be balanced in playtests. See the [current 48-card catalog](CARD_CATALOG.md) and [implementation evidence](BALANCE_IMPLEMENTATION_2026-10-30.md).

---

## 1. Pitch

A board game that plays out live on a toy-like city. The heart of it is **engine-building through a shared card market**: each round, players **buy building cards**, then everyone builds on **one shared map at the same time**. Cars and trucks drive around to make the city feel alive, but they never decide who wins. Buildings pay their owner coins and victory points. Some give lasting effects that show up as icons on your screen. After 8 rounds, the most victory points wins.

**Pillars**
1. **Every round has a decision.** Buy or save? Build now or wait for a better tile?
2. **Shared board, shared consequences.** Neighbours and traffic help or hurt everyone, and *when* you build matters.
3. **Readable engine-building.** Your effects are always visible, and rivals' effects are inspectable.
4. **The city is alive.** Cars, trucks and ambulances show the economy running. They are ambience, not mechanics.
5. **Short and replayable.** One match fits in a coffee break.

---

## Card presentation and visual identity

Cards are physical, printed pieces in the Punchboard style: warm card stock, ink outlines, restrained paper texture, and a distinctive illustration or emoji placeholder. A consistent face helps players compare cards in the market and recognize them in hand. Card type supplies a family identity; the name and illustration identify the individual card. Rarity uses both a frame and a shaped marker.

Read the face from top to bottom: **actual gameplay tags** (icon and label), illustration, name and type, an **Interacts with** summary, exact rules, placement requirements and rarity. Home, Trade, Nature, Industry, Culture and Civic are gameplay tags; roads and events without tags do not acquire a tag for display. The interaction summary identifies subjects of an effect, not tags belonging to the card. For example, Cafe is Trade and Culture, while it interacts with houses, shops and parks. A warning symbol can flag a negative interaction, but the full rules explain its scope.

Rules remain authoritative and must state timing, ownership, ranges, caps and penalties. Use clear timing words such as Place, Payout, Round start and End where applicable; do not invent timing or derive complete prose from effect parameters. Summaries are reviewed card by card and may omit details rather than imply a rule. Road requirements and ownership limits are separately visible. Market prices and eligibility warnings belong to the market context, not the permanent card face.

In hand, compact cards retain identity and interaction information; full rules appear on hover, selection or keyboard focus. Market cards show the full rules. Inspection and persistent effects use the same names, tags and rules. Contracts share the physical style but have their own content. Tag chips remain usable to highlight owned buildings. Symbols are paired with text, and meaningful information must remain readable without color alone. Verify long cards, five-card hands, focused and disabled states, different resolutions and large text in the running game; icon explanations and bespoke illustrated art remain opportunities for future refinement.

**References and rationale:** [Dominion's rules](https://www.riograndegames.com/wp-content/uploads/2016/09/Dominion2E.pdf) show consistent card zones and separate instructions at different timings; [Dune: Imperium](https://news.direwolfdigital.com/dune-imperium-designer-diary-1-beginnings/) uses board-interaction icons and distinct effect areas, backed by an [icon guide](https://www.direwolfdigital.com/assets/dune/DUNE_IMPERIUM_Player_Aid_Icons_and_Board_Spaces.pdf); [Magic's card anatomy](https://magic.wizards.com/en/news/feature/anatomy-magic-card-2006-10-21) demonstrates a fixed type line. Icons support quick scanning but do not replace precise text.

---

## 2. Match structure

A match is **8 rounds**. Each round has three phases:

```
MARKET (untimed selections; optional 10 s countdown)  ->  ACTIVE (about 40 s)  ->  PAYOUT (about 5 s)
```

### 2.1 Market phase

Each selection reveals **five different cards** from the unlocked weighted pool. Each shows a fixed price determined by rarity (section 5.1). Every eligible player may buy **one** card or pass. A card remains in the pool after purchase: any number of different players may buy a copy of the same card. Buying immediately spends the coins and adds the card to the player's hand. Ownership limits and hand capacity are checked per player.

Purchases are public: colored, numbered player tokens appear beneath the chosen card. A player who purchases or passes is finished for that selection. The selection starts untimed; any eligible human, including someone who already chose, can press **Hurry up** once to start a shared 10-second countdown. Bots do not start the countdown. When time expires, anyone who has not chosen passes. The selection ends early once all eligible players have chosen. Disconnected players pass under host control.

Reveal up to **three selections** per round while any player is below the **hand limit of 5**. Players at the limit sit out. Default cards count toward the limit. Cards can recur across selections but not twice within one selection. Hand Target 3 guides bot discarding, not market admission. Each selection guarantees a Street option.

### 2.1a Default cards

At the start of every round, **every player gets free default cards** *(tune)*: **1 random Common card and no Streets**. The card counts toward the hand limit.

Some buildings add **bonus cards each round** (the "Builder" civic cards in section 5.2). Bonus cards are given at the same time as defaults.

### 2.2 Active phase
- Everyone plays cards from their hand onto the shared board in real time. There are no turns.
- First click claims a tile. Cards can be placed in any order, so you can save them for later rounds (up to the hand limit).
- Cars and trucks drive around the roads as a visual for your income (see section 6). They never block or fail anything.
- A placed building is permanent and belongs to the placer. No card can destroy or remove it.

### 2.3 Payout phase
- Flat income and passive effects are paid out, and the scoreboard animates.
- Everyone gets **base income of 2 coins** *(tune)*, plus what their buildings produce.
- **Catch-up:** the player with the **lowest VP** gets **+1 coin** (every player tied for last gets it). Not given in round 1.
- After round 8, **end-of-game bonuses** are scored (section 8) and a winner is declared.

### 2.4 Starting state
- Everyone starts with **8 coins** *(tune)* and an empty hand (defaults arrive at the start of round 1).
- The map is seeded and starts with a few random roads (as in v0.1) so there is always somewhere to build.
- Board size scales with player count: about **55 tiles per player** (11x11 for 2 players up to 18x18 for 6).

### 2.5 Tunable settings

All of these are plain data, so they can be changed per mode without touching code.

| Setting | Default | Notes |
|---|---|---|
| Rounds | 8 | |
| Hurry-up countdown | 10 s | starts only when an eligible human presses Hurry up |
| Cards per market selection | 5 | distinct cards |
| Hand Target | 3 | bot discard target; not market eligibility |
| Market selection cap per round | 3 | |
| Hand limit | 5 | |
| Default cards | 1 random Common, 0 Streets | |
| Active phase | 40 s | |
| Base income per round | 2 coins | |
| Catch-up bonus | +1 coin to last place | from round 2 |
| Starting coins | 8 | |
| Rarity odds | see section 5.1 | |

---

## 3. Currencies

| | Coins | Victory Points (VP) |
|---|---|---|
| Use | Spent on market cards | Decide the winner |
| Earned from | Payouts and effects | Placement bonuses, parks, end bonuses |
| Carry over? | Yes | Permanent |

Spare coins at the end are worth **1 VP per 5 coins** *(tune)*, so hoarding is never free.

---

## 4. The board

- A square grid of tiles. Each tile holds one building or road.
- Roads run off the edge of the map. Edge roads are where decorative **trucks** enter.
- Houses, shops, cafes, farms and factories **need an adjacent road** to be placed (roads give the city its shape; they do not limit income). Parks, roads and civic buildings do not (unless the card says so).
- Roads auto-shape to their neighbours (straight, curve, T, 4-way, dead end).

---

## 5. Cards

### 5.1 Anatomy of a card, tags and rarity

| Field | Meaning |
|---|---|
| **Name / type** | Road, Residential, Commerce, Industry, Green, Growth, Landmark, Civic or Event |
| **Tags** | One or two of: **Home, Trade, Nature, Industry, Culture, Civic**. Other cards score from them. |
| **Rarity** | How often it shows up, and its fixed market price |
| **Needs road** | Must touch a road to be placed |
| **On place** | Happens once, to the placer |
| **Payout** | Happens every round, to the owner |
| **VP** | Points, either on placement or at game end |
| **Icon** | *Yes* if it appears in your effect tray (section 7) |

Each card's rarity sets its fixed purchase price. Placement and Renovation are free under prototype B. Prototype A's optional build fees remain available for research; they are not the default. These values need human playtesting.

**Tags are the engine.** You do not need many cards to build a strategy, only a tag to build around. Specialising (e.g. Nature) makes your tag cards worth more, while spreading across tags feeds the Senate. Events and roads have no tag.

| Rarity | Price | Colour | Odds per reveal *(tune)* | Available from |
|---|---|---|---|---|
| **Common** | 1 | white | 55% | round 1 |
| **Uncommon** | 4 | green | 28% | round 1 |
| **Rare** | 7 | blue | 14% | round 2 |
| **Legendary** | 11 | gold | 3% | round 4 |

Reveal weights are relative weights for each draw; removing cards already shown changes subsequent probabilities within a selection.

### 5.2 Card list

Cards are grouped by what they are for. Anything that says *per X* counts your own buildings with that tag, including the card itself, unless it says "other".

**Roads**

| Card | Rarity | Tags | Needs road | Effect |
|---|---|---|---|---|
| **Street** | Common | | no | Payout: +1 VP per building adjacent to your Streets. Count each building once. |
| **Avenue** | Uncommon | | no | Counts as 2 tiles for Longest Road. Payout: +1 coin if adjacent to at least 2 buildings. |
| **Roundabout** | Uncommon | | no | When anyone builds beside this: gain 1 VP. |

**Residential**

| Card | Rarity | Tags | Needs road | Effect |
|---|---|---|---|---|
| **House** | Common | Home | yes | Place: +1 VP per adjacent Home building. Payout: +1 coin if a Shop is within 2 tiles. |
| **Cottage** | Common | Home | no | Place: +1 VP per adjacent park tile or Lake. Payout: +1 coin if adjacent to a park tile or Lake. |
| **Apartment** | Uncommon | Home | yes | Place: +2 VP per adjacent Home building. Payout: +2 coins if a Shop is within 2 tiles. |

**Commerce**

| Card | Rarity | Tags | Needs road | Effect |
|---|---|---|---|---|
| **Shop** | Common | Trade | yes | Payout: +1 coin per Home building within 2 tiles, max 3. |
| **Cafe** | Uncommon | Trade, Culture | yes | Payout: +1 coin per adjacent Home building or Shop, max 3. End: +2 VP if adjacent to a park tile or Lake. |
| **Market** | Rare | Trade | yes | Payout: +1 coin per Shop you own within 2 tiles, max 4. |

**Industry**

| Card | Rarity | Tags | Needs road | Effect |
|---|---|---|---|---|
| **Farm** | Common | Nature | yes | Payout: +1 coin if adjacent to another Farm or Orchard. |
| **Orchard** | Uncommon | Nature | yes | Park size 1. Place: score the park's increase. Payout: +1 coin if adjacent to a Farm or another Orchard. |
| **Factory** | Uncommon | Industry | yes | Payout: +2 coins. End: each adjacent Home building or Cafe loses its owner 1 VP, including yours. |

**Green**

| Card | Rarity | Tags | Needs road | Effect |
|---|---|---|---|---|
| **Park** | Common | Nature | no | Park size 1. Place: score the park's increase. |
| **Wild Garden** | Common | Nature | no | Park size 1. Place: score the park's increase. |
| **Fountain** | Uncommon | Nature, Culture | no | Park size 2. Place: score the park's increase. |
| **Lake** | Uncommon | Nature | no | Payout: each adjacent Home building earns its owner 1 coin. Counts as a park neighbor, but is not a park tile. |

**Growth** (these start weak and pay off if you place them early)

| Card | Rarity | Tags | Needs road | Effect |
|---|---|---|---|---|
| **Sapling** | Uncommon | Nature | no | Park size 1. Place: score the park's increase. After its second payout, grows to size 3 and scores the increase again. |
| **Vineyard** | Uncommon | Nature, Trade | yes | Payout: nothing for its first 2 payouts; then +3 coins each payout. |
| **Startup** | Rare | Trade | yes | Payout: +1 coin the first time, +2 the second, then +3 each time. |

**Landmarks** (they change the buildings next to them, for everyone)

| Card | Rarity | Tags | Needs road | Effect |
|---|---|---|---|---|
| **Stadium** | Rare | Culture, Civic | yes | Payout: each adjacent Home building earns its owner 1 coin; each adjacent Cafe loses its owner 1 coin. End: +1 VP per adjacent Home building. |
| **Airport** | Rare | Industry, Civic | yes | Payout: +3 coins. End: each Home building within 2 tiles loses its owner 1 VP. Maximum loss: 3 VP per player across all Airports. |
| **Skyscraper** | Rare | Trade, Civic | yes | Requires at least 2 adjacent buildings. End: +1 VP per other building within 2 tiles, max 6. |

**Civic** (the effect-tray cards; limit one of each per player unless noted)

| Card | Rarity | Tags | Needs road | Icon | Effect |
|---|---|---|---|---|---|
| **Warehouse** | Uncommon | Industry | yes | yes | Round start: gain a Street if your hand has room. When you build your first Street each round: gain 1 coin. |
| **Builders' Yard** | Rare | Industry | yes | yes | Round start: gain a Street or a random Common, if your hand has room. Choose which before the round starts. |
| **Bank** | Rare | Trade | no | yes | Payout: +2 coins. |
| **Town Hall** | Rare | Civic | yes | yes | Payout: +2 coins if you own at least 3 Civic buildings, including this. |
| **Tourist Office** | Rare | Culture, Civic | yes | yes | Payout: +1 coin per Culture building you own, max 3. Includes this. |
| **Tax Office** | Uncommon | Civic | no | yes | Payout: +1 coin per 4 Home buildings you own. |
| **Depot** | Uncommon | Industry | yes | yes | Payout: +1 coin per 5 road tiles you own, max 4 coins. |
| **School** | Uncommon | Civic, Culture | yes | yes | Payout: +1 coin per Home building you own within 2 tiles, max 4. |
| **Hospital** | Rare | Civic | yes | yes | Each round: one random Home building has an emergency. The nearest Hospital within 4 tiles earns its owner 1 VP. Ties go to the earlier Hospital. |
| **Granary** | Uncommon | Civic | no | yes | Payout: +1 coin per Farm or Orchard you own, max 3. |

**Tag engines** (these reward focusing on one tag)

| Card | Rarity | Tags | Needs road | Icon | Effect |
|---|---|---|---|---|---|
| **Chamber of Commerce** | Rare | Trade, Civic | yes | yes | Payout: +1 coin per 2 Trade buildings you own, max 4 coins. Includes this. |
| **Industrial Estate** | Rare | Industry, Civic | yes | yes | Payout: +1 coin per Industry building you own, max 5. Includes this. |
| **Housing Authority** | Rare | Home, Civic | yes | yes | End: +1 VP per 3 Home buildings you own, max 6. Includes this. |
| **Museum** | Rare | Culture, Civic | yes | yes | End: +1 VP per other Culture building you own, max 8. |
| **Eco Council** | Rare | Nature, Civic | yes | yes | End: +1 VP per 2 Nature buildings you own, max 8; then subtract 2 VP per Industry building you own, minimum 0. Includes this in Nature. |

**Triggers** (they pay out when you do something, so they chain with everything else)

| Card | Rarity | Tags | Needs road | Icon | Effect |
|---|---|---|---|---|---|
| **Gardener's Guild** | Uncommon | Nature, Civic | no | yes | When you build a park tile: gain 1 coin. |
| **Contractor** | Rare | Industry, Civic | yes | yes | When you build a House card or Shop: gain 1 coin, up to 2 coins per round. |
| **Architect's Studio** | Rare | Culture, Civic | yes | yes | When you build your first building each round beside one of your buildings: gain 2 coins. This Studio can trigger its own effect. |

**Legendary**

| Card | Rarity | Tags | Needs road | Icon | Effect |
|---|---|---|---|---|---|
| **Central Station** | Legendary | Trade, Civic | yes | yes | Payout: +1 coin per Shop or Market you own, max 4. |
| **Monument** | Legendary | Culture | no | yes | End: +6 VP. |
| **Senate** | Legendary | Civic | yes | yes | End: +2 VP for each tag you have on at least 3 buildings, max 12. Includes this. |

**Economy events** (one-shot, played from your hand during the active phase, then discarded)

| Card | Rarity | Effect |
|---|---|---|
| **Grant** | Common | Play: gain 4 coins, then discard this card. |
| **Ribbon Cutting** | Uncommon | Play: gain 1 coin per building you built this round, max 4, then discard this card. |
| **Rush Hour** | Uncommon | Play: this payout, gain 1 extra coin per Shop you own, max 4. Discard this card. Once per round. |
| **Land Claim** | Uncommon | Play: reserve an empty tile until next round starts. Only you may build there. Discard this card. |
| **Renovation** | Uncommon | Play: replace one of your buildings with a different building card from your hand. Discard both cards. |

**Design rule: no card destroys or removes a rival's building.** A placed building is permanent. **No card changes another player's market purchase** either: the market is the same for everyone. The only card that touches a rival is Land Claim, which blocks one empty tile for one round. Start with them in test builds and cut anything that feels bad.

### 5.3 Park rules

Orthogonally adjacent park tiles (Park, Wild Garden, Fountain, Sapling and Orchard) form a park region; Lake is not a park tile. Park size is the sum of tile sizes: Fountain contributes 2 and mature Forest 3. Physical tile count is separate from size.

On placement or Sapling growth, sum the sizes of the distinct regions joined before the addition as `S_before`, counting each region once. Let `S_after` be the resulting size. Only the placer or growing tile's owner earns `ceil(min(S_after, 10) / 2) - ceil(min(S_before, 10) / 2)` VP. There are no shared awards. An isolated size-1 park earns 1 VP; extensions 1Ã¢â€ â€™2, 2Ã¢â€ â€™3, 9Ã¢â€ â€™10 and 10Ã¢â€ â€™11 earn 0, 1, 0 and 0 respectively. Fountain and growth use their actual size increase.

### 5.4 Adjacency rules

**Adjacent** means sharing an edge. **Within N tiles** uses Chebyshev distance and includes diagonals. A **Home building** is any building with the Home tag, including Housing Authority. Unless a card says otherwise, eligible neighbors of any owner count; each building pays its own owner. Factory and Airport penalties can affect either owner. Roads and events are not buildings.

### 5.5 The deck
- Each reveal picks a **rarity first** (by the odds in section 5.1, skipping tiers not yet unlocked), then a card within it by weight. Cards are drawn **with replacement**, except one-per-player Civic cards, which leave a player's pool once they own one.
- **Within Common:** Street 14, House 10, Park 8, Shop 6, Farm 6, Cottage 4, Wild Garden 3, Grant 2.
- **Within Uncommon:** Avenue 3, Roundabout 2, Apartment 3, Cafe 4, Orchard 2, Factory 3, Fountain 3, Lake 2, Sapling 3, Vineyard 2, Warehouse 2, Tax Office 2, Depot 2, School 2, Granary 2, Gardener's Guild 2, Rush Hour 2, Land Claim 2, Ribbon Cutting 2, Renovation 2.
- **Within Rare:** Market 3, Startup 2, Chamber of Commerce 2, Industrial Estate 2, Housing Authority 2, Museum 2, Eco Council 2, Architect's Studio 2, Contractor 2, Builders' Yard 2, Bank 4, Town Hall 2, Tourist Office 2, Hospital 2, Stadium 2, Airport 2, Skyscraper 2, 
- **Within Legendary:** Monument 1, Central Station 1, Senate 1.
- Rare Civic cards give a strong long-term engine, so their price should reflect their long-term value.

### 5.6 First playtest set (21 cards)

The full list is 48 cards, which is too many to balance at once. Start with this set, and add the rest once the core loop feels good:

- **Basics:** Street, House, Cottage, Shop, Farm, Park, Wild Garden, Grant.
- **Variety:** Apartment, Cafe, Fountain, Factory, Sapling.
- **Engines:** Bank, Town Hall, Museum, Chamber of Commerce, Gardener's Guild, Architect's Studio.
- **Interaction and payoff:** Land Claim, Monument.

This covers parks, local income, tags, triggers, growth and light interaction.

---

## 6. Payouts and the living city

Each building calculates its own income independently from eligible neighbors. A House earns 1 coin and an Apartment 2 if a Shop is within 2 tiles; a Shop earns 1 per Home building within 2 tiles, up to 3; a Farm or Orchard earns 1 if another Farm or Orchard is adjacent; a Cafe earns 1 per adjacent Home building or Shop, up to 3. Neighbors may belong to any player. No capacity or allocation is shared.

Payouts apply building effects, event modifiers, base income and catch-up; applicable deductions use the available coin balance. Growth advances after payout. Traffic is cosmetic and never determines income.

### 6.3 Cosmetic traffic
Vehicles exist to make the board feel busy and to show the economy at work.
- Continuous background cars travel between road-connected buildings and board edges. Cars emerge from building frontages, use right-hand lanes, then turn into their destination and move slightly inside before disappearing.
- **Trucks** drive from the map edge to farms and shops at the start of the active phase.
- **Emergencies:** a random house catches an emergency during the active phase. An **ambulance** drives to it from the nearest road, and other vehicles slow down. The **nearest Hospital** within 4 tiles answers it (+1 VP to its owner). Nobody is penalised if no Hospital is near. Only the distance check matters, not the route. There is one emergency per round.
- Vehicles have no gameplay capacity or congestion effects. Cosmetic following gaps and intersection yielding make movement readable; a small population grows with the city, with a hard cap of 100. Driver speeds and lane positions vary slightly. See [TRAFFIC.md](TRAFFIC.md) for implementation and verification details.

---

## 7. Effect tray and HUD

- **Effect tray** (top left): one icon for each civic building you own. Hovering shows its name and effect. Stackable ones show a count.
- Clicking a rival in the scoreboard shows *their* tray, so you can read what they are building toward.
- **Top bar:** round number, phase and timer.
- **Coins and VP** are visible in the city HUD, with floating "+N" numbers on gains; the market shows the available coin balance.
- **Market screen:** five fixed-price cards, public colored buyer tokens, player status, available coins, Buy, Pass and Hurry up. The normal HUD is hidden while shopping. View city (V / right bumper) hides the market temporarily, enables camera movement and tile inspection, and shows the countdown if one is running; the same control returns to the market. The market clock continues during inspection.
- **Goals panel:** the public goals (section 8.2) with who has claimed them, and your secret contract.
- **Tags and interactions:** cards show their actual tags as labeled chips. Hovering a tag on a card or in its rules highlights matching cards and buildings; tag chips in the effect tray highlight the selected playerÃƒÂ¢Ã¢â€šÂ¬Ã¢â€žÂ¢s buildings. Hovering an interaction or its heading highlights matching board pieces; tag targets and specific building targets remain distinct. This is a category preview: range, ownership and other conditions still apply as written. Matching tiles receive an ink-and-mustard printed frame and icon, while other tiles darken slightly. View city retains the last hovered target and shows the match count; returning clears the active highlight. The market stays fully visible.
- **Hand:** up to 5 cards along the bottom. Click one to see a ghost on the board: green if legal, red if not, and the placement bonus shown ("+3 VP").

---

## 8. Scoring and winning

### 8.1 Victory points

**VP sources**
1. Placement bonuses (houses, apartments, cottages, Roundabouts).
2. Park placement and growth awards (section 5.3).
3. Emergencies answered by Hospitals.
4. End-of-game cards (Monument, Museum, Housing Authority, Eco Council, Skyscraper, Senate) and end-of-game bonuses.
   - Public goals and your secret contract (sections 8.2 and 8.3).
5. Spare coins (1 VP per 5).

**End-of-game bonuses** (each scored once, ties split the points)
| Bonus | Condition | VP |
|---|---|---|
| **Longest Road** | Your longest chain of connected roads you placed (Avenues count double) | 6 |
| **Biggest Park Owner** | Most physical owned tiles in any one park | 6 |
| **Full House** | Most Home buildings placed | 4 |

Penalties are applied (Factory pollution, Airport noise). **Highest VP wins.** Ties go to the player with more coins left.

Tied bonus pools split equally and round down; a bonus with no qualifying entries is not awarded. A road chain is a simple path through owned road tiles, excluding neutral starting roads. Biggest Park Owner counts physical owned tiles, separate from park size. Home rules use the Home tag. Architect's Studio can trigger on its own placement. Vineyard and Startup age by completed payouts. Hospital selects one random Home-building emergency per round; nearest uses Chebyshev distance and oldest placement breaks ties. Senate counts each tag independently.

**Builder and Renovation conventions (M7):** Warehouse and Builders' Yard grant extra cards after the shared defaults, in source placement order, starting the round after the building is placed. The Yard's persistent choice initially selects Street and can be changed for the following round through the owner's effect tray; a full hand skips the grant without consuming a random Common draw. Warehouse pays only for the exact Street definition, once per source per round. Renovation consumes its event and a different non-road/non-event building card, targeting only the player's own non-road building. The old building is excluded during validation and scoring; prior earned points are retained, and the replacement starts at age zero with a fresh placement order. The replacement counts as new construction, but removing an earlier building does not reset that round's first-building or source-trigger history. Authenticated commands validate both slots against one hand version before consumption. Bots use the same validator and scoring rules.

**Historical auction balance baseline (M8, 27 October 2026; predates the shared market and must not be used to infer market balance):** After incremental bot bid scheduling was added, a 100-match, 25-board paired simulation using live four-player sizing produced win shares of Balanced 44%, Industry Tag 10%, Park Rush 15% and Economy First 31%, with no rejected actions or budget violations. This compared the auction-era bot proxies, not optimal human strategies. Mean final VP was 68.77, 60.07, 64.14 and 66.60 respectively. The [baseline report](BALANCE_BASELINE.md) records seed, settings, curves, source fingerprints and the previous timing version separately. The market requires a new baseline and human playtests. The 4 ms AI frame budget is a soft CPU scheduling parameter, not an economy change or a guarantee that every evaluation fits inside it.

### 8.2 Public goals

At the start of a match, **3 public goals** are revealed from the list below. Goals are checked at the **end of every active phase**, so speed of clicking does not matter. A player who meets a goal claims **5 VP** *(tune)*. If several players meet it in the same round, **each gets 3 VP**. A claimed goal is replaced at the start of the next round, and an unclaimed goal stays.

**Implementation conventions (M4, 27 October 2026):** the active placement phase is Build; check goals once when it ends, before payout income or growth. Rainy Day uses the coins held at that checkpoint, rather than peak cash earlier in the round. Home conditions include Cottage and count Apartment once. Connected groups use orthogonal adjacency, High Street requires consecutive Shops, and Civic conditions use the Civic tag rather than card type. Green Thumb counts physical owned cells in a connected shared park. Tag Collector allows a multi-tag building to count for each of its tags. The nine-goal deck reshuffles its discard pile when exhausted, never duplicating an active goal; an unclaimed goal retains its slot. Public goal awards remain part of match VP and appear separately in the final explanation.

| Goal | Condition |
|---|---|
| **Neighbourhood** | Own 4 Home buildings in a connected group |
| **High Street** | Own 3 Shops in a straight line |
| **Green Thumb** | Own 4 tiles in a single park |
| **Farm Belt** | Own 3 Farms in a connected group |
| **Civic Pride** | Own 3 Civic buildings |
| **Arts District** | Own 3 Culture buildings |
| **Tag Collector** | Own 4 buildings in each of 3 different tags |
| **Mini Mall** | Own a Shop, a Cafe and a Park, all touching the same road tile |
| **Rainy Day** | Hold 12 coins at the same time |

### 8.3 Secret contracts

Each player is dealt **2 contracts** at the start and keeps one. It scores the VP printed on it at game end if the condition is met. Harder contracts pay more. Contracts steer your purchases without telling rivals what you are after.

Each player's two offers are distinct; different players may receive the same contract. The match waits for every choice before dealing defaults or starting its clock. Offers and kept contracts are private owner views until the final reveal. Landlord counts each owned Home once; Mayor counts distinct Civic-tagged card definitions. Entrepreneur requires three owned Shops, each with at least two Home buildings within 2 tiles; the same Home may qualify multiple Shops. Fulfilled contracts award their printed VP once; unmet contracts reveal for zero.

| Contract | Condition | VP |
|---|---|---|
| **Suburbia** | Own 10 Home buildings | 5 |
| **Greenbelt** | Own 10 Nature buildings | 5 |
| **Hoarder** | End the game with 15 coins unspent | 5 |
| **Cultured** | Own 5 Culture buildings | 6 |
| **Landlord** | Own 3 Home buildings next to rivals' Home buildings | 6 |
| **Industrialist** | Own 5 Industry buildings | 7 |
| **Mayor** | Own 4 different Civic-tagged buildings | 7 |
| **Entrepreneur** | Own 3 Shops, each with at least 2 Home buildings within 2 tiles | 6 |

---

## 9. Multiplayer and solo

- The host is authoritative, as in v0.1. Clients send requests (buy, pass, place, play event), and the host validates and broadcasts the result.
- Purchases and buyer identities are public; unplayed hands remain private.
- A late joiner is given a snapshot, as in v0.1.
- **Solo mode:** 1-3 AI rivals. They choose market cards with a budget-and-preference heuristic and place with the same scoring rules the player sees. Difficulty adjusts their evaluation and choices.
- **Disconnects:** the host passes for disconnected players in the market and readies them during Build.

---

## 10. Camera and controls

Unchanged from v0.1: WASD pan, middle-mouse grab, wheel zoom to cursor, Q/E rotate, R recentre.

| Input | Action |
|---|---|
| Left click | Place the selected card |
| 1-5 | Select a card in hand |
| Right click | Deselect |
| Click a market card or Buy | Buy one copy at its shown price |
| Space | Pass in the market; ready during Build |

---

## 11. Presentation

- Same bright toy city, tile drop-in animation and sound feedback as v0.1.
- Placement and big payouts get small screen shake and sound. The pitch of the sound rises with consecutive good drops.
- A clear phase banner announces MARKET / BUILD / PAYOUT and the countdown ticks in the last 5 seconds.
- Hover tooltips for every building and every effect icon.

---

## 12. What changes in the code

| Today | Becomes |
|---|---|
| Random piece dealt on a timer | Default cards plus cards bought in the market, held in a hand |
| `GameMode` (Normal, Chaos, Sandbox) | One mode, with solo AI. Sandbox stays as a debug option. |
| Hunger and delivery | Independent local income and deterministic payouts. `Vehicle` stays, but only as cosmetic traffic. |
| Score on placement only | VP and coins, with several sources |
| `CityGame` host logic | Split into a round state machine, a market and a payout calculator. Traffic spawning becomes a separate cosmetic system. |
| `Tile.Owner` exists already | Used for every payout |
| Hardcoded tile rules (`TileRules`) | Data-driven `CardDef`s with tags, rarity and effect hooks: `OnPlace`, `OnPlaceOther` (triggers), `OnPayout`, `OnGameEnd`. Most cards are then data plus a few small effect classes. |

Reuse as is: `CityGrid` (placement, road shaping, pathfinding for cosmetic cars), `Vehicle`, `CityCamera` and the networking pattern.

---

## 13. Open questions

1. Do the three market selections and hand limit keep the market phase quick?
2. Does marginal park scoring leave parks desirable?
3. Do prototype B prices create meaningful spending choices in human play?
4. Is Land Claim fair, and is the catch-up bonus sufficient?
5. Are eight rounds, the full 48-card set, public goals and secret contracts easy to follow?
6. Do local ranges and tag engines create enough varied placement decisions?
7. What art and models should replace the remaining placeholders?

### Economy event pricing (30 October balance implementation)

Ribbon Cutting and Rush Hour are Common, weight 1 each, costing 1 coin. At the new Uncommon price of 4 their maximum 4-coin reward could never return a profit. Their effects and caps stay unchanged; low weight keeps free round defaults mostly focused on buildings. See the implementation report for post-change simulations.

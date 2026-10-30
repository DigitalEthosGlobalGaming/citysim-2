# City Sim: Game Design Document

Status: **v0.1, describes the game as it is built today**, plus a list of open questions for the redesign (section 12).
Engine: s&box (C#). Players: 1 to many, shared board, host-authoritative.

---

## 1. Pitch

A cozy, low-pressure city-building puzzle. A tile at a time, you are dealt a random piece (house, shop, park or road) and drop it on a grid. Where you put it decides your score: parks love houses, shops hate living next to houses, houses like other houses. Once the city is alive, cars and trucks drive the roads to keep buildings fed. Fill the map and the round ends.

Think *Dorfromantik* meets *Islanders*, with a little traffic simulation for life.

**Pillars**
1. **Placement is the game.** Every drop is a small, readable decision.
2. **The city looks alive.** Drop-in animations, cars, ambulances, sound.
3. **Easy to pick up.** One button to place, one to discard, a camera that behaves.
4. **Play together.** Friends share one board and compete on score.

---

## 2. Core loop

1. The game deals you a piece (after a short delay, if your hand is empty).
2. Hover the board: a ghost shows where it would go, green if legal, red if not, with "+N points here".
3. Click to place. Score goes up, the piece drops in with a thump and a sound.
4. Don't like the piece? Right-click to discard it and wait for the next one.
5. Meanwhile, buildings get hungry and vehicles go to feed them (section 6).
6. When about 90% of the board is filled, the round ends and returns to the menu after 10 seconds.

---

## 3. Modes

| Mode | Board size | Piece delivery | Ends? | Notes |
|---|---|---|---|---|
| **Normal** | 10-15 x 10-15 | one new piece every 3 s once your hand is empty | yes, at 90% filled | the standard game |
| **Chaos** | 1.8x Normal | every 1 s | yes | fast and frantic, bigger board |
| **Sandbox** | 2x Normal | none: you pick | never | keys 1-4 choose House / Park / Shop / Road, infinite pieces, no discard |

---

## 4. The board

- Square grid, each cell is one tile (50 world units).
- Each round is seeded, so every peer builds the identical map.
- **Starting streets:** 1-4 random full-length horizontal roads and 1-4 vertical ones are laid across the map. There is always a road network to build against.
- Roads run off the edge of the map, so the edge is where outside traffic enters.
- Empty cells show a plain ground tile. Roads and parks replace it entirely.

---

## 5. Placeables

| Piece | Odds | Needs a road? | Has food needs? | Variants |
|---|---|---|---|---|
| **House** | 40% | yes, adjacent | yes | 5 body shapes x 6 colour schemes |
| **Shop** (Business) | 20% | yes, adjacent | yes | 3 shapes x 5 tints |
| **Park** (forest) | 20% | no | no | randomised rocks and bushes, random rotation |
| **Road** | 20% | no | no | auto-shapes to neighbours: straight, curve, T, 4-way, dead end |

Rules
- A piece can only go on an empty cell.
- Houses and shops must touch a road, and they turn to face it.
- Roads re-shape themselves when a neighbour changes. Off-map counts as connected.
- Pieces are permanent: nothing can be moved or demolished.

### 5.1 Placement scoring

Each placement scores `max(0, sum of affinities with the 4 neighbours) + 1`.

| Pair | Affinity | Why |
|---|---|---|
| Park + House | **+2** | green views |
| Park + Park | **+2** | a bigger forest |
| Shop + Shop | **+2** | a shopping strip |
| House + House | **+1** | neighbourhood |
| Shop + House | **-1** | noise |
| Park + Shop | **-1** | |
| Park + Road | **-1** | |
| everything else (roads with houses/shops, anything with empty) | 0 | |

The floor is +1 point for any legal placement, so a bad drop costs opportunity, never points. The maximum for one drop is +9 (a park with four park or house neighbours).

---

## 6. Food economy and traffic

This is the only live simulation, and it is host-only: clients just watch.

### 6.1 Hunger
- Each house and shop has a **Supply** (starts 5-20) and a **Max Supply** (18-30).
- Every **5 seconds** each building loses **2 supply** (unless a delivery is already on its way).
- At 0 supply and next to a road, a building is **hungry**: it tints red for everyone.

### 6.2 Deliveries
- Each 5 s tick, up to **10** hungry buildings (random order) get a delivery, with a global cap of **100 vehicles**.
- **Shops** are always restocked by a **truck from the map edge**.
- **Houses** send a **car from a random shop** 75% of the time, otherwise a truck from the edge.
- Vehicles take the **shortest road path** (breadth-first search). If no road connects, no delivery happens and the building stays hungry. This is the reason to build connected road networks.
- On arrival: supply rises by `3-6 + 8` (capped at max), and the **owner earns +2 score**. The vehicle then reverses back along its route and despawns.
- While a vehicle is en route the target is marked "delivering" and does not lose supply.

### 6.3 Ambulance
- Each tick there is a 20% chance of an **ambulance** driving from a random shop to a random house, if none is already out.
- It is faster, uses the inner lane, plays a siren, and **all other vehicles slow down** when it is within range.
- It is cosmetic: it has no score or gameplay effect.

### 6.4 Vehicle behaviour
Vehicles follow the road centres with a lane offset, turn smoothly, and each has its own engine sound at its world position.

---

## 7. Scoring and winning

- Placement score (section 5.1) plus +2 per completed delivery to your buildings.
- Every building remembers who **owns** it (who placed it), and deliveries pay that player.
- Highest score on the shared board at the end wins. A personal high score is tracked per session.
- There is currently **no penalty for hungry buildings** beyond the red tint and missing out on delivery points.

---

## 8. Multiplayer

- The first player to start hosts a **friends-only lobby**; others join and are brought up to date with a snapshot of the board.
- One shared board, but **each player has their own hand, delivery timer and score**.
- The host validates everything. Clients send requests (place, discard, select, start); the host broadcasts the result and every peer applies it to its own copy of the grid.
- If the host leaves, another player takes over and the simulation continues.
- Single player uses the same code path, with the local machine as host.

---

## 9. Camera and controls

| Input | Action |
|---|---|
| Left click | Place the held piece |
| Right click | Discard the held piece (not in Sandbox) |
| 1 / 2 / 3 / 4 | Sandbox: Shop / Park / House / Road |
| WASD / arrows | Pan (Shift for speed) |
| Middle mouse drag | Grab and drag the map |
| Mouse wheel | Zoom towards the cursor |
| Q / E | Rotate the view 45 degrees |
| R | Recentre |

The camera is an eased orbit rig with bounds. Placement and denied drops add small screen shake.

---

## 10. Presentation

- **Look:** bright, toy-like low-poly city on a flat green plane under a light blue sky.
- **Feedback:** pieces drop in with a short ease, button-press sound, camera nudge; denied placements get a buzz and a smaller nudge; hungry buildings glow red.
- **HUD:** current piece, hover info (+points or "Can't go here"), score, scoreboard in multiplayer, mode and end-of-round screens.
- **Audio:** Kenney UI sounds, per-vehicle engine loops, ambulance siren.

---

## 11. Technical notes

- `CityGame`: rules, networking, host simulation (pieces, hunger, vehicles).
- `CityGrid` and `Tile`: board state, placement, road shapes, pathfinding.
- `Vehicle`: movement along a route, with arrival and finish callbacks.
- `CityPlayer`: tile picking, ghost, input. `CityCamera`: camera rig.
- `Hud.razor`: all UI.

---

## 12. Open questions for the redesign

The current game is a solid toy, but several systems are shallow. These are the things worth deciding before building more.

**Depth and goals**
1. *What is the long-term goal?* Right now it is "fill the board". Do we want objectives (reach 200 points, feed everyone, build 5 parks), a campaign, or score attack only?
2. *Is the tile deal pure luck?* Options: a visible queue of the next 3 pieces, a hold slot, or a draft shared between players.
3. *Placement is permanent.* Should there be demolish, move, or upgrade (house to apartment, shop to mall)?

**Traffic and the economy**
4. *Hunger has no consequences.* Should starving buildings lose score, leave, or block growth? Today the only effect is a red tint.
5. *Food is invisible and abstract.* Should shops hold stock that they must buy, creating real supply chains (farms, warehouses, trucks)?
6. *Traffic is a single route per delivery.* Do we want congestion, one-way roads, intersections, or road capacity so that road design matters?
7. *The ambulance is decorative.* Make emergencies a real event (fires, illness) that reward a good road layout, or cut it?

**More placeables**
8. Candidates: services (fire, police, school), industry and farms, water and shoreline, power, landmarks, and terrain such as hills and rivers on the starting map.
9. Should the affinity table become a visible chart or an in-game hint? Players cannot currently see why a tile is worth what it is, other than the hover number.

**Multiplayer**
10. *Co-op or competitive?* Today it is score versus score on one board. A co-op mode with a shared goal might fit "cozy" better.
11. Should players claim districts, or is the free-for-all board fine?

**Feel and UX**
12. A tutorial or first-run hint, a reduce-shake accessibility option, and colour-blind-safe hunger indication (red tint alone is not enough).

---

## 13. Suggested next step

Pick answers for questions 1, 4 and 6 first: **goal, consequence of hunger, and how much traffic should matter**. Everything else follows from those three.

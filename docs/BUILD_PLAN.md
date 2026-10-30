# City Sim: Build Plan

Plan for building the game described in [GDD.md](GDD.md) (v0.8). The goal is a **playable loop as early as possible**, then layers on top. Each milestone ends in something you can play and judge, not just code that compiles.

UI structure follows the `game-ui-ux` skill, and feedback follows the `game-feel` skill. Both are folded into every milestone below (section 3), not left to the end.

---

## 1. Principles

1. **Rules are pure C#, the scene is a view.** Auction, payout, Links, park scoring, goals and contracts are plain classes working on a data model, with no `GameObject`s. That makes them testable and means they can be shared by host, AI bots and the UI preview ("+3 VP here").
2. **Data-driven cards.** A card is a `CardDef` (id, name, type, tags, rarity, needs road, effect hooks), registered in one library. Adding a card should be a data entry plus, at most, one small effect class.
3. **The host owns the truth.** Bids, hands and the deck live only on the host. Clients send requests and receive results, as the current code already does.
4. **Playable at every milestone.** Keep the old game running until the new loop replaces it (M1), then delete the old modes.
5. **Feel is part of "done".** A feature is not finished until it has its feedback from section 3.2.
6. **Tunables are data.** Every number marked *(tune)* in the GDD lives in `MatchSettings` or the card library, never inline.

---

## 2. Architecture

### 2.1 Proposed layout

```
code/
  Cards/        CardDef, Tag, Rarity, CardLibrary, Effects/ (one small class per special rule)
  Match/        MatchState (the pure model), RoundMachine, Auction, Payout, Links,
                ParkScoring, Goals, Contracts, MatchSettings
  Net/          Host requests (RPCs), private-hand RPCs, snapshots
  Game/         CityGame (thin: wires model <-> scene <-> net), CityPlayer, CityCamera, Vehicle, Sfx
  Tiles/        CityGrid, Tile (the view of the board; reads/writes MatchState)
  Ai/           Bot (bids and places using the same rules)
  Feel/         Feedback (one place that maps events to shake, sound, tween, floaters)
  Ui/           Screens/, Panels/, Hud.razor(.scss)
```

### 2.2 The key refactor

Today `CityGame` mixes rules, networking and scene objects, and `Tile` holds renderers. The plan splits it:

| Layer | Contains | Engine types? |
|---|---|---|
| **MatchState** | Players, coins, VP, hands, board cells (owner, card, age), round, phase, goals, contracts | none |
| **Rule classes** | `Auction.Resolve`, `Payout.Calculate`, `Links.Build`, `ParkScoring.OnTileAdded`, `Goals.Check` | none |
| **Host (`CityGame`)** | Runs the `RoundMachine`, applies rule results, broadcasts them | yes |
| **View** | `CityGrid` spawns models; HUD panels read state and subscribe to events | yes |

Rule classes return **results** (e.g. a `PayoutResult` listing every coin and VP with its source building). The view uses the same list to play the feedback in order, which means the payout animation is free and always matches the numbers.

### 2.3 Round state machine

```
Lobby -> RoundStart -> Bid (mini-auction x N) -> Build -> Payout -> RoundStart ... -> EndGame
```

- One authoritative `Phase` and `PhaseEndsAt` on the host, broadcast on change.
- Clients drive their own countdown display from `PhaseEndsAt`, so there is no per-frame network traffic.
- Every transition raises an event (section 3.1), which is what the UI reacts to.

### 2.4 Networking

| Data | Where it lives | How it moves |
|---|---|---|
| Bids | Host only | `[Rpc.Host] SetBid(lot, amount)`; clients get only "who is ready" |
| Hands | Host + the owner | Filtered RPC to the owning connection (the existing `HostGive` pattern) |
| Board changes | Everyone | Broadcast `Place(card, x, y, owner)` |
| Resolve results | Everyone | Broadcast list of winners, prices and the new reveal |
| Payout results | Everyone | Broadcast `PayoutResult` so every client plays the same animation |
| Late joiner | Snapshot | Extend the existing snapshot with round, phase, hands (own only), goals |

---

## 3. UI and feel, built in from the start

### 3.1 UI architecture (`game-ui-ux`)

**Events, not polling.** The current HUD recomputes a hash every frame. The new HUD subscribes to a small `MatchEvents` bus and re-renders only the panel that changed.

| Event | Updates |
|---|---|
| `PhaseChanged` | Top bar, which screen is shown, input mode |
| `CoinsChanged(player)` / `VpChanged(player)` | Resource panel, scoreboard |
| `HandChanged` | Hand bar |
| `BidChanged` | Your bid chips on the lot cards |
| `LotsRevealed` / `AuctionResolved` | Bid screen |
| `EffectTrayChanged(player)` | Top-left effect tray |
| `GoalClaimed` / `GoalsChanged` | Goals panel |
| `PayoutResolved` | Payout sequence |

**Screen stack.** `Menu -> Lobby -> Match -> End`, with `Settings` and `Pause` pushed on top. The Match screen has sub-views for Bid, Build and Payout, driven by `PhaseChanged`. No `isBidding` / `isPaused` flags.

**Layout rules**
- Anchored to edges and corners with flex containers. No absolute pixel positions.
- Designed at 1920x1080 and scaled by the screen panel. HUD corners anchor outward on ultrawide.
- A 3% margin keeps critical UI off the screen edge.
- Use the compact fixed UI sizing requested on 27 October. Do not expose text-percentage selection; verify legibility and pointer/focus hit areas at the target resolutions.

**Panels**

| Panel | Where | Notes |
|---|---|---|
| **Top bar** | Top centre | Round x/8, phase name, timer ring |
| **Resources** | Top right | Coins, VP, with floating "+N" |
| **Effect tray** | Top left | One icon per Civic building, with count badge and hover tooltip |
| **Goals panel** | Left, collapsible | 3 public goals and your secret contract |
| **Scoreboard** | Right | All players, VP and ready pips |
| **Hand bar** | Bottom | Up to 5 cards, number keys 1-5 |
| **Bid screen** | Centre, over the board | Lots as cards, your chip count, Ready |
| **Tooltip** | Cursor | Name, tags, rarity, rules text, live "+VP here" |

**Input**
- Mouse is the primary device. Every action also has a keyboard path: 1-5 select a card, Space is Ready, Esc opens Pause.
- Gamepad: D-pad moves between lots and cards, A adds a bid chip, X removes one, Y readies. Each screen sets an initial focus and a visible focus ring. The camera uses the sticks.
- Colour is never the only signal. Rarity also has a shape marker, tags have icons, and hungry/invalid states have an icon as well as a tint.

### 3.2 Feedback plan (`game-feel`)

Feedback is scaled by **importance tier**, defined once in `Feel/Feedback.cs`, and every event is assigned a tier. The existing `CityCamera.AddTrauma` is the shake hook.

| Tier | Shake (trauma) | Sound | Visual |
|---|---|---|---|
| **Small** | 0 | soft tick | small pop |
| **Medium** | 0.15-0.3 | clear chime | pop, particles, floater |
| **Large** | 0.4-0.6 | fanfare | pop, ring pulse, banner |

| Event | Tier | Feedback |
|---|---|---|
| Tap to bid | Small | Click with pitch rising per chip, chip pops on the lot (scale 1 -> 1.12 -> 1, ease-out-back, 0.12 s), coin counter ticks down |
| Ready | Small | Soft chime, your pip fills; opponents' pips pop when they lock in |
| Lot reveal | Small | Cards flip in sequence 0.15 s apart. **Legendary** is Large: slower flip, gold glint |
| Auction resolve | Medium | Winning card arcs into your hand (ease-out), losing bids refund with a reverse coin tick |
| Place a card | Medium | Existing drop-in plus thump, shake 0.3, pitch rises on consecutive placements, "+VP" floater |
| Bad placement | Small | Deny buzz, ghost wobbles, shake 0.1 |
| Payout | Medium | Buildings tally oldest first, each gives a "+N" floater and a thin arc to its Link partner with a cosmetic car; sound pitch climbs; counter rolls up with ease-out |
| Park grows | Medium | Tile squash-and-stretch, ring pulse over the whole park, shake scaled by park size |
| Sapling grows | Medium | Scale tween with overshoot |
| Goal claimed | Large | Banner slides in, fanfare, shake 0.5, goal card flips |
| Round start | Small | Phase banner eases in/out, last 5 s tick |
| End game | Large | Score tally sequence, winner highlight, confetti |

Rules from the skill, applied here:
- **Transient, not permanent.** Everything returns to rest. Shake decays, pops settle.
- **Eased.** No linear tweens. Overshoot (back) for pops, ease-out for settling.
- **Cap noise.** A big payout would otherwise play 30 sounds, so the sequence plays at most 8 sounds and groups the rest into a count.
- **No hit-stop.** This is not an impact game, and a freeze would also stall a real-time board.
- **Never blocks input.** Feedback is visual only and can be skipped by clicking.
- **Accessibility:** a "reduce screen shake" and a "reduce flashing" setting. Shake is already optional by tier.

---

## 4. Milestones

### Implementation checkpoint (26–27 October 2026)

- **M0 implemented.** `code/Cards` contains the exact 21-card playtest catalog with emoji icons, tags, rarity, weights, effect hooks and numeric effect parameters. `code/Match` contains the engine-independent model, settings, event bus and foundation checks. Card effect execution remains in M1–M3 as planned.
- `BoardView.Bind(state, grid)` rebuilds the scene from model cells and subscribes to placement events. Existing art is reused by category and tag tint. The bridge bypasses legacy placement/food rules; the existing game is unchanged until M1 replaces its loop.
- Run checks with `dotnet run --project tests/CitySim.Rules.Tests.csproj`, or run `citysim_test_foundations` in the engine console.
- M0 verification: 30 headless checks pass. The complete project builds against the installed engine assemblies with zero warnings/errors. The earlier editor connection issue was resolved during M1 by allowing `sbox-cli` to access its local connection outside the filesystem sandbox; no second editor is needed.
- **M1 implemented.** `RoundMachine` drives eight rounds of stub Bid → Build → Payout. Defaults use library weights and are shared across players; hands persist up to five cards. `Links`, `Payout` and `PlacementScoring` cover House, Shop, Farm, Cafe and Cottage, with immediate Grant play so every random Common default is usable. Park-pot scoring and remaining effects are still M3 work.
- The old Normal/Chaos modes, piece timers, hunger and delivery economy have been removed. The menu starts a solo match or a debug Sandbox. Multiplayer transport/lobbies return in M5; the M1 scene deliberately runs locally.
- M1 UI: independent event-subscribed hand, resource, phase and placement panels; 3% safe margins; consistent-height 1080p scaling; 150% text; screen stack and pause; keyboard board cursor; selected-card and invalid-state markers. Keys: 1–5 select, arrows aim, Enter place, Space ready, right-click/Backspace deselect, G discard, Esc pause. D-pad/A/B/Y/shoulder bindings are present; the physical controller pass remains outstanding.
- M1 feedback: generated placeholder tick/chime/thump/deny sounds, eased placement drop, decaying camera trauma, ghost denial wobble, phase banners, eased coin rolls, world floaters and decorative Link cars. Payout sound count is capped at eight and the visual tally is skippable. No feedback changes simulation time.
- M1 verification: 33 round checks plus the 30 M0 checks pass headlessly and the round checks also pass inside the engine. Live editor compile succeeds with zero diagnostics. A timed scene run completed all eight rounds and built 16 defaults; payouts and the end screen were observed. Injected input actions verified selection, arrow aiming, Enter placement, deselection and mouse-ray placement; the actual menu click handler was exercised. Pause stopped the clock and board input and focused one overlay button. Placement settled at the tile centre and combined trauma decayed from 0.4 to zero after 1.2 seconds.
- Visual checks: menu, Build, five-card Sandbox, Payout feedback and End were captured. HUDs were inspected at 1280×720, 1920×1080 and 3440×1440, including 150% text. Full physical keyboard/mouse/controller playthroughs and listening to the generated sounds have not been verified. The existing car model reports a missing `material.007.vmat` resource; it remains a placeholder-art limitation for the later asset pass.
- **M2 implemented.** `Auction.Resolve` greedily orders bids, awards one lot per player, favours the lowest VP in a tie, uses stable seat order for remaining ties, and reports winning prices/refunds/discards. `CardDraw` uses rarity unlock rounds and data weights. Ownership limits include Civic cards still in hand.
- `RoundMachine` now runs real mini-auctions by default, with `players + 1` lots, a Hand Target, a Mini-auction Cap, early resolution when all eligible players lock in, and a brief results interval. Full/target hands sit out. The M1 Bid stub remains an explicit harness/debug setting (`EnableAuctions = false`).
- Bids are held in a private host-side `AuctionSession`, outside `MatchState`. Its recipient projection contains only that player's amounts/budget and the public lots/ready states. Requests carry an auction ID so stale clicks cannot affect new reveals. Escrow is virtual: chips reduce displayed available coins, while only the winning price changes the bank. M5 still owns real network privacy tests and transport.
- The Bid sub-view supports click/plus to add, right-click/minus/X to remove, left/right arrows or D-pad to select, Enter/A to add, and Space/Y to lock in. It shows rarity shapes, minimum bids, below-minimum warnings, ready pips, winner labels and refund summaries. Enlarged text uses a scrolling lot area with the budget and Ready footer kept visible.
- `StubOpponent` bids a fixed budget on the first affordable lot, locks in after a short delay, and places in the first legal cells. This is a deterministic test rival, not M6's strategy AI. Defaults and auction awards flow through the same hands and placement rules.
- M2 feedback: staggered card flips, eased chip pops, rising chip pitch, ready-pip pops/chimes, reverse refund ticks, a winning-card arc to the hand, and a slower gold Legendary reveal. Reduced flashing suppresses the Legendary glint. No animation blocks input or controls the simulation.
- M2 verification: **105 checks** pass (30 foundation, 33 rounds, 42 auction); the auction checks also pass inside s&box. Full project and live editor compilation succeed. UI click/right-click handlers and injected keyboard lot controls were exercised. A one-coin bid lost to the stub's two-coin bid and all eight banked coins remained. A live timed eight-round auction match completed eight auctions with balanced accounts and placed 23 human / 24 rival buildings in that test seed.
- Bid, resolution, winning-card transfer and Legendary screens were captured at 1280×720, 1920×1080 and/or 3440×1440. The actual text-size control was used for 150% checks; non-overlapping row bounds and working scroll offsets were inspected. A Legendary glint was observed at opacity 0.97 and became zero under reduced flashing. Physical controller operation and listening to audio remain unverified; the existing car-material limitation remains for the asset pass.
- **M3 implemented (27 October).** All 21 playtest cards execute their rules: weighted/shared park awards and merges; Fountain size; two-payout Sapling growth; Apartment house units/capacity; Factory/Bank income; Town Hall/Chamber tag engines; Guild/Architect triggers; Grant; Land Claim reservations/expiry; Cafe, Museum and Monument end points; and pollution. The model emits explicit placement/growth/award results, and payout/end settlement are idempotent.
- `FinalScoring` explains earned points, card bonuses/penalties, spare coins and the four competitive bonuses. `RoadScoring` finds an exact simple owned-road chain (tree/ring fast paths and a reachability-pruned search for branching cycles), rather than counting a whole branching network. Paid Link units/endpoints accumulate in player stats. Rules conventions and the default park-cap interpretation are recorded in GDD section 8.1; both park-cap policies are tested/configurable.
- M3 UI: effect icons with stack badges and native tooltips, an inspectable tray for each player, hover/focus tag highlighting, reservation flags and ghost targeting, and a scrolling explained score screen with skippable tally and winner markers. Long card/tray/score content scrolls at 150% text without compressing overlapping rows. Emoji/tinted existing models remain the visual placeholders.
- M3 feedback: shared park floaters and a region pulse, eased squash/stretch, Sapling-to-Forest scale overshoot, effect-icon pop-in, small event-card cues, and end tally/fanfare/confetti. Reduced flashing suppresses confetti. The end transition clears prior world feedback so it cannot cover score explanations; all effects remain outside the simulation.
- M3 verification: **155 checks** pass (30 foundation, 33 rounds, 42 auction, 50 core cards), including the hand-calculated **40–6** score fixture and an independent road-solver comparison over all **511 nonempty 3×3 boards**. The 50 core-card checks also pass inside s&box. SDK and live editor compile cleanly. A live eight-round match reconciled all final totals to the authoritative model and explanation rows; a later enlarged-text run also reconciled and showed 20 confetti pieces with zero leftover world floaters over the score screen.
- Live M3 checks observed Bank ×3 and all seven effect icons/tooltips, tag highlight/dimming and tint restoration, Land Claim through mouse-ray input (empty cell preserved, card consumed, flag label present), reservation expiry, a Forest park size of 3, a growth overshoot settling at scale 0.25, and a park pulse. Build/tray/hand and end screens were inspected at 720p, 1080p and ultrawide, including 150% text; scroll offsets confirmed access to long score/card content. Physical-controller testing, listening to sounds and the pre-existing car-material art issue remain for later verification/polish.
- Card data now has a versioned cache refresh because the editor preserves old static definitions across play restarts/hotloads; this prevents stale effect parameters after schema/data updates.
- **M4 implemented (27 October).** Nine public goals use pure condition evaluators and a seeded draw/discard pile. Three distinct goals are public; claims settle once at the end of Build, before income/growth, paying 5 VP to a sole claimant or 3 VP to each simultaneous claimant. Claimed cards show the winners for the rest of the round, then refresh; unclaimed slots stay. Exhausted decks recycle without duplicate active goals.
- Eight secret contracts deal two distinct offers per player and gate the first round until each player keeps one. Private sessions return only owner choices/progress; no contracts enter the public model until final reveal. Printed VP is awarded once at final scoring, with failed contracts shown for zero. Public goal VP and contract VP have separate explanation rows without double counting. GDD section 8 records the interpretation of checkpoints, tags, connectivity, paid Link units and deck recycling, and removes the stale duplicate eight-building contract table.
- M4 UI/feel: a pre-match contract picker with public goal previews; collapsible scrolling goals/owner-contract panel; focus isolation and keyboard choice/confirmation; live progress driven by model events. A bundled large goal banner/chime/shake survives the following smaller phase announcement, and goal cards flip with easing. Contract rows unlock/flip during the skippable final tally. Banners leave the goal-panel close control clear. Feedback never controls scoring or the simulation clock; inspecting goals keeps the clock running. Reused score panels now subscribe to scoring events so rapid replay cannot leave them blank.
- M4 verification: **210 checks pass** (the prior 155 plus **55 objective checks**), covering all conditions, sole/shared awards, checkpoint timing/idempotence, replacement/recycling, offer privacy/projections, invalid selections, start gating and exact final reconciliation. All 55 objective checks also pass inside s&box. SDK build and live compilation succeed with zero diagnostics; the final console interval has no new CitySim warnings.
- Live checks: native UI clicks kept the second contract and started round 1 from a frozen round-0 picker. Directly asserted keyboard actions through the panel handler also selected/confirmed the second choice. Rainy Day claimed for 3 VP each in a two-player fixture and 5 VP in a solo fixture; the large banner persisted through Payout. The clock advanced with goals open, scrolling reached the owner's secret contract, claim flips settled after 0.35 seconds, and camera trauma returned to zero. A manually accelerated eight-round match revealed both unmet contracts and reconciled every total; a second eight-round fixture fulfilled Hoarder for exactly 5 VP and resisted duplicate settlement.
- Picker, goals and score layouts were inspected at 720p, 1080p and/or ultrawide, including the actual 150% text setting. The normal two-choice picker keeps both choices and confirmation visible at 720p/150%; longer goal/score content scrolls. Physical-controller operation, listening to the generated sounds, and the pre-existing car-material art issue remain unverified/deferred. Actual transport privacy and multi-client play remain M5 work.
- **M5 in progress (27 October): rules/protocol foundation implemented.** Recipient snapshots separate public board/resources/hand counts/ready states from the owner's hand, contract and bid view. They omit the authoritative seed and random/deck state; the contract wire token is no longer the seed-derived internal draft token. Replica models contain empty rival hands and reject wrong-owner, stale and retired-match snapshots.
- `MatchAuthority` accepts the authenticated caller separately from command data, scopes requests to match/round/phase/auction, validates the host clock and selected card/hand version, and rejects replayed commands. Hand versions prevent a delayed placement from consuming a second identical card after the first shifted the hand. Sealed bids remain in the host's private session.
- Full host checkpoints round-trip the board, reservation expiry, hand versions, private offers/selections/bids, goal draw/discard piles, mini-auction and readiness state, payout guards, cached scores and command sequences. Explicit RNG state replaces opaque `System.Random` state for match/goal/contract draws, preserving the exact next draw after restoration. Seeded sequences change from the earlier local-only implementation; reproducibility and draw weights remain intact.
- Public lobby rules cover up to four seats, ready-up, host-only start, contract-pick/play/end transitions, late-join observers, host promotion, departed seats, same-identity rejoin and rematch admission. `CityNetwork` now connects these rules and filtered projections to authenticated static RPCs. `CityGame` routes network bids, Ready, placements, discards, events and contract choices through `MatchAuthority`. Received presentation results drive the existing client feedback without replaying rule mutations. Multiplayer pause keeps the server clock running.
- M5 UI: host/join controls, an event-driven lobby with ready markers and a transient ready-row pop/chime, host-only start, a scrolling scoreboard, spectator labels/public lots, and host rematch/leave controls. Runtime grid, camera, ground, HUD and cars stay local with `NotNetworked`, so they do not duplicate during joining or handoff. Network contract drafts use an independent match token, rejecting stale queued picks without exposing the deal seed. Snapshot delivery counters are per recipient, avoiding gaps that reveal other players' private bid-edit activity. Rejected network placements return only to their caller rather than triggering the host's denial feedback.
- Stable seats now follow engine-authenticated Steam accounts rather than disposable connection IDs. The host-only seat registry revokes superseded connections, survives checkpoint version 2, and restores the last accepted command sequence to a returning owner. Late disconnect callbacks cannot remove a replacement connection. Clients cannot provide or change the account used to claim a seat.
- M5 rule verification: **283 headless checks pass** (prior 210 plus **73 network/lobby checks**); all 73 network checks also pass inside s&box. JSON round trips replay through eight rounds with matching future draws, board/economy/goal state and final scoring. SDK and editor compilation are clean.
- Migration transport: `HostMatchVault` is restricted to a dedicated `NetworkMode.Never` root. A temporary marker was absent from the installed engine's ordinary network scene serialization and present with its handoff serialization option; the probe object was removed. The engine's [handoff source](https://github.com/Facepunch/sbox-public/blob/master/engine/Sandbox.Engine/Systems/Networking/System/NetworkSystem.HostMigration.cs) sends the full handoff only to the successor and rebuilds ordinary resync snapshots for the other peers. Live clients received no vault; the elected successor received the private payload and restored its authority. Remote connections cannot spawn, refresh or destroy scene objects; normal Ready commands and filtered payouts were verified with these permissions disabled.
- M5 live verification (27 October): three real local instances joined one lobby and exercised their native Ready/contract buttons. A first match finished with identical **18/14/9 VP** results on every instance. A second action-driven run observed rounds 1–8 on all three instances, with eight bid submissions and 24 card actions per player; every model and explanation total agreed at **41/33/21 VP**. During play and at the end, both non-host clients had empty rival hands, seed zero, no `RoundMachine` and no host vault. The host retained the full authoritative state.
- A fourth instance joined an active sealed auction as an observer with the correct round/phase/clock/board and four public lots, no owner payload, no cards, no build input, seed zero and no vault. Each client had exactly one grid and camera. A graceful host departure restored the identical full-match fingerprint and RNG state on the elected successor, including all three hands/contracts and the locked three-coin bid. The eventual auction history confirmed the one-coin losing bid refunded, while the two- and three-coin winners paid exactly once.
- Live account recovery passed: a player left, restarted with a new connection GUID, and recovered the original seat, Street/Shop hand, Industrialist contract, sealed two-coin bid and command sequence. The seat retained its offline auto-ready lock. The returning player and two peers then completed eight rounds with **34/29/21 VP**, matching model and explanation totals everywhere and empty rival hands on both clients.
- **Migration verification remains incomplete:** on installed engine **26.10.13c**, the remaining loopback peers took the engine's full reconnect fallback and failed inside `Sandbox.GameInstanceDll.CreateGameNetworkingAsync` with a null reference, leaving no active scene. This repeated with the version-2 registry; even a fresh peer failed to load the game assembly after the successor began hosting. The successor retained the correct account registry, state and authority, but this is not a verified successful handoff for every peer. Recovered runtime fixtures were saved to `.build/m5-migration-checkpoint.json` and `.build/m5-migration-v2.json`. Stable seat recovery itself now passes; the engine's local reconnect path remains the outstanding gate. Do not mark M5 complete on the successor-only result.
- Missing/incompatible migration checkpoints now fail closed: no replacement lobby or commands, frozen clock, a recovery explanation and one focused Leave control. The negative test retained the existing lobby, and clicking the native Leave button returned to Menu with networking off, error cleared and vault removed. Recovery layouts were visually inspected at 720p and 1080p with 150% text after restarting the editor to clear stale engine styles from the failed rejoin.
- Lobby/three-player score/scoreboard screens were inspected at 720p; enlarged-text lobby controls also fit at 720p/150%, and its Ready pop returned to rest. Ultrawide layout was captured for inspection. A live recipient received payout phase/coin cues and exactly one three-coin bank increase (8 to 11), with no rival cards or host vault. Compiled feedback sounds resolve on the client and `Sound.Play` reported playing; startup source-resource warnings did not indicate a persistent missing compiled sound. Physical controller use, listening to sounds and the remaining placeholder-material art pass are unverified/deferred.
- A fresh waiting-lobby reproduction at 16:45 on 27 October reproduced the same peer scene loss and engine exception before any match was created. The successor kept the lobby and one vault with no recovery error. Reproduction steps, observations, limits and the retest gate are in [M5_HOST_MIGRATION_REPRO.md](M5_HOST_MIGRATION_REPRO.md). Both temporary clients were stopped and the editor returned to stopped play with its saved scene intact.
- A separate, unchanged `game.minimal` template reproduced the same failing peer reconnect at 17:06, without City Sim code, checkpoint or vault. The engine's developer-host flag stayed true; the reconnecting peer lost its game ident and scene. The exact null operand remains unknown. The stock probe and metadata comparison are recorded in the reproduction report; its temporary games/editor were stopped.
- Recovery hardening now rejects missing settings, contradictory lobby/match state, invalid clocks/IDs/sequences and recipient revisions before installing authority. Only active authenticated channels are rebound; the successor must own a connected saved seat. Three malformed checkpoint variants reached the Recovery screen without replacing the lobby. A real two-instance active-match promotion retained the successor's seat and Street/House hand with one vault and no recovery error.
- **User-directed UI update (27 October):** the new `punchboard-design-system` skill and native `Pb*` library now drive the menu, pause/recovery, lobby, hand/auction, resource/timer/effect/placement HUD, goals/contracts and final scores. Existing rules, private projections and feedback hooks remain. Native menu activation, auction plus/minus, contract confirmation, Ready, pause/resume and semantic menu navigation/back were exercised; goals and score content scroll. Screens rendered at 720p and 1080p. Physical controller use remains unverified.
- **Current sizing decision supersedes earlier 150% checks:** the user requested a much smaller UI and removal of the text-percentage selector. The game now uses one fixed compact HUD scale (`ScreenPanel.Scale = 0.65`), reapplied after hotload, narrower side panels, smaller hand cards, a short centred hand hint, and compact resource counters. Empty effects collapse to an Inspect button that retains access to all players. Both menu and pause text-size controls were removed. The revised Build HUD was checked at 1280×720, 1920×1080 and 1920×914; hint and selected-card overlaps were corrected. The generic library's optional large-text styles are reusable but are not exposed by the game.
- **M5 remaining:** recover/verify all peers after migration. This verification remains open after the same failure was reproduced in an untouched engine template. M6 follows the completed application work while the independent engine gate stays recorded; full-plan completion still requires resolving or explicitly accepting this limitation.
- **M6 implemented (27 October):** solo setup now selects 1–3 rivals and Easy/Normal/Hard through the compact Punchboard controls. The board scales with the total player count. Mira, Rowan and Jules replace the live fixed-bid stub; `StubOpponent` remains only for the earlier auction fixtures. Solo standings expose public VP, card counts and readiness.
- `Bot` uses its own contract/hand/auction view and public board, goals and resources. It never copies rival hands, sealed bids, hidden contracts, seed or future draw state into evaluation clones. Every legal spot gets a placement preview and a bounded shortlist receives exact placement, payout, public-goal, own-contract and end-game-card-effect evaluation. Difficulty changes noise, bid greed, candidate budget and income horizon. Each update performs at most one action after a short bounded thinking delay; rules and input are never paused for AI.
- `FinalScoring.CardEffects` is shared by final scoring and bot evaluation, covering Monument, Museum, Cafe and Factory pollution without reimplementing their effects. Bots respect bidding budgets/minima, locked readiness and deadlines, and use the ordinary Grant, Land Claim, discard and placement APIs.
- **M6 verification:** **309 headless checks pass**, including **26 bot checks**; the 26 also pass inside s&box. Fixed seeds reproduce decisions and final scores, rival private hand/bid changes do not alter a bot's decision, hard bots prefer a six-VP Monument over a late Bank, and the difficulty fixture produces different resolved prices (Easy pays 2/keeps 6 coins; Hard pays 3/keeps 5). Two-, three- and four-bot matches complete all eight rounds; the 18×18 four-bot stress match took about 1.1–1.3 seconds headless (not a per-frame production budget).
- A live four-player match using normal game clocks and three Hard rivals completed eight rounds: **Jules 42, Mira 34, Rowan 30, You 14 VP**. Each rival built 23 tiles and finished with no cards. Every score-line sum, model VP and displayed total agreed. All native rival-count/difficulty buttons were exercised; a separate Normal two-rival run reached Build with three players and the correct hand/focus.
- The full match exposed retiring hand cards being animated indefinitely. `PbHand` now removes retiring faces before applying fan transforms. Three repeated empty/refill cycles rendered exactly **0/2/0/2/0/2** cards, and a real round transition displayed two cards for a two-card hand. Menu and hand focus, compact HUD and auction layout were inspected in the engine. Physical controller use, listening to audio, and a strict live frame-time profile remain unverified. M7 and M8 are next; M5's external reconnect gate remains open.

- **M7 in progress (27 October):** 40 of the GDD's 48 named drawable cards are implemented. The earlier “about 65”/“45 more” estimates do not introduce unnamed cards. New batches cover Industrial Estate/Housing Authority/Eco Council; Roundabout/Contractor; Avenue/Market/Orchard/Lake/Vineyard/Startup; Tourist Office/Tax Office/Depot/School/Granary; and Stadium/Airport/Skyscraper. See [M7_CARD_COVERAGE.md](M7_CARD_COVERAGE.md) for the remaining eight.
- Contractor consumes its per-source, per-round cap only on successful placement. Usage survives host checkpoint restore and is copied into bot evaluation without copying private rival data. Tag checks distinguish Home-tagged Civic cards from residential buildings. Growth income uses completed payouts since placement. Granary bonuses create no new Link. Signed Stadium penalties net against simultaneous income and saved coins, with a zero bank floor and an exact payout explanation. Airport penalties share one three-VP cap per player. Skyscraper requires two adjacent buildings as well as a road.
- **M7 verification so far:** **405 headless checks pass**, including **96 new card checks**, and all 96 pass inside s&box. The expanded catalogue invalidated a draw-dependent difficulty fixture; it now uses controlled Monument lots with goals/contracts disabled and observes Easy paying 7 versus Hard 8. The 18×18 four-bot match still completes (about 1.24 seconds headless). Nineteen new cards were placed via native validation, including a Skyscraper site prepared with two Parks; procedural silhouette names and settled scales were inspected in the engine. SDK and live compilation pass.
- Persistent settings now expose effects volume, reduced shake and reduced flashing through menu/pause Settings. Native actions and cookie reload were verified; solo Settings preserved the paused clock. Injected semantic actions verified Goals/Back, discard, hint dismissal, one placement from a focused hand card, and no placement from a focused non-hand control. Original preferences were restored after testing. These are semantic handler tests, not physical-controller or audio-listening evidence. Separate-process persistence and the multiplayer Settings clock still need verification.
- **M7 second checkpoint:** 45 of 48 named cards now execute. Central Station counts received paid Link endpoints (including both ends when owned), without counting Granary bonuses; Senate scores qualifying owned tags. Ribbon Cutting uses explicit committed building history and configured cap; Rush Hour adds one capped doubling of received Link income for this round. Extra Rush Hour copies remain in hand rather than stacking. Both events use authenticated versioned commands, public activity snapshots, checkpoints, native hand actions and bot sequencing. Hospital uses a checkpointed once-per-Build attempt at the configurable midpoint or before early completion; a random residential cell is answered by the nearest in-range Hospital, oldest placement breaking ties. Range/award come from card data. Notices reveal board cells rather than private information.
- **Current verification:** **444 headless checks pass**; all **135 M7 card checks** also pass inside s&box. SDK build is clean. Native Ribbon Cutting changed coins from 8 to 9 after one building; native Rush Hour activated with no immediate coin change. A real 3.52-second run observed one Hospital response, VP 0→1, and the matching feedback cue. The compact board inspector rendered Farm name, owner, rarity, tags and rules after a fresh play start; it follows hover or the keyboard board cursor. Latest full four-bot headless stress run took about 1.42 seconds. Original preferences and solo options were restored and the editor returned to Menu.
- **M7 full card implementation checkpoint:** all 48 named cards now execute. Warehouse gives its extra Street and a once-per-round Street placement coin. Builders' Yard exposes a saved next-round Street/Common preference in the effect tray, using an authenticated owner command and owner-only snapshot field. Renovation uses a two-card, owned-target flow with pure validation and atomic commit; old models/effects are removed and replaced, prior earned points remain, and explicit building/trigger history prevents reuse after replacement. Bots can renovate when a full board blocks a useful building and choose Yard defaults.
- The full M7 rule checkpoint passes **486 headless checks**, including **21 Builder**, **19 Renovation** and **28 bot** checks. Final-card and bot checks also pass inside s&box. A native Bank→House replacement through card callbacks and injected Confirm consumed both cards, destroyed the old model, retained cell occupancy, restored ground and settled at scale 0.25. The native Yard Common button changed the owner preference and rendered toggled. SDK/live compilation pass. Physical input and the remaining presentation audit are still open; the card catalogue is not by itself a completion claim.
- **M7 verification update:** all 175 M7 card checks pass, with 28 bot checks at the 486-check card checkpoint. The later M8/scheduling-inclusive suite passes **500 checks**. All 175 card checks and 28 bot checks also pass in the current engine build. Semantic input checks cover Menu/Solo options/Lobby/Contract/Bid/Build/Goals/Pause/Settings/End, including add/remove bid, Ready and Back. A dedicated **I / right-stick-click** controls mode makes Builder choices and scoreboard rows reachable by D-pad navigation; **B** returns to the board. It gates camera, placement and bid input, skips hidden/deleting controls, and recovers focus after a row disappears. Controller-style navigation activated a rival scoreboard row and selected that owner's tray. Physical gamepad hardware and sound listening remain unverified.
- A real two-peer M7 test propagated the client's Builder choice without changing the host's choice, rejected a stale Renovation hand version, then consumed both valid cards and replaced Bank→House on host and client. The client destroyed its old model, restored the ground and retained an empty rival hand. A repeated native House→Park→House test preserved the empty-cell count, hid/restored ground correctly and left one settled building. The temporary client was stopped and all settings/preferences/launcher arguments restored.
- Payout feedback now caps individual cues and sounds at eight, groups further sources into one count, and draws fading Link-only arcs. A native twelve-entry mixed-sign fixture emitted eight individual cues plus one group, five Link cues, signed deductions and transient arc segments without changing the bank; feedback expired to zero floaters. The arc was captured in the native renderer. Renovation cursor feedback describes replacement/errors, and scoreboard buttons open the requested owner's tray. The compact title/control layout and full placement panel were inspected at 720p; 1080p and 3440×1440 also rendered. Text-size selection remains removed.
- **M8 implemented harness and initial baseline:** `SimulationHarness` runs seeded four-strategy bot matches, rotates each board seed through four seats, uses live 15×15 sizing and the seed-derived road cross, and records all provenance/settings, fractional win shares, scores and post-growth round VP/coin curves. Both normal CLI output and JSON include curves. Checks cover replay, rotation, shared seeds, live sizing, complete curves, legal budgets and Sapling growth sampling. See [BALANCE_BASELINE.md](BALANCE_BASELINE.md) and its saved 100-match JSON. The current paired run finished in about 126 seconds with no rejected actions or budget violations: Balanced won 44, Industry Tag 10, Park Rush 15 and Economy First 31. The pre-scheduling baseline is archived separately because bot timing changed outcomes. No card or economy values changed: these bot proxies and 25 distinct board seeds do not establish a justified numeric adjustment without human playtests.
- **Live AI profiling:** an initial native eight-round run found a 41.355 ms bot-group update during Bid. Auction evaluation now processes one lot per update, and a rotating 4 ms soft group budget defers remaining bots. Exact candidate quality and ordinary action delays remain. The next native run had median 0.0209 ms, p99 6.2467 ms and maximum 36.7414 ms; a single evaluation is indivisible, so this is not a hard frame-time guarantee. Seeds differed, preventing a controlled improvement claim. [AI_FRAME_PROFILE.md](AI_FRAME_PROFILE.md) retains both raw profiles and the measurement scope. Pending work/deadline/cancellation and scheduler rotation checks pass.
- Remaining full-plan verification includes the independently reproduced M5 peer-reconnect gate, physical gamepad/audio verification and human balance playtests. Rare AI evaluation spikes remain a measured performance limitation, not an unmeasured whole-match timing claim.

Sizes are rough: **S** about a day, **M** a few days, **L** a week or more.

### M0: Foundations (S-M)
**Build**
- `Tag`, `Rarity`, `CardDef`, `CardLibrary`, `MatchSettings` (all tunables).
- `MatchState` model and a `BoardView` bridge so `CityGrid` can render from it.
- `MatchEvents` bus.
- A tiny test harness (a console command or editor test) that runs rules on a `MatchState` and prints results.

**Done when:** the card library loads all 21 playtest cards and a harness test can place a card on a `MatchState`.

### M1: Single-player rounds, no auction yet (M)
**Build**
- `RoundMachine` with Bid (stubbed), Build and Payout phases and timers.
- Default cards dealt to the hand each round (1 Street + 1 random Common).
- Hand bar, placement from hand with the ghost showing "+VP here".
- `Links`, `Payout` for House, Shop, Farm, Cafe, Cottage and base income. Cosmetic cars from Links.
- Resource panel, top bar, phase banner.
- Remove the old hunger/delivery economy and the Normal/Chaos/Sandbox modes. Keep Sandbox as a debug mode.

**Feel:** place thump, deny buzz, payout tally with floaters, round banner.

**Done when:** you can play 8 rounds alone, place your defaults, and watch payouts add up deterministically.

### M2: The auction (M)
**Build**
- `Auction.Resolve` (greedy, one win each, refund, tie-break by lowest VP) with unit-style tests.
- Mini-auction loop: reveal `players + 1` lots, Hand Target and Mini-auction Cap.
- Bid screen: lot cards, tap to add, right-click to remove, Ready.
- Hidden bids on the host (single player first, with a stub opponent).

**Feel:** chip pop, rising click, flip reveal, card arc to hand, Legendary glint.

**Done when:** a round runs Bid -> Build -> Payout and you can lose a lot to a stub opponent's bid.

### M3: Core cards and scoring (L)
**Build**
- The 21-card playtest set (section 5.6 of the GDD) with their effects.
- `ParkScoring` (placer and other owners, merge, cap), Sapling growth.
- Effect tray with tooltips. Tag highlighting on hover.
- End-of-game scoring and bonuses (Longest Road, Biggest Park Owner, Best Connected, Full House).
- End screen.

**Feel:** park pulse, sapling growth, tray icon pop-in, end-game tally.

**Done when:** all 21 cards work, and a full solo match ends with a correct, explained score.

### M4: Goals and contracts (M)
**Build**
- `Goals` (3 public, checked at end of the active phase, 5 VP or 3 VP when tied) and `Contracts` (deal 2, keep 1).
- Goals panel, contract pick screen at match start.

**Feel:** goal banner and fanfare, contract reveal at the end.

**Done when:** goals claim and replace correctly, ties split, contracts score at game end.

### M5: Multiplayer (L)
**Build**
- Host-held bids, filtered hands, broadcast results.
- Lobby screen: ready-up, host start.
- Snapshot for late joiners including the round and phase.
- Host migration: rebuild hands and the deck from the snapshot.
- Scoreboard, ready pips.

**Done when:** 3 players in one lobby complete a match, and no client can read another's bids or hand.

### M6: Solo AI (M)
**Build**
- `Bot` using the same rule classes: score each lot and each placement spot by expected coins and VP, bid within budget, place with a short thinking delay so it looks human.
- Difficulty as parameters (greed, randomness, lookahead).

**Done when:** a solo player gets 1-3 rivals that bid, build and score sensibly, and difficulty visibly changes the result.

### M7: Full card set and polish (L)
**Build**
- Remaining cards from the GDD (about 45 more), in batches by group: tag engines, triggers, landmarks, growth, Civic, events.
- Missing models or placeholders for Farm, Factory, Bank and others.
- Settings screen (reduce shake, reduce flashing, volume), pause screen. Text-size selection was removed at the user's request on 27 October; retain the compact fixed sizing.
- Gamepad pass over every screen.
- Tooltips and onboarding hints for the first match.

**Done when:** every card in the GDD works, and the game is playable on mouse and gamepad.

### M8: Balance and tuning (ongoing)
- A **simulation harness**: run N bot-vs-bot matches headless and print win rate by strategy (tag, park-rush, economy-first), average VP and coin curves.
- Tune the *(tune)* values from sim results and real playtests. Record each change in the GDD.

---

## 5. Order of work and what can run in parallel

```
M0 -> M1 -> M2 -> M3 -> M4 -> M5 -> M6 -> M7
                          \-> M8 (starts after M3, continues)
```

- **Rules and UI can split** from M1 onward: one stream builds the rule classes, the other the panels, joined by `MatchEvents` and the result types (section 2.2).
- **M5 (multiplayer) comes after the single-player loop is fun.** The existing networking pattern is reused, and the pure-rules split keeps the change small.
- **M8's harness is worth building by M3.** It checks the balance of the new numbers long before there are many players to playtest with.

---

## 6. Risks and mitigations

| Risk | Mitigation |
|---|---|
| The refactor to a pure `MatchState` is bigger than expected | Do it in M0-M1 while there is little code to move; keep `CityGrid` as the view |
| Real-time placement lets fast clickers win | Keep goals end-of-phase; claim tiles on the host with a short grace window; test with latency |
| Auction feels slow or unfair | Mini-auction pacing is in `MatchSettings`; playtest at M2 before building cards |
| 65 cards is too much to balance | Playtest on 21 first (M3), add the rest in groups with the harness (M7, M8) |
| Tooltips and tags overload new players | Tag highlighting, hover rules text, a short first-match hint |
| Hidden info leaks over the network | Bids and hands only on the host; clients get filtered RPCs; test with a second client |
| Feedback becomes noisy | Tiers, a sound cap per payout, reduce-shake and reduce-flashing settings |
| Missing art for new buildings | Use tinted placeholder models per tag until proper ones exist |

---

## 7. How each milestone is checked

- **Rules:** harness tests for `Auction.Resolve`, `Payout.Calculate`, `Links.Build`, `ParkScoring` and `Goals` with hand-built boards and known answers.
- **Play:** run the game with the editor tools (`sbox-cli`), enter play mode, take screenshots of each phase and each panel.
- **Resolutions:** check the compact fixed HUD at 1280x720, 1920x1080 and 3440x1440. Text-percentage selection was removed at the user's request.
- **Input:** play one round with mouse only, keyboard only, and gamepad only.
- **Network:** run two clients and confirm bids and hands stay private.
- **Feel:** watch each event from section 3.2 fire and return to rest, and confirm the shake decays and input is never blocked.

Each milestone report says what was observed and what was not verified.

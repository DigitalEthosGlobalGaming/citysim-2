# Build-plan acceptance status

27 October 2026. This records the evidence boundary for the full plan; implemented code and passing fixtures do not waive the open gates below.

| Requirement | Current evidence | Status / limit |
|---|---|---|
| M0 original 21 cards, pure model, placement harness | Original IDs/metadata remain in FoundationChecks; pure Match code builds headlessly; model-to-scene bridge is active | Implemented |
| M1 eight deterministic rounds, defaults, Links, payouts, hand placement and removal of old economy | RoundChecks, retained-default and payout fixtures; native eight-round runs; Match/Sandbox replace old Normal/Chaos modes | Implemented; later milestones add rivals |
| M2 greedy hidden-bid auction, minima, refunds, tie rules, mini-auction lifecycle | AuctionChecks; recipient projection excludes rival bids; previous native winning/losing/refund runs and current semantic add/remove/Ready checks | Implemented |
| M3 original card rules, parks/growth, explained final scores and competitions | CoreCardChecks, known score fixtures, road-solver comparison; current engine card checks and native replacements | Implemented |
| M4 public goals and private contracts | ObjectivesChecks, seeded goal/contract replay, owner filtering and native contract selection | Implemented |
| M5 lobby, authenticated commands, owner-only hands/bids, late join/reconnect and three-player completion | NetworkChecks, earlier real three-player completion, real two-peer M7 stale/valid commands and private-hand/model checks | Application implementation verified in these cases |
| M5 all remaining peers survive host migration | Successor restores its private checkpoint; another local peer loses its scene/game ident in City Sim and an untouched engine template | **Open external engine gate**; see M5_HOST_MIGRATION_REPRO.md |
| M6 1–3 parameterized rivals using shared public-state rules | BotChecks, seed replay/privacy/difficulty fixtures, full bot matches and real three-Hard-rival frame profiles | Implemented |
| M6 responsive AI scheduling | One lot evaluated per update; rotating 4 ms soft group budget; cancellation/deadline/rotation checks and native frame measurements | Measured limitation: individual evaluations can exceed the budget; see AI_FRAME_PROFILE.md |
| M7 every named GDD card | 48 catalogue entries, 175 M7 card checks passing headlessly and inside s&box, live final-card workflows | Implemented; see M7_CARD_COVERAGE.md |
| M7 placeholders | Reused models/emojis and procedural farm/factory/civic/lake/office/landmark silhouettes | Allowed by user; final art/material cleanup deferred |
| M7 settings, pause, first-match hints, tooltips and compact UI | Persistent volume/shake/flashing settings, pause stack, hint dismissal, board/card/effect inspection; fixed scale 0.65 and no text-size selector | Implemented; native layout checks at 720p, 1080p, wide screenshot ratio and 3440×1440 |
| M7 pointer/keyboard/controller action paths | Native callbacks and semantic actions cover menus, lobby, contract, bid, build, goals, pause, settings, end, Builder choice, Renovation and rival tray inspection | Semantic checks pass; actual mouse/keyboard pass being completed separately |
| M7 physical gamepad and sound listening | Semantic gamepad actions and input bindings exist; sound handles/resources were exercised | **Unverified physical device/acoustic tests** |
| Feel: placement/denial, auction, parks/growth, goals/end, transient payout feedback | Event-driven Feedback/FeedbackPanel, prior native milestone observations, current Hospital/renovation/grouped-payout/arc observations; animations return to rest, feedback never changes simulation time | Implemented and targeted native checks; not an acoustic listening claim |
| M8 N-match strategy harness with wins, mean VP and coin/VP curves | CLI text and JSON reports, seven deterministic harness fixtures, saved 100-match paired baseline and settings/seed provenance | Implemented; see BALANCE_BASELINE.md |
| M8 numeric tuning informed by real playtests | Current and previous timing-policy baselines are saved; no card/economy adjustment is justified by bot proxies alone | **Human balance playtests open**; no numeric rule change claimed |

## Reproduction commands

From `city_sim`:

```powershell
dotnet run --project tests/CitySim.Rules.Tests.csproj
dotnet run --project tests/CitySim.Rules.Tests.csproj -- --simulate --matches 100 --seed 12345 --json
```

The coordinated suite passes 500 checks. The SDK project builds with zero warnings/errors. Native checks are separate from these headless commands.

Runtime evidence lives in `.build/m7-*`, `.build/m6-ai-frame-profile*.json` and the saved screenshots. Durable simulation/profile reports are under `docs/balance` and `docs/performance`. Some M5 runtime checkpoint fixtures include private hands/bids and must remain local; they are not ordinary client snapshots or public report attachments.

The full goal remains unproven while the external migration gate and physical/human verification gates remain open. Earlier 150% text requirements were superseded by the user's explicit compact fixed-size request.

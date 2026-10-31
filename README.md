# City Sim

A competitive, eight-round city-building game for s&box. Buy cards from a shared market, build on a shared board, earn income, and compete for victory points.

## Open the game

Open `city_sim.sbproj` in the s&box editor and run `scenes/minimal.scene`. The editor generates the game and editor project files. The project includes its imported asset sources and license notices under `Assets`.

## Rules and balance

- [Game design](docs/GDD.md)
- [Current 48-card catalog](docs/CARD_CATALOG.md)
- [Latest balance changes and evidence](docs/BALANCE_LOCAL_PLACEMENT_2026-10-30.md)
- [Earlier balance implementation](docs/BALANCE_IMPLEMENTATION_2026-10-30.md)
- [AI redesign, solo setup and evidence](docs/AI_REDESIGN.md)
- [Current AI ratings and weighted choices](docs/AI_WEIGHTED_CHOICES.md)
- [Local balance analytics](docs/ANALYTICS.md)
- [Known host-migration issue](docs/M5_HOST_MIGRATION_REPRO.md)

The current rules use independent income, one-time Street and building road bonuses, fixed park placement VP plus a Nature-neighbour bonus, market prices of 1/4/7/11, and free placement. Fifteen of the 40 building cards require roads. Balance still needs human playtesting.

Play opens Game setup: choose 1–4 named AI opponents and set each difficulty separately. Your setup is saved locally. Multiplayer supports up to four human players.

## Run the pure rule checks

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet run --project tests/CitySim.Rules.Tests.csproj
```

To simulate the current rules:

```powershell
dotnet run --project docs/balance/lab/BalanceLab.csproj -- .build/balance-run.json 32 30102026 final
```

Engine caches, generated project files, compiled asset caches, and local build/test captures are excluded from version control. Publishing to GitHub does not publish the game to the s&box platform.

To compare the AI difficulties at every supported opponent count, with seat rotations and a Normal human proxy:

```powershell
dotnet run --project tests/CitySim.Rules.Tests.csproj -- --ai-benchmark --boards 4 --seed 30102026
```

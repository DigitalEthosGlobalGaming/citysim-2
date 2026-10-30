# City Sim

A competitive, eight-round city-building game for s&box. Buy cards from a shared market, build on a shared board, earn income, and compete for victory points.

## Open the game

Open `city_sim.sbproj` in the s&box editor and run `scenes/minimal.scene`. The editor generates the game and editor project files. The project includes its imported asset sources and license notices under `Assets`.

## Rules and balance

- [Game design](docs/GDD.md)
- [Current 48-card catalog](docs/CARD_CATALOG.md)
- [Balance implementation and evidence](docs/BALANCE_IMPLEMENTATION_2026-10-30.md)
- [Local balance analytics](docs/ANALYTICS.md)
- [Known host-migration issue](docs/M5_HOST_MIGRATION_REPRO.md)

The current economy uses independent building income, owner-only marginal park scoring, market prices of 1/4/7/11, and free placement. Balance still needs human playtesting.

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

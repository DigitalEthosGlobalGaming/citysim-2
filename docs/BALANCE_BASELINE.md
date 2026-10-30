# Balance baseline — 27 October 2026

The M8 harness runs the real pure rules with four Normal bots: Balanced, Tag (Industry), Park Rush and Economy First. These are strategy proxies, not optimal players. Each block of four games shares a match seed and road cross while rotating the strategies through every seat. A strategy retains its bot seed through that rotation. Four-player board dimensions use the same settings formula as the game: 15×15 with current defaults.

Run from `city_sim`:

```powershell
dotnet run --project tests/CitySim.Rules.Tests.csproj -- --simulate --matches 100 --seed 12345 --json
```

Progress goes to stderr; JSON goes to stdout. Omit `--json` for a concise text report. `--matches` accepts 1–10000; use a multiple of four for complete seat rotations. The default invocation without `--simulate` runs the rule checks.

The report includes settings, board dimensions, road coordinates, match and bot seeds, strategy/seat mapping, final scores, fractional tied-win shares, per-round VP/coin curves, rejected actions and budget violations. Curves are sampled after payout and growth, before end-game bonuses; final VP includes those bonuses. Incomplete matches or invalid actions fail the run. The harness advances at fixed 0.25-second steps; it models rules and bot policy, rather than render-frame scheduling or GPU cost.

## Recorded run

[Current 100-match JSON](balance/2026-10-27-seed-12345.json), seed 12345, 25 paired seed blocks, approximately 126 seconds headless after incremental bid evaluation was added. Zero rejected actions or budget violations. Harness checks cover deterministic replay, seat rotation, shared seeds, live board sizing, complete curves, win-share conservation and a known Sapling growth result. Additional scheduling checks cover partial evaluation, deadlines, cancellation and fair frame-budget rotation. The complete suite passes 500 checks.

| Strategy | Win share | Mean final VP | Mean coins after round 8 | Mean VP after round 8, before bonuses |
|---|---:|---:|---:|---:|
| Balanced | 44% | 68.77 | 49.03 | 50.31 |
| Tag (Industry) | 10% | 60.07 | 53.98 | 43.29 |
| Park Rush | 15% | 64.14 | 23.71 | 52.42 |
| Economy First | 31% | 66.60 | 68.92 | 42.32 |

Each strategy played 100 games. The paired design has 25 distinct board seeds; the 100 outcomes are not 100 independent board samples. The earlier 9×9 exploratory reports in `.build` are superseded and must not inform game tuning.

The [earlier 15×15 baseline](balance/2026-10-27-before-ai-scheduling-seed-12345.json) is retained separately: wins were 33/10/14/43 for Balanced/Tag/Park Rush/Economy First, with mean VP 68.81/60.59/65.47/67.96. Bot evaluation timing changed between versions; no card or economy values changed. These are different policy versions, not a controlled rule-tuning experiment.

## Tuning decision

Keep the current rule values. Balanced wins most often in the current matchup; Park Rush leads pre-bonus VP and holds fewer coins. The weak Industry strategy result can reflect bot preferences, card availability and opponents as well as card strength. The change in results between timing versions is another reason not to attribute a win-rate difference directly to card balance. This run alone does not establish which rule value should change.

Before changing income, coin conversion or park awards, compare further seed blocks and human playtests, inspect card acquisition and final-score components, and rerun the same paired baseline after each isolated change. Record any numeric change and its before/after evidence in the GDD. No rule numbers changed for this baseline.

Source SHA-256 fingerprints:

| Source | SHA-256 |
|---|---|
| `code/Cards/CardLibrary.cs` | `FE8A18FD15FA2D2E9873A7F76F9F9C1A0D7F96E8CBF599137040E3C37645569E` |
| `code/Match/MatchSettings.cs` | `F2E1E64477309E5C2E9DB8A94A0C28E20EDDC841DECEA49A2324818D1B167412` |
| `code/Ai/Bot.cs` | `2F4074B807D2420945884B0544D78D8A65F7CE7D4743324779D6180ED503F4D7` |
| `code/Ai/SimulationHarness.cs` | `470740B8C1E4F85417FE36B8A83D7C7ED3747CC49AB566DF5E3AC9FC3ADCD5E9` |

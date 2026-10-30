# Live AI frame profile — 27 October 2026

`CityGame.AiFrameMeasured` is an optional observer around the complete local bot-update group in `OnUpdate`. It measures CPU elapsed time after the round-machine tick and before rendering; it does not measure total frame time, GPU work, networking or observer execution. The scheduler uses a rotating start index and `MatchSettings.AiFrameBudgetMilliseconds` (default 4 ms).

Bots now evaluate at most one auction lot per update, retaining the ordinary candidate budget for that lot. Partial work submits no bid. The best evaluated choice can be submitted near the deadline; stale work is discarded on an auction or hand change. A pending step resumes on a later frame without another thinking-delay draw. Ordinary actions still use their short thinking delay.

The group budget is **soft**: after a bot update returns, the scheduler can defer the remaining bots to the next frame. It cannot interrupt an individual placement or lot calculation. Rotation prevents the first bot from monopolizing later frames. Headless checks cover partial work, deadlines, cancellation, valid budgets and rotation.

## Native observations

Both runs used the actual game loop with three Hard rivals on a 15×15 board through eight rounds. Bid and Build durations were normal. Payout was shortened to 0.5 seconds to keep the profiling run bounded. The human seat readied when eligible and did not build; this is a CPU profile, not a human balance playtest. Observers were removed and the editor returned to its offline menu afterward.

| Run | Seed | Samples | Median | p95 | p99 | Maximum |
|---|---:|---:|---:|---:|---:|---:|
| Original synchronous bidding | 533305 | 3,842 | 0.0161 ms | 0.0541 ms | 6.3101 ms | 41.355 ms, round 1 Bid |
| Incremental bidding + rotating budget | 425185 | 4,016 | 0.0209 ms | 1.8392 ms | 6.2467 ms | 36.7414 ms, round 3 Build |

Raw data: [original](performance/2026-10-27-ai-frame-original.json), [incremental](performance/2026-10-27-ai-frame-incremental.json). The runtime scripts are `.build/m6-ai-frame-profile.cs` and related profiling fixtures.

The runs use different seeds and are not a controlled before/after performance comparison. Idle updates dominate these percentiles. The incremental run still had two samples above 16.7 ms: 29.44 ms in Bid and 36.74 ms in Build. This does not establish a hard 60-FPS budget. Individual evaluation cost, first-use/JIT work, allocations and collection pauses need finer attribution before further optimization. No card or economy values were changed to improve these timings.

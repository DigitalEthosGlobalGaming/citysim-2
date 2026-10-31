# AI ratings and weighted choices — 30 October 2026

Implemented policy `2026-10-30.5-weighted-choices`, recorded as balance version `2026-10-30.5`. The [local placement balance rules](BALANCE_LOCAL_PLACEMENT_2026-10-30.md) remain in effect.

## How choices work

AI rates cards and actions using their effects on its score, income, goals, contract and visible opponents. The best rating is a prediction, not knowledge of future draws or hidden rival cards.

Market purchases, secret-contract selection and building placements now all use the same seeded random policy:

| Difficulty | Chance assigned to the best-rated option(s), when credible alternatives exist |
|---|---:|
| Easy | 55% |
| Normal | 75% |
| Hard | 90% |

The remaining probability is distributed among credible alternatives according to their ratings. Higher-rated alternatives receive more weight. Equally best options share the best-choice budget equally. When every credible option is tied for best, all share 100% equally. A sole clearly superior option is chosen with certainty. These percentages apply to each draw, not the probability of winning a match or finding an objectively optimal whole turn.

For example, the running engine returned these probabilities for ratings 10, 9.5, 9 and 2:

| Rating | Easy | Normal | Hard |
|---:|---:|---:|---:|
| 10 | 55.0% | 75.0% | 90.0% |
| 9.5 | 24.2% | 14.1% | 6.0% |
| 9 | 20.8% | 10.9% | 4.0% |
| 2 | 0% | 0% | 0% |

An alternative must be within `max(minimum gap, abs(best rating) × fraction)` of the best rating. Minimum gaps are 1/0.75/0.5 and fractions are 0.65/0.40/0.25 for Easy/Normal/Hard. Alternative weights are `exp((rating − best) / max(0.25, gap / 2))`. Nonfinite ratings are excluded.

Build decisions first group candidates by the card being played. The best evaluated site rates that card; the AI draws a card, then draws one of that card's rated legal sites. This prevents a card with many shortlisted sites from receiving extra chances compared with another card. Both draws follow the difficulty policy.

Unaffordable/unusable purchases are excluded before selection. Passing is rated at zero and can compete with a marginal purchase. Negative-value purchases are never added to the draw. Placements are filtered against the live board before drawing, including at a deadline, so occupied sites cannot be selected. Existing pacing, planning budgets and human-finished acceleration remain in force.

The decision RNG remains separate from pacing and name generation. Identical seeds and update/action sequences reproduce decisions. New games use fresh match seeds, so their choices vary. All difficulties still follow the same rules and receive the same resources. Easy/Normal retain their shorter planning horizons; Hard retains its two-action lookahead.

## Current balance checks

The evaluator uses actual rule execution on a public-state planning copy. Targeted checks confirm that:

- Nature adjacency adds one placement VP, including beside a rival's Nature card.
- The optional road bonus adds one placement VP; evaluation also accounts for any lost catch-up income.
- Street VP is awarded when the new building is placed, with no recurring or retroactive Street points.
- Forest maturity forecasts a fixed two VP, with no weighted park-size calculation.
- The relaxed road requirements and Lake/park behaviour use the shared live rules.

No card prices, placement points or income values were changed for this randomness pass.

## Verification

The full rule suite passes **723 printed checks**, plus the analytics assertion group. Probability checks sample 20,000 draws per difficulty and confirm the configured best-choice rates, correctly weighted alternatives, tie handling, finite-value filtering and seeded replay. Integration checks verify market variety under 40 decision seeds, card/site fairness at every difficulty and live occupied-site filtering. Existing full-match replay, authority, budgets, pacing, contracts and all-48-card planning checks pass. The game project builds with zero warnings/errors, and the open editor compiled successfully.

Native verification confirms the policy probabilities, the one-time Street wording, Nature park wording and fifteen road requirements. This was read-only and did not alter the user's match.

Ran **96 eight-round matches**, covering one through four AI opponents, four board seeds, mixed difficulties and seat rotations against a Normal/Balanced human proxy. All matches completed with **zero rejected actions and zero budget violations**.

| Difficulty | AI seats observed | Mean final VP | Mean final coins | Win share per observed AI seat |
|---|---:|---:|---:|---:|
| Easy | 68 | 71.26 | 76.69 | 11.8% |
| Normal | 88 | 75.08 | 79.49 | 17.0% |
| Hard | 68 | 80.54 | 100.06 | 45.6% |

The aggregate ratings separate in the expected direction in this sample. Matchup weights differ, seat rotations are correlated, and only four board seeds were used; these are not guaranteed human win rates. In the eight one-opponent games for each difficulty, Easy won two, Normal one and Hard five against the proxy. That small group does not establish separation in every matchup. Late coin surplus still needs economy tuning independently of the choice policy.

Evidence: [benchmark](ai/weighted-choices-2026-10-30/benchmark.json), [rule checks](ai/weighted-choices-2026-10-30/rule-checks.txt), [build](ai/weighted-choices-2026-10-30/build.txt), [native policy](ai/weighted-choices-2026-10-30/native.json), [source fingerprints](ai/weighted-choices-2026-10-30/source-sha256.json).

```powershell
dotnet run --project tests/CitySim.Rules.Tests.csproj
dotnet run --project tests/CitySim.Rules.Tests.csproj -- --ai-benchmark --boards 4 --seed 30102026
```

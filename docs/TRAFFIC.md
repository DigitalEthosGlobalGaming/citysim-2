# Cosmetic traffic

Traffic is a local presentation layer. It reads public board cells and existing match events; it never changes coins, VP, hospital coverage, Link eligibility, gameplay randomness, input or round timing. No vehicle has a collider or network replication.

`TrafficController` maintains background travel in menus, matches and sandbox. Population grows from a few vehicles to a normal maximum of 18, with a temporary four-vehicle payout/Rush Hour bustle. The existing 100-vehicle hard limit remains a safety ceiling. Hospital responses have a reserved visual slot and retry occupied departure points. Link trips and deliveries are staggered, not released as one burst. Route failures affect presentation only.

`TrafficPath` caches distance-sampled road geometry by road revision. In Source coordinates (+X forward, +Y left), right is `(dy,-dx)`; the nominal lane centre is eight units right of the road centre. Corners are tangent cubic curves. Roundabouts circulate counterclockwise around the raised island. Roads use their mesh surface height rather than the ground slab height. Adding streets preserves existing trips; resetting the board invalidates old routes.

Trips to buildings receive their own driveway geometry. Cars emerge from their origin's main mesh, join the road, turn into the destination frontage, and stop slightly inside its facade. Concealment happens inside the building, not in the road. The main mesh bounds also select the barn for farm trips. Replacing a destination retires its old visual trip safely. Through traffic uses road-connected board edges.

Vehicle cruising speeds are approximately 42 units/second for ordinary cars, 34.5 for delivery vehicles, 25.5 for tractors and 51 for ambulances, before each driver's ±18% preference. Acceleration and braking are bounded; corners slow to about 62% of cruise and driveways to 12.5 units/second. Each driver has a fixed lane preference within ±0.6 units and slow drift of at most 0.15 units. Both fade out at driveways. These small differences stay inside the right-hand lane.

Conflicting intersection paths yield; separated opposing lanes can pass together. Visual following gaps and ambulance yielding do not affect rules. Waiting does not despawn cars. Routine traffic adds no camera shake, hit-stop or per-car engine loops. Ambulances have a quiet siren and a restrained beacon that becomes steady with Reduce Flashing.

## Assets

Kenney Car Kit 3.0 (23 January 2026), CC0. Source FBX files, the pack-specific colour atlas and licence are under `Assets/raw/kenney/cars/`. Native ModelDoc assets are under `Assets/models/kenney/cars/` and share `materials/kenney/cars.vmat`. Sources came from `../models/kenney_car-kit/`. All eleven imported models compiled and loaded with positive bounds and `Model.IsError == false`.

Models face +X after FBX import. Visuals normalize their length to 12.5 units for cars or 15 units for larger service vehicles and place their wheel contact at the road surface. Imported files have no authored physics. Sedan, hatchback, SUV, taxi, van, delivery, truck, ambulance and tractor have current dispatch roles; garbage truck and firetruck are available for future service activity.

## Verification

Run `CitySim.TrafficChecks.Run()` through the editor's `run_csharp` tool. The temporary fixture checks all cardinal lane entries/exits, both turn directions, continuity, single-road trips, roundabout clearance, conflicting versus opposing intersection paths, building entry/exit, cached-path isolation, renovation and board reset. It destroys its objects in `finally` and does not alter the active match.

`SuccessfulSpawns`, `UnroutableTrips`, `CachedRoutes` and `TargetPopulation` expose presentation diagnostics. Vehicle speed, cruise speed, distance and lane deviation are inspectable. Full headless rules checks remain `dotnet run --project tests/CitySim.Rules.Tests.csproj`.

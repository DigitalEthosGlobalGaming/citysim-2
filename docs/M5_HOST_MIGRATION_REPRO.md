# M5 local host migration reproduction

Last verified: 27 October 2026, 17:06 Australia/Brisbane. Engine: **26.10.13c**.

M5 remains incomplete. A graceful departure restores the successor, but another
local peer fails while the engine creates its networking game instance. This is
also reproducible in the waiting lobby before any match exists.

## Reproduce in the editor

1. Open `city_sim.sbproj`, play `scenes/minimal.scene`, and choose **Host multiplayer**.
2. Use the editor's local-instance launcher to join two additional game instances.
   Wait for all three players to appear in the lobby. Do not ready up or start a match.
3. On the original host choose **Leave lobby**. Allow the graceful handoff to finish.
4. Inspect both remaining instances. The first fully connected peer becomes host;
   the other takes the local reconnect path and loses its active game scene.

For CLI observation, launch test instances with the editor's additional instance
argument `-mcp`, then restore that preference after spawning. Use `sbox-cli --list`
to find their PIDs and `--connect <pid> --call run_csharp` to query each process.
Do not reuse PIDs from a previous run.

Observation script (works even when the project assembly has unloaded):

```csharp
return new {
    scene = Game.ActiveScene?.Name,
    host = Networking.IsHost,
    active = Networking.IsActive
};
```

Also read each process with `--call read_console '{"minimumLevel":"Error","limit":12}'`.

## Observed result

Before departure, all three players were connected, the lobby was `Waiting`, and
both clients reported `CityGame.Instance.Match == null`.

After departure:

| Observation | Successor | Other peer |
|---|---|---|
| Active scene | Present | `null` |
| Networking active | `true` | `true` |
| Is host | `true` | `false` |
| Lobby | `Waiting`, 3 retained members | No scene to render |
| Match | Absent, as before | No active scene |
| Host vault | Exactly 1 | Not inspected after scene loss |
| Application recovery error | `null` | Application cannot render recovery UI |

The failing peer logged:

```text
System.NullReferenceException: Object reference not set to an instance of an object.
   at Sandbox.GameInstanceDll.CreateGameNetworkingAsync(NetworkSystem system)
   at Sandbox.Network.NetworkSystem.InitializeGameSystemAsync()
```

Earlier active-match reproductions also include
`NetworkSystem.On_Handshake_Welcome` in the stack. The waiting-lobby run had
separate bootstrap HTTP 401 messages shortly before the networking exception;
their causal relationship is unknown. Earlier reproductions reached the same
networking exception after reporting zero server packages and zero file downloads.

## Scope and limits of the evidence

- This reproduces without dealing cards, choosing contracts, bidding, building,
  advancing timers, paying income, or scoring a match.
- Account-to-seat recovery with a new connection GUID passes when a client returns
  to the original host. Three players then completed an eight-round match with
  matching 34/29/21 VP totals and private rival hands omitted.
- The successor restores the private checkpoint and continues correctly in the
  active-match tests. That alone does not prove migration succeeds for every peer.
- A fresh peer joining the promoted host also failed to load the game in the
  earlier active-match test.
- An independent stock `game.minimal` project reproduced the same exception and
  peer scene loss at 17:06. It was created with `sbox-cli --new 'Migration Probe'
  --template game.minimal --in <workspace>/.build`, and retained only the generated
  template assembly source. No City Sim component, checkpoint, vault or rule code
  was included. A runtime script created a lobby with `DestroyWhenHostLeaves=false`,
  the editor launched two joining local instances, then the host called
  `Networking.Disconnect()`. The first joined peer retained its scene as successor;
  the other lost its scene with the same engine stack. The underlying null operand
  is still unknown.
- Public engine source describes a loopback reconnect fallback for local peers
  without a direct channel to the successor. See
  [NetworkSystem.HostMigration.cs](https://github.com/Facepunch/sbox-public/blob/master/engine/Sandbox.Engine/Systems/Networking/System/NetworkSystem.HostMigration.cs).
- No engine binaries or source were changed, and no external issue was submitted.

The earlier private runtime fixtures are `.build/m5-migration-checkpoint.json`
and `.build/m5-migration-v2.json`. They contain complete host state, including
private cards and bids; keep them local when sharing this report.

## Engine metadata comparison in the stock project

The read-only probe used reflection on `Networking.System.IsDeveloperHost` and
`Application.GameIdent` / `Application.GamePackage`. It did not modify engine state.
The probe script is `.build/migration-metadata.cs`; the template project is
`.build/migration_probe/migration_probe.sbproj`.

| Instance/state | Developer host | Game ident | Game package | Scene |
|---|---|---|---|---|
| Original editor host | true | local.migration_probe | Sandbox.LocalPackage | Present |
| Each client before departure | true | local.migration_probe | null | Present |
| Successor after departure | true | local.migration_probe | null | Present |
| Other peer after reconnect | true | null | null | null |

The developer-host flag was retained in this run. A null local package value is
also present on working clients, so neither observation alone establishes the
cause. The losing peer's ident disappears during the full reconnect. Wire package
metadata and the failing method's exact null operand have not been inspected.
Both clients also logged bootstrap HTTP 401 errors; only the reconnecting peer
logged the networking null reference.

All probe games and its temporary editor were stopped after the test. The user's
City Sim and unrelated project editors were left running and were not restarted.

## Retest gate

After a relevant engine fix or a confirmed working transport configuration, repeat
the waiting-lobby test, then the three-player active-match handoff. Every remaining
peer must retain/recover its seat and private view, reach the same board, round,
phase and clock, accept legal commands, and finish with matching explained scores.
Verify that only the elected successor receives the host vault. Do not close M5
until this gate passes. M6–M8 application work subsequently progressed after the
failure was reproduced independently in the untouched engine template. That work
does not close this migration gate or establish that all peers recover.

The test instances were disconnected and stopped. The editor was returned to its
original stopped play state with no unsaved scene changes.

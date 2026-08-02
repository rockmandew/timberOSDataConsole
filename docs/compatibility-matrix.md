# Compatibility Matrix — timberOS Data Console

How the mod behaves as the game, its own schema, and consumers change.

## Verified target

| Component | Version verified against |
| --- | --- |
| Timberborn | 1.0 release, Steam build 23107127 |
| Mod manifest `MinimumGameVersion` | `1.0.0.0` |
| Bindito.Core | as shipped in the above build |
| Newtonsoft.Json | as shipped in `Timberborn_Data/Managed` |
| Telemetry schema | `1.0.0` |

## Assembly dependencies (compile-time references)

| Assembly | Used for | If missing/renamed |
| --- | --- | --- |
| `Bindito.Core` | `Configurator`, `[Context]`, `Bind`/`MultiBind` | mod fails to load — hard dependency |
| `Timberborn.HttpApiSystem` | `IHttpApiEndpoint` | endpoint won't register — hard dependency |
| `Timberborn.SingletonSystem` | `ILoadableSingleton`, `IUpdatableSingleton` | coordinator won't run — hard dependency |
| `Timberborn.GameCycleSystem` | `GameCycleService` | `gameTime` collector → `error`, rest OK |
| `Timberborn.Population` | `PopulationService`, `PopulationData` | `population` collector → `error`, rest OK |
| `Timberborn.InventorySystem` | `DistrictInventoryRegistry`, `Inventory` | `resources` collector → `error`, rest OK |
| `Timberborn.GameDistricts` | `DistrictCenter` | `resources` collector → `error`, rest OK |
| `Timberborn.EntitySystem` | `EntityComponentRegistry` | `resources` collector → `error`, rest OK |
| `Timberborn.GameFactionSystem` | `FactionService` | `game` collector → partial (`factionId` null) |
| `Timberborn.SettlementNameSystem` | `SettlementReferenceService` | settlement name → null (guarded) |
| `Newtonsoft.Json` | serialization | endpoint fails — hard dependency |

The three "hard dependency" rows are the only ones that stop the mod entirely; everything else
degrades to a per-collector `error` status while the snapshot keeps serving.

## Failure-mode behavior

| Situation | Behavior |
| --- | --- |
| No settlement loaded | Coordinator not in `Game` context → no snapshot; `/snapshot` returns `503` |
| One collector throws | That domain is `null`, its `collectors[]` entry is `status:"error"` with the message; others unaffected |
| Game paused | Snapshots keep refreshing (unscaled-time cadence); counters reflect paused state |
| Game version bumped, member renamed | Affected collector → `error`; **update the collector, not the whole mod** |
| Consumer sends old schema expectation | `schemaVersion` is in every envelope; consumers should check it |
| Port 8080 changed in-game | Read `HttpApi.Url`/save `Port`; consumers should make the base URL configurable |

## Schema evolution policy

- **Additive changes** (new nullable fields, new collectors) → **minor** schema bump, backward
  compatible. Consumers ignore unknown fields.
- **Breaking changes** (rename/remove/retype a field) → **major** schema bump. Ship a migration note
  in `contracts` and bump `TelemetryEnvelope.schemaVersion`.
- The C# DTOs (`mod/src/Telemetry/TelemetryDtos.cs`) and the Zod schema
  (`contracts/src/index.ts`) are the two halves that MUST change together. The contract test
  (`contracts/test/contract.test.ts`) guards the fixture against drift.

## Upgrade checklist when a new Timberborn version ships

1. Re-run discovery (`ilspycmd`) on the changed assemblies.
2. Run `dotnet build -c Release` in `mod/` — compile errors pinpoint moved APIs.
3. Load a save, hit `/timberos/v1/health` and `/timberos/v1/snapshot`, check `collectors[]` for any
   `error` entries.
4. Bump `manifest.json` `MinimumGameVersion` only if a hard dependency changed.

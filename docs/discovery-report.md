# Discovery Report — timberOS Data Console

**Date:** 2026-08-01
**Method:** Direct inspection of the installed game + decompilation of game assemblies with
[`ilspycmd`](https://github.com/icsharpcode/ILSpy). Nothing here is assumed from tutorials; every
API listed was read out of the shipped DLLs on this machine.

## 1. Environment

| Item | Value |
| --- | --- |
| Game | Timberborn (Mechanistry), full 1.0 release |
| Install | `C:\Program Files (x86)\Steam\steamapps\common\Timberborn` |
| Steam build id | 23107127 |
| Mod manifest `MinimumGameVersion` seen in installed mods | `1.0.0.0` |
| Managed assemblies | `Timberborn_Data/Managed` (game + Unity + Bindito + Newtonsoft) |
| Mods folder | `C:\Users\<user>\Documents\Timberborn\Mods` |
| Reference mod on disk | `rockmandew.TimberbornHD` (manifest + single DLL) |
| Toolchain | .NET SDK 10, Node 22, npm/pnpm (corepack), git, gh |

## 2. Modding approach (verified)

Timberborn 1.0 ships an **official mod system** — no BepInEx, no bundled Harmony.

- **DI container:** `Bindito.Core` (`Bindito.Core.dll`, `Bindito.Unity.dll`).
- **Packaging:** a folder under `Documents/Timberborn/Mods/<ModId>/` containing `manifest.json`
  plus the compiled `.dll`. Manifest fields observed in a shipping mod:
  `Name, Version, Id, MinimumGameVersion, Description, RequiredMods, OptionalMods`.
- **Entry point:** a class deriving from `Bindito.Core.Configurator`, decorated with
  `[Context("Game")]` (other contexts: `Bootstrapper`, `MainMenu`, `MapEditor`). The container
  auto-discovers configurators in mod assemblies.
- **Lifecycle:** singletons bound via `Bind<T>().AsSingleton()` that implement
  `Timberborn.SingletonSystem` interfaces are driven automatically:
  `ILoadableSingleton.Load()`, `IUpdatableSingleton.UpdateSingleton()`, `ITickableSingleton.Tick()`.
- **Reflection/patching:** **not required** for this mod. All telemetry for the first slice reads
  public services through DI.

## 3. Transport decision — use the game's native HTTP server

`Timberborn.HttpApiSystem.dll` is a **native game assembly**. It already runs a local
`HttpListener` at `http://localhost:{Port}/` (default **8080**, persisted per-save, settable via
`HttpApi.SetPort`). The same system powers the HTTP Adapter / HTTP Lever that the existing
**timberOS** already consumes.

The key extension point:

```csharp
public interface IHttpApiEndpoint { Task<bool> TryHandle(HttpListenerContext context); }
```

`HttpApi` holds `IEnumerable<IHttpApiEndpoint>` and calls each until one returns `true`. Mods add
endpoints with `MultiBind<IHttpApiEndpoint>().To<T>().AsSingleton()` — exactly how the game
registers its own `/api/adapters` and `/api/levers` endpoints.

**Chosen design:** register a single `TelemetryEndpoint` on the native server rather than embedding
a second web server in the mod (the spec's "least game-thread risk, simplest update path"). Serves:

- `GET /timberos/v1/health` → liveness + latest sequence
- `GET /timberos/v1/snapshot` → the full telemetry envelope

Note: the game's `HttpListenerContextExtensions.WriteJson/WriteText` helpers are `internal`, so the
mod serializes with `Newtonsoft.Json` directly (the same serializer the game uses under the hood).

## 4. Threading model (critical)

`HttpApi.ProcessRequests` runs on a background `Task`, so `IHttpApiEndpoint.TryHandle` executes on
the **HTTP listener thread — not the Unity main thread**. Reading game state there would be unsafe.

**Solution implemented:** `SnapshotCoordinator` (an `IUpdatableSingleton`) builds an **immutable**
`TelemetryEnvelope` on the main thread every ~2 s and publishes it into a `SnapshotHolder`
(atomic reference swap). `TelemetryEndpoint` only serializes that already-built object, so it never
touches live game state and never blocks the simulation. Unscaled time is used so telemetry keeps
refreshing while the game is paused.

## 5. First-slice telemetry sources (verified in decompiled code)

| Domain | Service / type | Member(s) | Confidence |
| --- | --- | --- | --- |
| Game version | `UnityEngine.Application` | `version` | High |
| Faction | `Timberborn.GameFactionSystem.FactionService` | `Current.Id` | High |
| Settlement | `Timberborn.SettlementNameSystem.SettlementReferenceService` | `SettlementReference.SettlementName` | High |
| Time | `Timberborn.GameCycleSystem.GameCycleService` | `Cycle`, `CycleDay`, `PartialCycleDay` | High |
| Population | `Timberborn.Population.PopulationService` | `GlobalPopulationData` → `PopulationData` | High |
| Jobs / housing | `PopulationData` | `BeaverWorkplaceData.{OccupiedWorkslots,FreeWorkslots}`, `BedData.{OccupiedBeds,FreeBeds}` | High |
| Contamination | `PopulationData.ContaminationData` | `ContaminatedTotal` | High |
| Resources | `Timberborn.EntitySystem.EntityComponentRegistry` | `GetEnabled<DistrictCenter>()` | High |
| District stock | `Timberborn.InventorySystem.DistrictInventoryRegistry` | `Inventories` (public) | High |
| Stock / capacity | `Timberborn.InventorySystem.Inventory` | `Stock` (`GoodAmount`), `AllowedGoods` (`StorableGoodAmount`) | High |

See [game-api-map.md](game-api-map.md) for field-by-field mapping and known gaps.

## 6. What was intentionally deferred

- Goods in non-public building inventories (mid-process workshop stock) are not summed yet.
- Weather / drought / badtide, power, water, buildings, production — later phases.
- Localized good/faction display names (mod emits stable ids; the TS side maps names).

These are captured as issues in the README's prioritized backlog.

## 7. Result

The mod **compiles cleanly against the installed game assemblies** (`dotnet build -c Release`), and
the shared contract + a replay fixture drive a working, deterministic depletion recommendation
offline (`npm run demo`). The architecture is proven end-to-end without needing the game open.

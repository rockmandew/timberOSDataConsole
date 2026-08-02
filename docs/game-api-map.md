# Game API Map — timberOS Data Console

Each desired telemetry field, its verified in-game source, the extraction technique, confidence, and
compatibility risk. "Verified" = read directly from the decompiled 1.0 assemblies on this machine.

## Envelope header

| Field | Source | Technique | Confidence | Risk |
| --- | --- | --- | --- | --- |
| `gameTime.cycle` | `GameCycleService.Cycle` | DI, property read | High | Low |
| `gameTime.cycleDay` | `GameCycleService.CycleDay` | DI, property read | High | Low |
| `gameTime.partialCycleDay` | `GameCycleService.PartialCycleDay` | DI, property read | High | Low |
| `settlementId` | `SettlementReferenceService.SettlementReference.SettlementName` | DI, property read | High | Med (uses save name; not a stable GUID) |
| `capturedAt` | `System.DateTime.UtcNow` | wall clock | High | Low |
| `sequence` | coordinator counter | in-process | High | Low |

## `payload.game`

| Field | Source | Confidence | Risk |
| --- | --- | --- | --- |
| `gameVersion` | `UnityEngine.Application.version` | High | Low |
| `modVersion` | `GameStateCollector.ModVersion` const | High | Low |
| `factionId` | `FactionService.Current.Id` (nullable pre-load) | High | Low |
| `settlementName` | `SettlementReferenceService.SettlementReference.SettlementName` | High | Med |

## `payload.population`

Source: `PopulationService.GlobalPopulationData` (`PopulationData`).

| Field | Source member | Confidence |
| --- | --- | --- |
| `total` | `TotalPopulation` | High |
| `beavers` | `NumberOfBeavers` | High |
| `adults` | `NumberOfAdults` | High |
| `children` | `NumberOfChildren` | High |
| `bots` | `NumberOfBots` | High |
| `employed` | `BeaverWorkplaceData.OccupiedWorkslots` | High |
| `openJobs` | `BeaverWorkplaceData.FreeWorkslots` | High |
| `beds` | `BedData.OccupiedBeds + BedData.FreeBeds` | High |
| `contaminatedBeavers` | `ContaminationData.ContaminatedTotal` | High |

Not yet emitted (available for later): `WorkforceData.{Employable,Unemployable}`, `BedData.Homeless`,
`WorkplaceData.Unemployed`, bot workforce/workplace, per-district breakdown.

## `payload.resources[]`

Global stock aggregated across public district inventories.

| Step | Source | Notes |
| --- | --- | --- |
| Enumerate districts | `EntityComponentRegistry.GetEnabled<DistrictCenter>()` | `DistrictCenter : IRegisteredComponent` |
| Get inventories | `DistrictCenter.TryGetComponent<DistrictInventoryRegistry>()` → `.Inventories` | public inventories only |
| `amount` per good | `Inventory.Stock` → `GoodAmount { GoodId, Amount }` | accurate |
| `capacity` per good | `Inventory.AllowedGoods` → `StorableGoodAmount { StorableGood.GoodId, Amount }` | per-good capacity allocation |

**Known gap:** goods held in non-public building inventories (e.g. a workshop's in-progress inputs
and outputs) are not counted. This under-counts totals slightly during heavy production. Tracked as
issue **R-1**.

## Extraction techniques legend

- **DI, property read** — service injected via Bindito constructor, value read on the main thread.
- **Enumerate** — iterate a registry on the main thread inside the coordinator's collector.
- All reads happen in `SnapshotCoordinator.UpdateSingleton()` (main thread). The HTTP thread only
  serializes the finished immutable snapshot.

## Compatibility risk notes

- Field names above are internal game symbols and **may change between game versions**. Every
  collector is isolated and wrapped in try/catch: if a member disappears, that collector reports
  `status: "error"` and the rest of the snapshot still serves. See
  [compatibility-matrix.md](compatibility-matrix.md).

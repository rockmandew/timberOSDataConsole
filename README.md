# timberOS Data Console

The **data endpoint that powers [timberOS](https://github.com/rockmandew/timberOS)**.

timberOS on its own is "headless": it reads only the boolean HTTP Adapter / Lever signals the game
exposes and *simulates* everything else. **timberOS Data Console** is a native Timberborn mod that
reads the *real* colony state — population, jobs, housing, global resource stock — and serves it as
JSON on the game's own local HTTP server, so timberOS can run on facts instead of assumptions.

```
Timberborn (1.0)
  └─ timberOS Data Console mod  ── reads game services on the main thread
        └─ native HTTP API :8080 ── GET /timberos/v1/snapshot  (JSON telemetry)
              └─ @timberos/data-console-contracts ── shared Zod schema + TS types
                    └─ timberOS gateway + dashboard ── real data, deterministic advisor
```

This repo is the **first vertical slice** described in the timberOS build request: mod → native HTTP
telemetry → validated contract → a deterministic, explainable resource-depletion recommendation,
proven end-to-end.

## Repository layout

| Path | What |
| --- | --- |
| `mod/` | The C# Timberborn mod (`TimberOS.DataConsole`). Collectors, snapshot coordinator, HTTP endpoint. |
| `contracts/` | `@timberos/data-console-contracts` — Zod schema + TS types (single source of truth), fixtures, tests, and an offline demo. |
| `docs/` | Discovery report, game API map, compatibility matrix. |
| `scripts/` | `package-mod.ps1` — build + install the mod locally. |

## Telemetry API

Served by the mod on the game's native HTTP server (default `http://localhost:8080`).

| Endpoint | Returns |
| --- | --- |
| `GET /timberos/v1/health` | `{ ok, schemaVersion, hasSnapshot, sequence }` |
| `GET /timberos/v1/snapshot` | Full `TelemetryEnvelope` (`503` until the first collection after a settlement loads) |

The envelope carries a game-time header plus `payload.game`, `payload.population`,
`payload.resources[]`, `payload.weather`, `payload.power`, and per-collector
`payload.collectors[]` health. Any value the mod could not
read is `null` (never `0`) so consumers can distinguish "unknown" from "empty". Full shape:
[`contracts/src/index.ts`](contracts/src/index.ts).

## Quick start

### 1. Build & install the mod (Windows)

```bash
pwsh scripts/package-mod.ps1
```

This compiles against your installed game assemblies and copies the DLL + `manifest.json` into
`Documents/Timberborn/Mods/rockmandew.TimberOSDataConsole/`. Launch Timberborn, load a settlement,
then:

```bash
curl http://localhost:8080/timberos/v1/snapshot
```

> The mod references the game DLLs from the default Steam path. If your install differs, set
> `TIMBERBORN_MANAGED` to your `Timberborn_Data/Managed` folder before building.

### 2. Run the contract tests + offline demo (no game needed)

```bash
cd contracts
npm install
npm test        # validates the reference fixtures against the schema
npm run demo    # replays a fixture and prints a real depletion recommendation
```

`npm run demo` output (from the bundled fixture):

```
[RECOMMENDATION · resource-depletion · confidence 80%]
Log reserves are declining. The colony has 118 logs and is losing about 22 per game day.
At the current rate, the 80-log reserve will be reached in about 1.7 days.
```

Point the demo at the live game with `TIMBEROS_LIVE=1 npm run demo`.

## How timberOS consumes this

timberOS imports `@timberos/data-console-contracts`, fetches `/timberos/v1/snapshot`, validates it
with `parseTelemetryEnvelope`, and feeds the normalized data into its rules/forecasting engine —
replacing the simulated values. Wiring timberOS's gateway to this endpoint is the next step (see
backlog **I-1**).

## Prioritized backlog

| ID | Item | Why |
| --- | --- | --- |
| **I-1** | Wire the timberOS gateway to `/timberos/v1/snapshot` (replace simulator) | Delivers the real-data payoff in the dashboard |
| **R-1** | Count goods in non-public building inventories | `resources[]` slightly under-counts during heavy production |
| **G-1** | Emit a stable settlement GUID, not just the save name | Reliable multi-settlement history keying |
| **P-1** | Add per-district population + bot workforce breakdown | District-level advisor rules |
| **PW-1** | Give power networks a stable id (currently positional `index`) | Reliable per-network history/alerts |
| **B-1** | Buildings + production + water collectors | Broaden telemetry to the full spec |
| **N-1** | Resolve localized good/faction display names | Nicer labels (currently ids; mapped on the TS side) |
| **A-1** | Optional auth token + configurable bind/port for non-loopback use | Security when exposed beyond localhost |

**Implemented so far (schema 1.1.0):** game-time header, `game`, `population` (incl. jobs/housing/
contamination), `resources` (global stock + capacity), `weather` (drought/badtide cycle + days
remaining), `power` (per-network supply/demand/battery + totals), and per-collector health.

## Design notes

- **Native transport, not a second web server.** The mod registers an `IHttpApiEndpoint` on the
  game's existing `HttpApiSystem` listener — lowest game-thread risk, no extra ports.
- **Never reads game state off-thread.** A main-thread coordinator builds an immutable snapshot
  every ~2 s; the HTTP thread only serializes the finished object.
- **Fails soft.** Each collector is isolated; one failure degrades a single domain, not the snapshot.
- **No AI, no cloud, local-first.** Everything runs on `localhost`.

See [`docs/discovery-report.md`](docs/discovery-report.md) for how every API was verified.

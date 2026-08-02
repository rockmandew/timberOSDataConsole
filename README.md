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

## Setup & run — start here (Windows, zero experience needed)

Follow these in order. Each command is meant to be **copy-pasted exactly**. You do **not** need
timberOS for any of this — the Data Console works on its own. (When you also run timberOS, it will
automatically pick up this data; see [Running it with timberOS](#running-it-with-timberos).)

### Step 1 — Install the free tools you need (one time)

Install these three, clicking "Next/Install" through each installer, then **restart your computer**
so Windows picks them up:

1. **.NET SDK 8** (builds the mod) — https://dotnet.microsoft.com/download → big "Download .NET SDK" button.
2. **Git** (downloads the code) — https://git-scm.com/download/win → accept all defaults.
3. **Node.js LTS** (only needed for the optional offline demo in Step 6) — https://nodejs.org → "LTS" button.

You also need **Timberborn** installed via Steam (this mod targets the 1.0 release).

### Step 2 — Download this project

Open **PowerShell** (press Start, type `PowerShell`, hit Enter) and run:

```powershell
cd $HOME\Downloads
git clone https://github.com/rockmandew/timberOSDataConsole.git
cd timberOSDataConsole
```

### Step 3 — Build and install the mod (one command)

```powershell
pwsh scripts/package-mod.ps1
```

> On **Windows PowerShell** (the blue icon, no separate install), use `powershell` instead of `pwsh`:
> ```powershell
> powershell -ExecutionPolicy Bypass -File scripts/package-mod.ps1
> ```

This compiles the mod against your installed game and copies it into
`Documents\Timberborn\Mods\rockmandew.TimberOSDataConsole\`. When it finishes it prints
**"Installed to …"**. That's success.

The script **finds Timberborn automatically** — it reads your Steam library list, so it works no
matter which drive the game is on (`C:`, `D:`, etc.). If it still can't find it, it will **ask you
for the path**. You can also tell it up front:

```powershell
pwsh scripts/package-mod.ps1 -TimberbornManaged "D:\SteamLibrary\steamapps\common\Timberborn"
```

### Step 4 — Turn the mod on in the game

1. Launch **Timberborn**.
2. On the main menu choose **Mods**, make sure **timberOS Data Console** is enabled, and if it asks,
   let it restart the game.
3. **Start or load a settlement** (data only exists once you're actually in a colony).

### Step 5 — See your live colony data (the snapshot)

While the game is running with a settlement loaded, open your web browser and go to:

```
http://localhost:8080/timberos/v1/snapshot
```

You should see a page of JSON — your real population, resources, weather, and power. That's the
Data Console working. (`http://localhost:8080/timberos/v1/health` gives a quick "is it alive" check.)

> Seeing `503 / "No snapshot collected yet"`? You're not in a settlement yet — load a colony and
> refresh. Seeing nothing / can't connect? The mod isn't enabled — recheck Step 4.

### Step 6 — (Optional) Try it without the game

To see the data-to-recommendation pipeline without launching Timberborn:

```powershell
cd contracts
npm install
npm test        # validates the sample data against the schema
npm run demo    # prints a real, deterministic depletion recommendation from sample data
```

Expected `npm run demo` output:

```
[RECOMMENDATION · resource-depletion · confidence 80%]
Log reserves are declining. The colony has 118 logs and is losing about 22 per game day.
At the current rate, the 80-log reserve will be reached in about 1.7 days.
```

(Run `$env:TIMBEROS_LIVE = "1"; npm run demo` to point the demo at the live game instead.)

## Running it with timberOS

The two projects are **independent but better together**:

- **This mod alone** gives you the live JSON snapshot at `localhost:8080` (Steps 1–5 above) — useful
  on its own for dashboards, scripts, or the offline advisor demo.
- **[timberOS](https://github.com/rockmandew/timberOS)** is the dashboard/command console. When its
  gateway is running, it automatically polls this mod's `/timberos/v1/snapshot` and shows your real
  colony data. If this mod isn't installed, timberOS still runs (it just won't show colony data).

To use both: finish Steps 1–5 here, then follow the **Setup & run** section in the timberOS README.
No extra configuration is required — timberOS looks for this endpoint automatically.

Under the hood, a consumer imports `@timberos/data-console-contracts`, fetches
`/timberos/v1/snapshot`, and validates it with `parseTelemetryEnvelope` before use.

## Prioritized backlog

| ID | Item | Why |
| --- | --- | --- |
| ~~**I-1**~~ | ~~Wire the timberOS gateway to `/timberos/v1/snapshot`~~ ✅ done — the gateway now polls this endpoint and serves it at `/api/colony` | Delivers the real-data payoff in the dashboard |
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

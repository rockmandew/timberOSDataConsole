using System;
using System.Collections.Generic;
using Timberborn.GameCycleSystem;
using Timberborn.HttpApiSystem;
using Timberborn.SingletonSystem;
using TimberOS.DataConsole.Telemetry.Collectors;
using UnityEngine;

namespace TimberOS.DataConsole.Telemetry
{
    /// <summary>
    /// Drives telemetry collection on the Unity main thread at a fixed cadence and
    /// publishes immutable snapshots to <see cref="SnapshotHolder"/>. Each collector
    /// is wrapped in try/catch so one failure degrades a single domain rather than the
    /// whole snapshot (its status is reported as "error"). Uses unscaled time so
    /// snapshots keep refreshing while the game is paused.
    /// </summary>
    public sealed class SnapshotCoordinator : ILoadableSingleton, IUpdatableSingleton
    {
        private const string SchemaVersion = "1.2.0";
        private const float IntervalSeconds = 2f;

        private readonly SnapshotHolder _holder;
        private readonly GameStateCollector _gameStateCollector;
        private readonly PopulationCollector _populationCollector;
        private readonly ResourceCollector _resourceCollector;
        private readonly WeatherCollector _weatherCollector;
        private readonly PowerCollector _powerCollector;
        private readonly ProductionCollector _productionCollector;
        private readonly WaterCollector _waterCollector;
        private readonly GameCycleService _gameCycleService;
        private readonly HttpApi _httpApi;

        private float _nextDueTime;
        private long _sequence;

        public SnapshotCoordinator(
            SnapshotHolder holder,
            GameStateCollector gameStateCollector,
            PopulationCollector populationCollector,
            ResourceCollector resourceCollector,
            WeatherCollector weatherCollector,
            PowerCollector powerCollector,
            ProductionCollector productionCollector,
            WaterCollector waterCollector,
            GameCycleService gameCycleService,
            HttpApi httpApi)
        {
            _holder = holder;
            _gameStateCollector = gameStateCollector;
            _populationCollector = populationCollector;
            _resourceCollector = resourceCollector;
            _weatherCollector = weatherCollector;
            _powerCollector = powerCollector;
            _productionCollector = productionCollector;
            _waterCollector = waterCollector;
            _gameCycleService = gameCycleService;
            _httpApi = httpApi;
        }

        public void Load()
        {
            _nextDueTime = 0f;
            EnsureHttpServerRunning();
            Debug.Log("[timberOS DataConsole] Snapshot coordinator loaded; serving telemetry at /timberos/v1/snapshot");
        }

        /// <summary>
        /// The game's native HTTP server is off until a player opens an HTTP Adapter/Lever
        /// building and clicks "Start API". Since this mod exists to serve telemetry, start
        /// the server ourselves when a settlement loads so the endpoint is reachable without
        /// requiring any in-game building. The server binds to http://localhost:{port}/ only
        /// (loopback), and Start() is a no-op if it's already running (e.g. the player, or a
        /// reload). Failures are non-fatal: telemetry snapshots are still built either way.
        /// </summary>
        private void EnsureHttpServerRunning()
        {
            try
            {
                if (_httpApi.IsRunning)
                {
                    Debug.Log($"[timberOS DataConsole] HTTP server already running at {_httpApi.Url}");
                    return;
                }

                _httpApi.Start();

                if (_httpApi.IsRunning)
                {
                    Debug.Log($"[timberOS DataConsole] Started game HTTP server at {_httpApi.Url}");
                }
                else
                {
                    Debug.LogWarning(
                        "[timberOS DataConsole] Could not start the game HTTP server" +
                        (string.IsNullOrEmpty(_httpApi.ErrorMessage) ? "." : $": {_httpApi.ErrorMessage}") +
                        $" (port {_httpApi.Port}). The port may be in use; free it or change the port on an " +
                        "HTTP Adapter building, then reload.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[timberOS DataConsole] Failed to auto-start the game HTTP server: {e.Message}");
            }
        }

        public void UpdateSingleton()
        {
            if (Time.unscaledTime < _nextDueTime)
            {
                return;
            }

            _nextDueTime = Time.unscaledTime + IntervalSeconds;
            _holder.Publish(BuildSnapshot());
        }

        private TelemetryEnvelope BuildSnapshot()
        {
            var statuses = new List<CollectorStatusDto>();

            GameStateDto? game = Run(_gameStateCollector, statuses);
            PopulationDto? population = Run(_populationCollector, statuses);
            IReadOnlyList<ResourceDto>? resources = Run(_resourceCollector, statuses);
            WeatherDto? weather = Run(_weatherCollector, statuses);
            PowerDto? power = Run(_powerCollector, statuses);
            ProductionDto? production = Run(_productionCollector, statuses);
            WaterDto? water = Run(_waterCollector, statuses);
            GameTimeDto? gameTime = ReadGameTime(statuses);

            var payload = new SnapshotPayload(game, population, resources, weather, power, production, water, statuses);

            return new TelemetryEnvelope(
                schemaVersion: SchemaVersion,
                messageType: "snapshot",
                source: "timberborn-mod",
                settlementId: game?.SettlementName,
                sequence: _sequence++,
                capturedAt: DateTime.UtcNow.ToString("o"),
                gameTime: gameTime,
                payload: payload);
        }

        private static TResult? Run<TResult>(ITelemetryCollector<TResult> collector, List<CollectorStatusDto> statuses)
            where TResult : class
        {
            if (!collector.IsAvailable)
            {
                statuses.Add(new CollectorStatusDto(collector.Name, "unavailable", null));
                return null;
            }

            try
            {
                TResult result = collector.Collect();
                statuses.Add(new CollectorStatusDto(collector.Name, "available", null));
                return result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[timberOS DataConsole] Collector '{collector.Name}' failed: {e.Message}");
                statuses.Add(new CollectorStatusDto(collector.Name, "error", e.Message));
                return null;
            }
        }

        private GameTimeDto? ReadGameTime(List<CollectorStatusDto> statuses)
        {
            try
            {
                var dto = new GameTimeDto(
                    cycle: _gameCycleService.Cycle,
                    cycleDay: _gameCycleService.CycleDay,
                    partialCycleDay: _gameCycleService.PartialCycleDay);
                statuses.Add(new CollectorStatusDto("gameTime", "available", null));
                return dto;
            }
            catch (Exception e)
            {
                statuses.Add(new CollectorStatusDto("gameTime", "error", e.Message));
                return null;
            }
        }
    }
}

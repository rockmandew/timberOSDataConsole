using System;
using System.Collections.Generic;
using Timberborn.GameCycleSystem;
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
        private const string SchemaVersion = "1.0.0";
        private const float IntervalSeconds = 2f;

        private readonly SnapshotHolder _holder;
        private readonly GameStateCollector _gameStateCollector;
        private readonly PopulationCollector _populationCollector;
        private readonly ResourceCollector _resourceCollector;
        private readonly GameCycleService _gameCycleService;

        private float _nextDueTime;
        private long _sequence;

        public SnapshotCoordinator(
            SnapshotHolder holder,
            GameStateCollector gameStateCollector,
            PopulationCollector populationCollector,
            ResourceCollector resourceCollector,
            GameCycleService gameCycleService)
        {
            _holder = holder;
            _gameStateCollector = gameStateCollector;
            _populationCollector = populationCollector;
            _resourceCollector = resourceCollector;
            _gameCycleService = gameCycleService;
        }

        public void Load()
        {
            _nextDueTime = 0f;
            Debug.Log("[timberOS DataConsole] Snapshot coordinator loaded; serving telemetry at /timberos/v1/snapshot");
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
            GameTimeDto? gameTime = ReadGameTime(statuses);

            var payload = new SnapshotPayload(game, population, resources, statuses);

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

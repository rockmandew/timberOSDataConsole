using Timberborn.GameFactionSystem;
using Timberborn.SettlementNameSystem;
using UnityEngine;

namespace TimberOS.DataConsole.Telemetry.Collectors
{
    /// <summary>
    /// Reads the settlement header: game version, faction and settlement name.
    /// Verified sources (Timberborn 1.0):
    ///   - UnityEngine.Application.version           → game version string
    ///   - FactionService.Current.Id                 → active faction id
    ///   - SettlementReferenceService.SettlementName → save/settlement name
    /// </summary>
    public sealed class GameStateCollector : ITelemetryCollector<GameStateDto>
    {
        public const string ModVersion = "0.2.0";

        private readonly FactionService _factionService;
        private readonly SettlementReferenceService _settlementReferenceService;

        public GameStateCollector(FactionService factionService,
            SettlementReferenceService settlementReferenceService)
        {
            _factionService = factionService;
            _settlementReferenceService = settlementReferenceService;
        }

        public string Name => "game";

        public bool IsAvailable => true;

        public GameStateDto Collect()
        {
            string? factionId = _factionService.Current?.Id;
            string? settlementName = TryGetSettlementName();
            return new GameStateDto(
                gameVersion: Application.version,
                modVersion: ModVersion,
                factionId: factionId,
                settlementName: settlementName);
        }

        private string? TryGetSettlementName()
        {
            try
            {
                string? name = _settlementReferenceService.SettlementReference.SettlementName;
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            catch
            {
                return null;
            }
        }
    }
}

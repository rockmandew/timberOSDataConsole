using System.Collections.Generic;
using Newtonsoft.Json;

namespace TimberOS.DataConsole.Telemetry
{
    // Immutable data-transfer objects that define the exact JSON wire shape emitted by
    // the mod. Property names are pinned with [JsonProperty] so the contract does not
    // drift when C# members are renamed. Keep these in sync with contracts/schema.
    //
    // Everything a collector could not read is left null (not zero) so consumers can
    // distinguish "unknown" from "genuinely empty", per the timberOS spec.

    /// <summary>Top-level envelope. Mirrors contracts TelemetryEnvelope.</summary>
    public sealed class TelemetryEnvelope
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion { get; }
        [JsonProperty("messageType")] public string MessageType { get; }
        [JsonProperty("source")] public string Source { get; }
        [JsonProperty("settlementId")] public string? SettlementId { get; }
        [JsonProperty("sequence")] public long Sequence { get; }
        [JsonProperty("capturedAt")] public string CapturedAt { get; }
        [JsonProperty("gameTime")] public GameTimeDto? GameTime { get; }
        [JsonProperty("payload")] public SnapshotPayload Payload { get; }

        public TelemetryEnvelope(string schemaVersion, string messageType, string source,
            string? settlementId, long sequence, string capturedAt, GameTimeDto? gameTime,
            SnapshotPayload payload)
        {
            SchemaVersion = schemaVersion;
            MessageType = messageType;
            Source = source;
            SettlementId = settlementId;
            Sequence = sequence;
            CapturedAt = capturedAt;
            GameTime = gameTime;
            Payload = payload;
        }
    }

    public sealed class GameTimeDto
    {
        [JsonProperty("cycle")] public int Cycle { get; }
        [JsonProperty("cycleDay")] public int CycleDay { get; }
        [JsonProperty("partialCycleDay")] public float PartialCycleDay { get; }

        public GameTimeDto(int cycle, int cycleDay, float partialCycleDay)
        {
            Cycle = cycle;
            CycleDay = cycleDay;
            PartialCycleDay = partialCycleDay;
        }
    }

    public sealed class SnapshotPayload
    {
        [JsonProperty("game")] public GameStateDto? Game { get; }
        [JsonProperty("population")] public PopulationDto? Population { get; }
        [JsonProperty("resources")] public IReadOnlyList<ResourceDto>? Resources { get; }
        [JsonProperty("weather")] public WeatherDto? Weather { get; }
        [JsonProperty("power")] public PowerDto? Power { get; }
        [JsonProperty("production")] public ProductionDto? Production { get; }
        [JsonProperty("water")] public WaterDto? Water { get; }
        [JsonProperty("collectors")] public IReadOnlyList<CollectorStatusDto> Collectors { get; }

        public SnapshotPayload(GameStateDto? game, PopulationDto? population,
            IReadOnlyList<ResourceDto>? resources, WeatherDto? weather, PowerDto? power,
            ProductionDto? production, WaterDto? water,
            IReadOnlyList<CollectorStatusDto> collectors)
        {
            Game = game;
            Population = population;
            Resources = resources;
            Weather = weather;
            Power = power;
            Production = production;
            Water = water;
            Collectors = collectors;
        }
    }

    /// <summary>
    /// Weather / hazard state. Timberborn cycles run temperate days then a hazardous
    /// stretch (drought or badtide). <c>hazardId</c> is the hazard type selected for the
    /// current cycle (upcoming while temperate, active once hazardous).
    /// </summary>
    public sealed class WeatherDto
    {
        [JsonProperty("isHazardous")] public bool IsHazardous { get; }
        [JsonProperty("hazardId")] public string? HazardId { get; }
        [JsonProperty("temperateDurationDays")] public int TemperateDurationDays { get; }
        [JsonProperty("hazardDurationDays")] public int HazardDurationDays { get; }
        [JsonProperty("daysUntilHazard")] public int? DaysUntilHazard { get; }
        [JsonProperty("hazardDaysRemaining")] public int? HazardDaysRemaining { get; }

        public WeatherDto(bool isHazardous, string? hazardId, int temperateDurationDays,
            int hazardDurationDays, int? daysUntilHazard, int? hazardDaysRemaining)
        {
            IsHazardous = isHazardous;
            HazardId = hazardId;
            TemperateDurationDays = temperateDurationDays;
            HazardDurationDays = hazardDurationDays;
            DaysUntilHazard = daysUntilHazard;
            HazardDaysRemaining = hazardDaysRemaining;
        }
    }

    /// <summary>One mechanical (power) network plus rolled-up totals in <see cref="PowerDto"/>.</summary>
    public sealed class PowerNetworkDto
    {
        [JsonProperty("index")] public int Index { get; }
        [JsonProperty("supply")] public int Supply { get; }
        [JsonProperty("demand")] public int Demand { get; }
        [JsonProperty("surplus")] public int Surplus { get; }
        [JsonProperty("batteryCharge")] public int BatteryCharge { get; }
        [JsonProperty("batteryCapacity")] public int BatteryCapacity { get; }
        [JsonProperty("generators")] public int Generators { get; }
        [JsonProperty("powered")] public bool Powered { get; }

        public PowerNetworkDto(int index, int supply, int demand, int surplus, int batteryCharge,
            int batteryCapacity, int generators, bool powered)
        {
            Index = index;
            Supply = supply;
            Demand = demand;
            Surplus = surplus;
            BatteryCharge = batteryCharge;
            BatteryCapacity = batteryCapacity;
            Generators = generators;
            Powered = powered;
        }
    }

    public sealed class PowerDto
    {
        [JsonProperty("networkCount")] public int NetworkCount { get; }
        [JsonProperty("totalSupply")] public int TotalSupply { get; }
        [JsonProperty("totalDemand")] public int TotalDemand { get; }
        [JsonProperty("totalSurplus")] public int TotalSurplus { get; }
        [JsonProperty("totalBatteryCharge")] public int TotalBatteryCharge { get; }
        [JsonProperty("totalBatteryCapacity")] public int TotalBatteryCapacity { get; }
        [JsonProperty("networksInDeficit")] public int NetworksInDeficit { get; }
        [JsonProperty("networks")] public IReadOnlyList<PowerNetworkDto> Networks { get; }

        public PowerDto(int networkCount, int totalSupply, int totalDemand, int totalSurplus,
            int totalBatteryCharge, int totalBatteryCapacity, int networksInDeficit,
            IReadOnlyList<PowerNetworkDto> networks)
        {
            NetworkCount = networkCount;
            TotalSupply = totalSupply;
            TotalDemand = totalDemand;
            TotalSurplus = totalSurplus;
            TotalBatteryCharge = totalBatteryCharge;
            TotalBatteryCapacity = totalBatteryCapacity;
            NetworksInDeficit = networksInDeficit;
            Networks = networks;
        }
    }

    public sealed class GameStateDto
    {
        [JsonProperty("gameVersion")] public string? GameVersion { get; }
        [JsonProperty("modVersion")] public string ModVersion { get; }
        [JsonProperty("factionId")] public string? FactionId { get; }
        [JsonProperty("settlementName")] public string? SettlementName { get; }

        public GameStateDto(string? gameVersion, string modVersion, string? factionId, string? settlementName)
        {
            GameVersion = gameVersion;
            ModVersion = modVersion;
            FactionId = factionId;
            SettlementName = settlementName;
        }
    }

    public sealed class PopulationDto
    {
        [JsonProperty("total")] public int Total { get; }
        [JsonProperty("beavers")] public int Beavers { get; }
        [JsonProperty("adults")] public int Adults { get; }
        [JsonProperty("children")] public int Children { get; }
        [JsonProperty("bots")] public int Bots { get; }
        [JsonProperty("employed")] public int? Employed { get; }
        [JsonProperty("openJobs")] public int? OpenJobs { get; }
        [JsonProperty("beds")] public int? Beds { get; }
        [JsonProperty("contaminatedBeavers")] public int? ContaminatedBeavers { get; }

        public PopulationDto(int total, int beavers, int adults, int children, int bots,
            int? employed, int? openJobs, int? beds, int? contaminatedBeavers)
        {
            Total = total;
            Beavers = beavers;
            Adults = adults;
            Children = children;
            Bots = bots;
            Employed = employed;
            OpenJobs = openJobs;
            Beds = beds;
            ContaminatedBeavers = contaminatedBeavers;
        }
    }

    /// <summary>Global stock of a single good, summed across all enabled inventories.</summary>
    public sealed class ResourceDto
    {
        [JsonProperty("goodId")] public string GoodId { get; }
        [JsonProperty("amount")] public int Amount { get; }
        [JsonProperty("capacity")] public int Capacity { get; }

        public ResourceDto(string goodId, int amount, int capacity)
        {
            GoodId = goodId;
            Amount = amount;
            Capacity = capacity;
        }
    }

    /// <summary>
    /// Colony-wide production summary across all manufactories. <c>operating</c> is the
    /// number actively able to produce; the rest are bucketed by the reason they're
    /// stopped (priority order: paused → no recipe → no workers → no power → missing
    /// ingredients/fuel → outputs full → idle). <c>utilization</c> is operating/buildings.
    /// </summary>
    public sealed class ProductionDto
    {
        [JsonProperty("buildings")] public int Buildings { get; }
        [JsonProperty("operating")] public int Operating { get; }
        [JsonProperty("utilization")] public float? Utilization { get; }
        [JsonProperty("paused")] public int Paused { get; }
        [JsonProperty("noWorkers")] public int NoWorkers { get; }
        [JsonProperty("noPower")] public int NoPower { get; }
        [JsonProperty("noIngredients")] public int NoIngredients { get; }
        [JsonProperty("outputFull")] public int OutputFull { get; }
        [JsonProperty("noRecipe")] public int NoRecipe { get; }
        [JsonProperty("idle")] public int Idle { get; }
        [JsonProperty("dominantConstraint")] public string? DominantConstraint { get; }

        public ProductionDto(int buildings, int operating, float? utilization, int paused,
            int noWorkers, int noPower, int noIngredients, int outputFull, int noRecipe, int idle,
            string? dominantConstraint)
        {
            Buildings = buildings;
            Operating = operating;
            Utilization = utilization;
            Paused = paused;
            NoWorkers = noWorkers;
            NoPower = noPower;
            NoIngredients = noIngredients;
            OutputFull = outputFull;
            NoRecipe = noRecipe;
            Idle = idle;
            DominantConstraint = dominantConstraint;
        }
    }

    /// <summary>
    /// Colony water-source summary. <c>contaminatedFraction</c> is the flow-weighted
    /// share of incoming water that is contaminated (badwater) — i.e. how much of what's
    /// entering the map is bad, which is what spikes during a badtide. Null when there is
    /// no active source flow.
    /// </summary>
    public sealed class WaterDto
    {
        [JsonProperty("sources")] public int Sources { get; }
        [JsonProperty("contaminatedSources")] public int ContaminatedSources { get; }
        [JsonProperty("contaminatedFraction")] public float? ContaminatedFraction { get; }
        [JsonProperty("totalStrength")] public float TotalStrength { get; }

        public WaterDto(int sources, int contaminatedSources, float? contaminatedFraction, float totalStrength)
        {
            Sources = sources;
            ContaminatedSources = contaminatedSources;
            ContaminatedFraction = contaminatedFraction;
            TotalStrength = totalStrength;
        }
    }

    /// <summary>Per-collector health so the dashboard can show Available/Error/Unavailable.</summary>
    public sealed class CollectorStatusDto
    {
        [JsonProperty("name")] public string Name { get; }
        [JsonProperty("status")] public string Status { get; }
        [JsonProperty("error")] public string? Error { get; }

        public CollectorStatusDto(string name, string status, string? error)
        {
            Name = name;
            Status = status;
            Error = error;
        }
    }
}

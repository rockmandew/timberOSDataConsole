using Timberborn.Population;

namespace TimberOS.DataConsole.Telemetry.Collectors
{
    /// <summary>
    /// Reads global population from <see cref="PopulationService.GlobalPopulationData"/>.
    /// All field names verified against Timberborn 1.0 PopulationData / WorkplaceData /
    /// BedData / ContaminationData.
    /// </summary>
    public sealed class PopulationCollector : ITelemetryCollector<PopulationDto>
    {
        private readonly PopulationService _populationService;

        public PopulationCollector(PopulationService populationService)
        {
            _populationService = populationService;
        }

        public string Name => "population";

        public bool IsAvailable => true;

        public PopulationDto Collect()
        {
            PopulationData data = _populationService.GlobalPopulationData;
            WorkplaceData beaverWork = data.BeaverWorkplaceData;
            BedData beds = data.BedData;

            return new PopulationDto(
                total: data.TotalPopulation,
                beavers: data.NumberOfBeavers,
                adults: data.NumberOfAdults,
                children: data.NumberOfChildren,
                bots: data.NumberOfBots,
                employed: beaverWork.OccupiedWorkslots,
                openJobs: beaverWork.FreeWorkslots,
                beds: beds.OccupiedBeds + beds.FreeBeds,
                contaminatedBeavers: data.ContaminationData.ContaminatedTotal);
        }
    }
}

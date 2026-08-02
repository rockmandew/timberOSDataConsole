using System;
using Timberborn.GameCycleSystem;
using Timberborn.HazardousWeatherSystem;
using Timberborn.WeatherSystem;

namespace TimberOS.DataConsole.Telemetry.Collectors
{
    /// <summary>
    /// Reads the weather cycle: whether the hazardous phase is active, the hazard type
    /// selected for this cycle (drought vs badtide), and days remaining in the current
    /// phase — the raw inputs a survival forecast needs.
    ///
    /// Verified sources (Timberborn 1.0):
    ///   - WeatherService.{IsHazardousWeather,TemperateWeatherDuration,HazardousWeatherDuration,
    ///     HazardousWeatherStartCycleDay,CycleLengthInDays}
    ///   - HazardousWeatherService.CurrentCycleHazardousWeather.Id  (drought/badtide)
    ///   - GameCycleService.CycleDay
    /// </summary>
    public sealed class WeatherCollector : ITelemetryCollector<WeatherDto>
    {
        private readonly WeatherService _weatherService;
        private readonly HazardousWeatherService _hazardousWeatherService;
        private readonly GameCycleService _gameCycleService;

        public WeatherCollector(WeatherService weatherService,
            HazardousWeatherService hazardousWeatherService, GameCycleService gameCycleService)
        {
            _weatherService = weatherService;
            _hazardousWeatherService = hazardousWeatherService;
            _gameCycleService = gameCycleService;
        }

        public string Name => "weather";

        public bool IsAvailable => true;

        public WeatherDto Collect()
        {
            bool isHazardous = _weatherService.IsHazardousWeather;
            int cycleDay = _gameCycleService.CycleDay;
            int hazardStartDay = _weatherService.HazardousWeatherStartCycleDay;
            int cycleLength = _weatherService.CycleLengthInDays;

            string? hazardId = _hazardousWeatherService.CurrentCycleHazardousWeather?.Id;

            int? daysUntilHazard = isHazardous ? (int?)null : Math.Max(0, hazardStartDay - cycleDay);
            int? hazardDaysRemaining = isHazardous ? (int?)Math.Max(0, cycleLength - cycleDay) : null;

            return new WeatherDto(
                isHazardous: isHazardous,
                hazardId: hazardId,
                temperateDurationDays: _weatherService.TemperateWeatherDuration,
                hazardDurationDays: _weatherService.HazardousWeatherDuration,
                daysUntilHazard: daysUntilHazard,
                hazardDaysRemaining: hazardDaysRemaining);
        }
    }
}

using System.Collections.Generic;
using Timberborn.MechanicalSystem;

namespace TimberOS.DataConsole.Telemetry.Collectors
{
    /// <summary>
    /// Summarizes every mechanical (power) network plus colony-wide totals.
    ///
    /// Verified sources (Timberborn 1.0):
    ///   - MechanicalGraphRegistry.MechanicalGraphs → all networks
    ///   - MechanicalGraph.{PowerSupply,PowerDemand,PowerSurplus,BatteryCharge,
    ///     BatteryCapacity,NumberOfGenerators,Powered}
    /// </summary>
    public sealed class PowerCollector : ITelemetryCollector<PowerDto>
    {
        private readonly MechanicalGraphRegistry _registry;

        public PowerCollector(MechanicalGraphRegistry registry)
        {
            _registry = registry;
        }

        public string Name => "power";

        public bool IsAvailable => true;

        public PowerDto Collect()
        {
            var networks = new List<PowerNetworkDto>();
            int totalSupply = 0, totalDemand = 0, totalCharge = 0, totalCapacity = 0, inDeficit = 0;
            int index = 0;

            foreach (MechanicalGraph graph in _registry.MechanicalGraphs)
            {
                int surplus = graph.PowerSurplus;
                networks.Add(new PowerNetworkDto(
                    index: index++,
                    supply: graph.PowerSupply,
                    demand: graph.PowerDemand,
                    surplus: surplus,
                    batteryCharge: graph.BatteryCharge,
                    batteryCapacity: graph.BatteryCapacity,
                    generators: graph.NumberOfGenerators,
                    powered: graph.Powered));

                totalSupply += graph.PowerSupply;
                totalDemand += graph.PowerDemand;
                totalCharge += graph.BatteryCharge;
                totalCapacity += graph.BatteryCapacity;
                if (surplus < 0)
                {
                    inDeficit++;
                }
            }

            return new PowerDto(
                networkCount: networks.Count,
                totalSupply: totalSupply,
                totalDemand: totalDemand,
                totalSurplus: totalSupply - totalDemand,
                totalBatteryCharge: totalCharge,
                totalBatteryCapacity: totalCapacity,
                networksInDeficit: inDeficit,
                networks: networks);
        }
    }
}

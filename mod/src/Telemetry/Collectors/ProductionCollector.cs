using System.Collections.Generic;
using Timberborn.Buildings;
using Timberborn.EntitySystem;
using Timberborn.MechanicalSystem;
using Timberborn.WorkSystem;
using Timberborn.Workshops;

namespace TimberOS.DataConsole.Telemetry.Collectors
{
    /// <summary>
    /// Colony-wide production state across every manufactory. Each building is
    /// classified by the FIRST applicable reason it isn't producing, in priority
    /// order, so the dashboard can drive gear motion by utilization and name the
    /// dominant constraint.
    ///
    /// Verified sources (Timberborn 1.0, decompiled):
    ///   - EntityComponentRegistry.GetEnabled&lt;Manufactory&gt;()
    ///   - Manufactory.{HasCurrentRecipe,HasAllIngredients,HasFuel,IsReadyToProduce,
    ///     HasUnreservedCapacityForCurrentProducts()}
    ///   - PausableBuilding.Paused · Workplace.NumberOfAssignedWorkers
    ///   - MechanicalNode.{IsConsumer,ActiveAndPowered}
    /// </summary>
    public sealed class ProductionCollector : ITelemetryCollector<ProductionDto>
    {
        private readonly EntityComponentRegistry _registry;

        public ProductionCollector(EntityComponentRegistry registry)
        {
            _registry = registry;
        }

        public string Name => "production";

        public bool IsAvailable => true;

        public ProductionDto Collect()
        {
            int total = 0, operating = 0;
            int paused = 0, noWorkers = 0, noPower = 0, noIngredients = 0, outputFull = 0, noRecipe = 0, idle = 0;

            foreach (Manufactory m in _registry.GetEnabled<Manufactory>())
            {
                total++;

                if (m.TryGetComponent(out PausableBuilding pausable) && pausable.Paused)
                {
                    paused++;
                    continue;
                }
                if (!m.HasCurrentRecipe)
                {
                    noRecipe++;
                    continue;
                }
                if (m.TryGetComponent(out Workplace workplace) && workplace.NumberOfAssignedWorkers == 0)
                {
                    noWorkers++;
                    continue;
                }
                if (m.TryGetComponent(out MechanicalNode node) && node.IsConsumer && !node.ActiveAndPowered)
                {
                    noPower++;
                    continue;
                }
                if (!m.HasAllIngredients || !m.HasFuel)
                {
                    noIngredients++;
                    continue;
                }
                if (!m.HasUnreservedCapacityForCurrentProducts())
                {
                    outputFull++;
                    continue;
                }
                if (m.IsReadyToProduce)
                {
                    operating++;
                }
                else
                {
                    idle++;
                }
            }

            float? utilization = total > 0 ? (float)operating / total : (float?)null;
            string? dominant = DominantConstraint(paused, noWorkers, noPower, noIngredients, outputFull, noRecipe, idle);

            return new ProductionDto(total, operating, utilization, paused, noWorkers, noPower,
                noIngredients, outputFull, noRecipe, idle, dominant);
        }

        private static string? DominantConstraint(int paused, int noWorkers, int noPower,
            int noIngredients, int outputFull, int noRecipe, int idle)
        {
            var buckets = new (string Name, int Count)[]
            {
                ("no_workers", noWorkers),
                ("no_power", noPower),
                ("no_ingredients", noIngredients),
                ("output_full", outputFull),
                ("no_recipe", noRecipe),
                ("paused", paused),
                ("idle", idle),
            };

            string? best = null;
            int bestCount = 0;
            foreach ((string name, int count) in buckets)
            {
                if (count > bestCount)
                {
                    best = name;
                    bestCount = count;
                }
            }
            return best;
        }
    }
}

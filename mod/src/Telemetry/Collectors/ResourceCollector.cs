using System.Collections.Generic;
using System.Linq;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;

namespace TimberOS.DataConsole.Telemetry.Collectors
{
    /// <summary>
    /// Aggregates global resource stock and capacity across every district's public
    /// inventories (stockpiles + warehouses — what the game shows as district stock).
    ///
    /// Verified sources (Timberborn 1.0):
    ///   - EntityComponentRegistry.GetEnabled&lt;DistrictCenter&gt;()   → all districts
    ///   - DistrictCenter → DistrictInventoryRegistry.Inventories        → public inventories
    ///   - Inventory.Stock (GoodAmount{GoodId,Amount})                   → stored amount
    ///   - Inventory.AllowedGoods (StorableGoodAmount{StorableGood.GoodId,Amount}) → per-good capacity
    ///
    /// Known limitation (v0.1): goods sitting in non-public building inventories
    /// (workshop inputs/outputs mid-process) are not counted. Tracked in the issue list.
    /// </summary>
    public sealed class ResourceCollector : ITelemetryCollector<IReadOnlyList<ResourceDto>>
    {
        private readonly EntityComponentRegistry _entityComponentRegistry;

        public ResourceCollector(EntityComponentRegistry entityComponentRegistry)
        {
            _entityComponentRegistry = entityComponentRegistry;
        }

        public string Name => "resources";

        public bool IsAvailable => true;

        public IReadOnlyList<ResourceDto> Collect()
        {
            var amounts = new Dictionary<string, int>();
            var capacities = new Dictionary<string, int>();

            foreach (DistrictCenter district in _entityComponentRegistry.GetEnabled<DistrictCenter>())
            {
                if (!district.TryGetComponent(out DistrictInventoryRegistry registry))
                {
                    continue;
                }

                foreach (Inventory inventory in registry.Inventories)
                {
                    foreach (GoodAmount stock in inventory.Stock)
                    {
                        Add(amounts, stock.GoodId, stock.Amount);
                    }

                    foreach (StorableGoodAmount allowed in inventory.AllowedGoods)
                    {
                        Add(capacities, allowed.StorableGood.GoodId, allowed.Amount);
                    }
                }
            }

            var goodIds = new HashSet<string>(amounts.Keys);
            goodIds.UnionWith(capacities.Keys);

            return goodIds
                .OrderBy(id => id)
                .Select(id => new ResourceDto(
                    goodId: id,
                    amount: amounts.TryGetValue(id, out int a) ? a : 0,
                    capacity: capacities.TryGetValue(id, out int c) ? c : 0))
                .ToList();
        }

        private static void Add(Dictionary<string, int> map, string key, int value)
        {
            map[key] = (map.TryGetValue(key, out int existing) ? existing : 0) + value;
        }
    }
}

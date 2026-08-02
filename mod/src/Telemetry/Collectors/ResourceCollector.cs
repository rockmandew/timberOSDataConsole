using System.Collections.Generic;
using System.Linq;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
// GoodAmount lives in Timberborn.Goods (already imported above).

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
    ///   - Inventory.GetCapacity(list)                                   → effective per-good capacity
    ///
    /// Capacity uses the game's own <see cref="Inventory.GetCapacity"/> rather than the raw
    /// <c>AllowedGoods</c> list. That matters: a warehouse's <c>AllowedGoods</c> reports EVERY good
    /// the building type could theoretically hold, each with the building's full capacity. But
    /// players restrict most storage to a single good (SingleGoodAllower), so raw AllowedGoods
    /// wildly over-counts — it credits a Log pile's capacity to Books, Bots, etc. that it can't
    /// actually store. GetCapacity applies LimitedAmount (= min(disallower, allowed)), skips
    /// ignorable-capacity inventories (construction sites), and drops zero-capacity goods, so the
    /// numbers match what the game shows per good in the district stock panel.
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
            var capacityBuffer = new List<GoodAmount>();

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

                    // GetCapacity appends the inventory's effective per-good capacity (honoring the
                    // player's SingleGoodAllower and skipping ignorable-capacity inventories).
                    capacityBuffer.Clear();
                    inventory.GetCapacity(capacityBuffer);
                    foreach (GoodAmount capacity in capacityBuffer)
                    {
                        Add(capacities, capacity.GoodId, capacity.Amount);
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

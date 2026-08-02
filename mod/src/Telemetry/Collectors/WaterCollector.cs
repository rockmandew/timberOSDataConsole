using Timberborn.EntitySystem;
using Timberborn.WaterSourceSystem;

namespace TimberOS.DataConsole.Telemetry.Collectors
{
    /// <summary>
    /// Colony water-source contamination. Aggregates every water source's current
    /// strength and contamination into a flow-weighted "how much of the incoming
    /// water is bad" fraction — the signal that spikes during a badtide. Cheap
    /// (there are few sources) and read on the main thread.
    ///
    /// Verified sources (Timberborn 1.0, decompiled):
    ///   - EntityComponentRegistry.GetEnabled&lt;WaterSource&gt;()
    ///   - WaterSource.{CurrentStrength, Contamination}  (Contamination is 0..1)
    /// </summary>
    public sealed class WaterCollector : ITelemetryCollector<WaterDto>
    {
        private const float ContaminationThreshold = 0.01f;

        private readonly EntityComponentRegistry _registry;

        public WaterCollector(EntityComponentRegistry registry)
        {
            _registry = registry;
        }

        public string Name => "water";

        public bool IsAvailable => true;

        public WaterDto Collect()
        {
            int sources = 0, contaminated = 0;
            float totalStrength = 0f, contaminatedStrength = 0f;

            foreach (WaterSource source in _registry.GetEnabled<WaterSource>())
            {
                sources++;
                float strength = source.CurrentStrength;
                float contamination = source.Contamination;
                totalStrength += strength;
                contaminatedStrength += strength * contamination;
                if (contamination > ContaminationThreshold)
                {
                    contaminated++;
                }
            }

            float? fraction = totalStrength > 0f ? contaminatedStrength / totalStrength : (float?)null;
            return new WaterDto(sources, contaminated, fraction, totalStrength);
        }
    }
}

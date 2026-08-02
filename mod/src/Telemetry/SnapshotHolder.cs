using System.Threading;

namespace TimberOS.DataConsole.Telemetry
{
    /// <summary>
    /// Thread-safe hand-off between the main-thread <see cref="SnapshotCoordinator"/>
    /// (writer) and the HTTP listener thread (reader). The coordinator builds an
    /// immutable <see cref="TelemetryEnvelope"/> on the Unity main thread and publishes
    /// it here; the HTTP endpoint reads the latest reference and serializes it. Because
    /// the envelope is immutable and swapped atomically, the reader never touches live
    /// game state and never blocks the game thread.
    /// </summary>
    public sealed class SnapshotHolder
    {
        private volatile TelemetryEnvelope? _current;

        /// <summary>Latest published snapshot, or null before the first collection completes.</summary>
        public TelemetryEnvelope? Current => _current;

        public void Publish(TelemetryEnvelope envelope)
        {
            Interlocked.Exchange(ref _current, envelope);
        }
    }
}

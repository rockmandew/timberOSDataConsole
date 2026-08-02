namespace TimberOS.DataConsole.Telemetry.Collectors
{
    /// <summary>
    /// A collector reads one telemetry domain from the game. Collectors run on the
    /// Unity main thread (driven by <see cref="SnapshotCoordinator"/>), never on the
    /// HTTP listener thread. Each returns an immutable slice; failures are caught by
    /// the coordinator so one broken collector never takes down the snapshot.
    /// </summary>
    /// <typeparam name="T">The immutable slice this collector produces.</typeparam>
    public interface ITelemetryCollector<out T> where T : class
    {
        /// <summary>Stable name used in collector-status reporting.</summary>
        string Name { get; }

        /// <summary>False when the required game services are missing this session.</summary>
        bool IsAvailable { get; }

        /// <summary>Read current state and return an immutable slice. May throw; the coordinator handles it.</summary>
        T Collect();
    }
}

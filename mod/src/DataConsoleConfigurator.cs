using Bindito.Core;
using TimberOS.DataConsole.Http;
using TimberOS.DataConsole.Telemetry;
using TimberOS.DataConsole.Telemetry.Collectors;

namespace TimberOS.DataConsole
{
    /// <summary>
    /// Wires the mod into Timberborn's Game scene via Bindito DI. Mirrors the game's
    /// own HttpApiSystemConfigurator pattern: singletons for the collectors/coordinator,
    /// and a MultiBind that adds our telemetry endpoint to the native HTTP API's
    /// endpoint list so it starts serving automatically with the settlement.
    /// </summary>
    [Context("Game")]
    public sealed class DataConsoleConfigurator : Configurator
    {
        protected override void Configure()
        {
            Bind<SnapshotHolder>().AsSingleton();

            Bind<GameStateCollector>().AsSingleton();
            Bind<PopulationCollector>().AsSingleton();
            Bind<ResourceCollector>().AsSingleton();
            Bind<WeatherCollector>().AsSingleton();
            Bind<PowerCollector>().AsSingleton();

            // ILoadableSingleton + IUpdatableSingleton — the SingletonSystem drives it.
            Bind<SnapshotCoordinator>().AsSingleton();

            // Adds our endpoint to the native HTTP server's IHttpApiEndpoint collection.
            MultiBind<Timberborn.HttpApiSystem.IHttpApiEndpoint>().To<TelemetryEndpoint>().AsSingleton();
        }
    }
}

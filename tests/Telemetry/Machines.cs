using SentinelAgente.Inventory;

namespace SentinelAgente.Tests.Telemetry;

/// <summary>As máquinas do testdata.</summary>
internal static class Machines
{
    public static SystemRoot Root(string name) => new(Path.Combine(AppContext.BaseDirectory, "testdata", "maquinas", name));
}

using SentinelAgente.Connection;
using SentinelAgente.Inventory;
using SentinelAgente.Telemetry.Cpu;
using SentinelAgente.Telemetry.Disk;
using SentinelAgente.Telemetry.Memory;

namespace SentinelAgente.Telemetry;

public static class TelemetryRegistration
{
    /// <summary>
    /// Registra os coletores e a amostra de hora em hora. Pede o
    /// <see cref="IServerLink"/> da conexão e o HWID do <see cref="MachineInventory"/>,
    /// o mesmo do handshake.
    /// </summary>
    public static IServiceCollection AddTelemetry(this IServiceCollection services)
    {
        var root = new SystemRoot("/");

        services.AddSingleton(new TelemetryTimings());
        services.AddSingleton<ITelemetryCollector>(new CpuUsage(root));
        services.AddSingleton<ITelemetryCollector>(new MemoryUsage(root));
        services.AddSingleton<ITelemetryCollector>(new DiskUsage(root, new DriveStats()));

        services.AddHostedService(provider => new TelemetryService(
            provider.GetRequiredService<IServerLink>(),
            provider.GetServices<ITelemetryCollector>(),
            provider.GetRequiredService<TelemetryTimings>(),
            () => provider.GetRequiredService<MachineInventory>().Hwid,
            provider.GetRequiredService<ILogger<TelemetryService>>()));
        return services;
    }
}

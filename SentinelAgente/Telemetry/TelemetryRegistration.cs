using SentinelAgente.Connection;
using SentinelAgente.Identity;
using SentinelAgente.Inventory;
using SentinelAgente.Telemetry.Cpu;
using SentinelAgente.Telemetry.Disk;
using SentinelAgente.Telemetry.Memory;
using SentinelAgente.Telemetry.Network;
using SentinelAgente.Telemetry.Processes;

namespace SentinelAgente.Telemetry;

public static class TelemetryRegistration
{
    /// <summary>
    /// Registra os coletores e a amostra de hora em hora. Pede o
    /// <see cref="IServerLink"/> da conexão.
    /// </summary>
    public static IServiceCollection AddTelemetry(this IServiceCollection services)
    {
        var root = new SystemRoot("/");

        services.AddSingleton(new TelemetryTimings());
        services.AddSingleton<ITelemetryCollector>(new CpuUsage(root));
        services.AddSingleton<ITelemetryCollector>(new MemoryUsage(root));
        services.AddSingleton<ITelemetryCollector>(new DiskUsage(root, new DriveStats()));
        services.AddSingleton<ITelemetryCollector>(new NetworkUsage(root, TimeProvider.System));
        services.AddSingleton<ITelemetryCollector>(new TopProcesses(root));

        services.AddHostedService(provider => new TelemetryService(
            provider.GetRequiredService<IServerLink>(),
            provider.GetServices<ITelemetryCollector>(),
            provider.GetRequiredService<TelemetryTimings>(),
            () => Hwid.ReadOrCreate(),
            provider.GetRequiredService<ILogger<TelemetryService>>()));
        return services;
    }
}

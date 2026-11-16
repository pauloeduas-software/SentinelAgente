using SentinelAgente.Protocol;

namespace SentinelAgente.Telemetry;

/// <summary>A amostra que os coletores preenchem; o que nenhum preencheu vai zerado.</summary>
public sealed class TelemetrySample
{
    public double CpuUsagePercentage { get; set; }
    public long RamTotalBytes { get; set; }
    public long RamUsedBytes { get; set; }
    public IReadOnlyList<DiskPayload> Disks { get; set; } = [];

    public TelemetryPayload ToPayload(string hwid) => new()
    {
        Hwid = hwid,
        CpuUsagePercentage = CpuUsagePercentage,
        RamTotalBytes = RamTotalBytes,
        RamUsedBytes = RamUsedBytes,
        Disks = Disks,
    };
}

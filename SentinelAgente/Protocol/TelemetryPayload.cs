namespace SentinelAgente.Protocol;

/// <summary>
/// A amostra de uso, de hora em hora (A12). Espelho do `TelemetryData` do
/// SentinelWeb (`server/modules/shared/agent-protocol.types.ts`); o que cada
/// número quer dizer está na A16.
/// </summary>
/// <remarks>
/// Campo ausente o servidor grava como zero ou lista vazia: a parte que um
/// coletor não conseguiu ler vai assim, e o resto da amostra segue.
/// </remarks>
public sealed record TelemetryPayload
{
    public required string Hwid { get; init; }
    public double CpuUsagePercentage { get; init; }
    public long RamTotalBytes { get; init; }
    public long RamUsedBytes { get; init; }
    public IReadOnlyList<DiskPayload> Disks { get; init; } = [];
    public NetworkPayload? Network { get; init; }
    public IReadOnlyList<ProcessPayload> TopProcesses { get; init; } = [];
}

/// <summary>Um ponto de montagem, em GB de 1024³ bytes.</summary>
public sealed record DiskPayload(string Name, double TotalGb, double UsedGb);

/// <summary>Os contadores desde o boot, em bytes, e a velocidade média em kbit/s (1 kbit = 1000 bits).</summary>
public sealed record NetworkPayload(long BytesReceived, long BytesSent, double RxSpeedKbps, double TxSpeedKbps);

/// <summary>Um processo e a RAM que ele ocupa (o RSS), em MB de 1024² bytes.</summary>
public sealed record ProcessPayload(int Pid, string Name, double RamMb);

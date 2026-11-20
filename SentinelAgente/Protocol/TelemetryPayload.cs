namespace SentinelAgente.Protocol;

/// <summary>
/// A amostra de uso, de hora em hora (A12) e a cada <c>REFRESH</c> (A26). Os
/// nomes são os que o <c>toTelemetryData</c> do SentinelWeb lê
/// (<c>server/modules/agent/helpers/normalize-payload.helper.ts</c>); o que cada
/// número quer dizer está na A16.
/// </summary>
/// <remarks>
/// Campo ausente o servidor grava como zero ou lista vazia: a parte que um
/// coletor não conseguiu ler vai assim, e o resto da amostra segue. A rede e os
/// processos o agente não manda (A24).
/// </remarks>
public sealed record TelemetryPayload
{
    public required string Hwid { get; init; }
    public double CpuUsagePercentage { get; init; }
    public long RamTotalBytes { get; init; }
    public long RamUsedBytes { get; init; }
    public IReadOnlyList<DiskPayload> Disks { get; init; } = [];
}

/// <summary>Um ponto de montagem, em GB de 1024³ bytes.</summary>
public sealed record DiskPayload(string Name, double TotalGb, double UsedGb);

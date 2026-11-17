namespace SentinelAgente.Protocol;

/// <summary>
/// A apresentação da máquina: a primeira mensagem de toda conexão. Espelho do
/// `HandshakeData` do SentinelWeb (`server/modules/shared/agent-protocol.types.ts`);
/// o que cada campo vira lá está em docs/referencia/protocolo.md.
/// </summary>
/// <remarks>
/// O HWID, o nome, o sistema e a versão do agente vão sempre: sem o sistema, o
/// servidor grava "desconhecido", e sem a versão, que o agente é anterior à A25. O resto depende de permissão e de firmware, e campo nulo não
/// vai para o fio. <c>ChassisType</c> e <c>DiskTotalBytes</c> o agente não manda
/// (A14); a RAM e os programas, também não (A24).
/// </remarks>
public sealed record HandshakePayload
{
    public required string Hwid { get; init; }
    public required string Hostname { get; init; }
    public required string OsVersion { get; init; }
    public required string AgentVersion { get; init; }
    public string? CpuModel { get; init; }
    public string? MacAddress { get; init; }
    public string? LocalIp { get; init; }
    public string? BiosSerial { get; init; }
    public string? SystemUuid { get; init; }
    public string? Manufacturer { get; init; }
    public string? HardwareModel { get; init; }
    public IReadOnlyList<GpuPayload>? Gpus { get; init; }
}

/// <summary>
/// Uma placa de vídeo: o nome, do <c>pci.ids</c>, e a memória, o que a aba
/// Máquina do SentinelWeb mostra (A24).
/// </summary>
public sealed record GpuPayload(string Name, long? MemoryBytes);

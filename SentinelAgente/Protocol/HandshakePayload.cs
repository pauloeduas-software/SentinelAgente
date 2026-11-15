namespace SentinelAgente.Protocol;

/// <summary>
/// A apresentação da máquina: a primeira mensagem de toda conexão. Espelho do
/// `HandshakeData` do SentinelWeb (`server/modules/shared/agent-protocol.types.ts`);
/// o que cada campo vira lá está em docs/referencia/protocolo.md.
/// </summary>
/// <remarks>
/// Só o HWID e o nome são obrigatórios. O resto depende de permissão e de
/// firmware, e campo nulo não vai para o fio.
/// </remarks>
public sealed record HandshakePayload
{
    public required string Hwid { get; init; }
    public required string Hostname { get; init; }
    public string? OsVersion { get; init; }
    public string? CpuModel { get; init; }
    public string? MacAddress { get; init; }
    public string? LocalIp { get; init; }
    public string? BiosSerial { get; init; }
    public string? SystemUuid { get; init; }
    public string? Manufacturer { get; init; }
    public string? HardwareModel { get; init; }
    public string? ChassisType { get; init; }
    public long? RamTotalBytes { get; init; }
    public long? DiskTotalBytes { get; init; }
}

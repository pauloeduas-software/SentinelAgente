namespace SentinelAgente.Protocol;

/// <summary>
/// A apresentação da máquina: a primeira mensagem de toda conexão, e de novo
/// uma vez por dia (A25) e a cada <c>REFRESH</c> (A26). Os campos são os que o
/// <c>toHandshakeData</c> do SentinelWeb lê
/// (<c>server/modules/agent/helpers/normalize-payload.helper.ts</c>); o que cada
/// um vira lá está em docs/referencia/protocolo.md.
/// </summary>
/// <remarks>
/// O HWID, o nome, o sistema e a versão do agente vão sempre: sem o sistema, o
/// servidor grava "desconhecido", e sem a versão, apaga a gravada. O resto
/// depende da máquina (permissão, firmware, rota padrão, processador, placa de
/// vídeo), e campo nulo não vai para o fio. A RAM e os programas o agente não
/// manda (A24).
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
/// Uma placa de vídeo: o nome, do <c>pci.ids</c>, e a memória da placa dedicada,
/// o que o SentinelWeb mostra (A24); a integrada vai sem memória (A28).
/// </summary>
public sealed record GpuPayload(string Name, long? MemoryBytes);

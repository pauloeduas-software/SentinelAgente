namespace SentinelAgente.Protocol;

/// <summary>
/// A apresentação da máquina: a primeira mensagem de toda conexão. Espelho do
/// `HandshakeData` do SentinelWeb (`server/modules/shared/agent-protocol.types.ts`);
/// o que cada campo vira lá está em docs/referencia/protocolo.md.
/// </summary>
/// <remarks>
/// O HWID, o nome e o sistema vão sempre: sem o sistema, o servidor grava
/// "desconhecido" e conta uma mudança de hardware. O resto depende de permissão
/// e de firmware, e campo nulo não vai para o fio. <c>ChassisType</c> e
/// <c>DiskTotalBytes</c> o agente não manda (A14). Os programas e as placas de
/// vídeo são os do GLPI Agent (A22): o servidor ainda os descarta.
/// </remarks>
public sealed record HandshakePayload
{
    public required string Hwid { get; init; }
    public required string Hostname { get; init; }
    public required string OsVersion { get; init; }
    public string? CpuModel { get; init; }
    public string? MacAddress { get; init; }
    public string? LocalIp { get; init; }
    public string? BiosSerial { get; init; }
    public string? SystemUuid { get; init; }
    public string? Manufacturer { get; init; }
    public string? HardwareModel { get; init; }
    public long? RamTotalBytes { get; init; }
    public IReadOnlyList<SoftwarePayload>? InstalledSoftware { get; init; }
    public IReadOnlyList<GpuPayload>? Gpus { get; init; }
}

/// <summary>
/// Um pacote instalado, com os campos do <c>SOFTWARES</c> do GLPI Agent. <c>From</c>
/// é o gerenciador (<c>deb</c> ou <c>rpm</c>); o tamanho vai em bytes, e a data
/// da instalação, que só o rpm guarda, no fuso da máquina.
/// </summary>
public sealed record SoftwarePayload(
    string Name,
    string Version,
    string? Arch,
    string From,
    string? Publisher = null,
    string? Category = null,
    long? SizeBytes = null,
    DateOnly? InstallDate = null);

/// <summary>
/// Uma placa de vídeo, com os campos do <c>VIDEOS</c> do GLPI Agent: o nome e o
/// chip saem do <c>pci.ids</c>, e o <c>PciId</c> é o par fabricante:dispositivo.
/// </summary>
public sealed record GpuPayload(
    string Name,
    string? Manufacturer,
    string? Chipset,
    long? MemoryBytes,
    string PciSlot,
    string PciId);

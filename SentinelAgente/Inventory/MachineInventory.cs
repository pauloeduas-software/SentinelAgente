using SentinelAgente.Connection;
using SentinelAgente.Protocol;
using SentinelAgente.Software;

namespace SentinelAgente.Inventory;

/// <summary>
/// A ficha da máquina que o handshake leva (A14), relida a cada conexão: são
/// poucos arquivos, e assim a peça trocada aparece na próxima.
/// </summary>
/// <remarks>
/// O HWID é lido, ou sorteado, uma vez (A20), e é o mesmo para quem mais o
/// manda (a telemetria). Se o arquivo dele não abre, a exceção sai antes da
/// primeira conexão e para o serviço. A lista de programas vem do
/// <c>Software</c>, relida também a cada conexão, como o GLPI Agent faz a cada
/// inventário.
/// </remarks>
public sealed class MachineInventory : IHandshakeSource
{
    private readonly SystemRoot _root;
    private readonly Func<string, IReadOnlyList<InterfaceAddress>> _addressesOf;
    private readonly Lazy<string> _hwid;
    private readonly Func<CancellationToken, Task<IReadOnlyList<SoftwarePayload>?>> _software;

    internal MachineInventory(InstalledSoftware software)
        : this("/", PrimaryNetwork.SystemAddresses, Identity.Hwid.DefaultFile, software.ReadAsync) { }

    internal MachineInventory(
        string root,
        Func<string, IReadOnlyList<InterfaceAddress>> addressesOf,
        string hwidFile,
        Func<CancellationToken, Task<IReadOnlyList<SoftwarePayload>?>>? software = null)
    {
        _root = new SystemRoot(root);
        _addressesOf = addressesOf;
        _hwid = new Lazy<string>(() => Identity.Hwid.ReadOrCreate(hwidFile));
        _software = software ?? (_ => Task.FromResult<IReadOnlyList<SoftwarePayload>?>(null));
    }

    /// <summary>
    /// O HWID com que a conexão se apresentou: quem manda dado usa este, e não
    /// relê o arquivo, como o <c>AgentID</c> do Tactical RMM, lido uma vez.
    /// </summary>
    public string Hwid => _hwid.Value;

    public async ValueTask<HandshakePayload> CreateAsync(CancellationToken cancellationToken) =>
        Create() with { InstalledSoftware = await _software(cancellationToken) };

    internal HandshakePayload Create()
    {
        var dmi = DmiInfo.Read(_root);
        var network = PrimaryNetwork.Read(_root, _addressesOf);

        return new HandshakePayload
        {
            Hwid = Hwid,
            Hostname = Hostname(),
            OsVersion = OsRelease.Read(_root),
            CpuModel = CpuInfo.Read(_root),
            MacAddress = network.MacAddress,
            LocalIp = network.LocalIp,
            BiosSerial = dmi.BiosSerial,
            SystemUuid = dmi.SystemUuid,
            Manufacturer = dmi.Manufacturer,
            HardwareModel = dmi.HardwareModel,
            RamTotalBytes = MemInfo.Read(_root),
            Gpus = GpuInfo.Read(_root),
        };
    }

    /// <summary>
    /// O nome curto, o mesmo do <c>gethostname</c>: o domínio fica de fora, como
    /// no GLPI Agent e no OCS, e a reconciliação do SentinelWeb também o tira.
    /// </summary>
    private string Hostname()
    {
        var name = _root.ReadValue("proc/sys/kernel/hostname") ?? Environment.MachineName;
        var dot = name.IndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }
}

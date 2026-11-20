using SentinelAgente.Connection;
using SentinelAgente.Protocol;

namespace SentinelAgente.Inventory;

/// <summary>
/// A ficha da máquina que o handshake leva (A14), relida a cada handshake: na
/// conexão, no reenvio diário (A25) e no <c>REFRESH</c> (A26). São poucos
/// arquivos, mais o <c>nvidia-smi</c> na máquina com NVIDIA, e assim a peça
/// trocada aparece no mesmo dia.
/// </summary>
/// <remarks>
/// O HWID é lido, ou sorteado, uma vez (A20), e é o mesmo para quem mais o
/// manda (a telemetria). Se o arquivo dele não abre, a exceção sai antes da
/// primeira conexão e derruba o agente com código 1; o systemd tenta de novo a
/// cada 30 s.
/// </remarks>
public sealed class MachineInventory : IHandshakeSource
{
    private readonly SystemRoot _root;
    private readonly Func<string, IReadOnlyList<InterfaceAddress>> _addressesOf;
    private readonly Func<IReadOnlyDictionary<string, long>> _nvidiaMemory;
    private readonly Lazy<string> _hwid;

    internal MachineInventory()
        : this("/", PrimaryNetwork.SystemAddresses, NvidiaSmi.Read, Identity.Hwid.DefaultFile) { }

    internal MachineInventory(
        string root,
        Func<string, IReadOnlyList<InterfaceAddress>> addressesOf,
        Func<IReadOnlyDictionary<string, long>> nvidiaMemory,
        string hwidFile)
    {
        _root = new SystemRoot(root);
        _addressesOf = addressesOf;
        _nvidiaMemory = nvidiaMemory;
        _hwid = new Lazy<string>(() => Identity.Hwid.ReadOrCreate(hwidFile));
    }

    /// <summary>
    /// O HWID com que a conexão se apresentou: quem manda dado usa este, e não
    /// relê o arquivo, como o <c>AgentID</c> do Tactical RMM, lido uma vez.
    /// </summary>
    public string Hwid => _hwid.Value;

    public ValueTask<HandshakePayload> CreateAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Create());

    internal HandshakePayload Create()
    {
        var dmi = DmiInfo.Read(_root);
        var network = PrimaryNetwork.Read(_root, _addressesOf);

        return new HandshakePayload
        {
            Hwid = Hwid,
            Hostname = Hostname(),
            OsVersion = OsRelease.Read(_root),
            AgentVersion = Inventory.AgentVersion.Current,
            CpuModel = CpuInfo.Read(_root),
            MacAddress = network.MacAddress,
            LocalIp = network.LocalIp,
            BiosSerial = dmi.BiosSerial,
            SystemUuid = dmi.SystemUuid,
            Manufacturer = dmi.Manufacturer,
            HardwareModel = dmi.HardwareModel,
            Gpus = GpuInfo.Read(_root, _nvidiaMemory),
        };
    }

    /// <summary>
    /// O nome curto, o mesmo do <c>gethostname</c>: o domínio fica de fora, como
    /// no GLPI Agent e no OCS.
    /// </summary>
    private string Hostname()
    {
        var name = _root.ReadValue("proc/sys/kernel/hostname") ?? Environment.MachineName;
        var dot = name.IndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }
}

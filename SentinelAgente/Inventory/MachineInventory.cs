using SentinelAgente.Connection;
using SentinelAgente.Identity;
using SentinelAgente.Protocol;

namespace SentinelAgente.Inventory;

/// <summary>
/// A ficha da máquina que o handshake leva (A14), relida a cada conexão: são
/// poucos arquivos, e assim a peça trocada aparece na próxima.
/// </summary>
/// <remarks>
/// O HWID é lido, ou sorteado, uma vez (A20). Se o arquivo dele não abre, a
/// exceção sai da primeira conexão e para o serviço.
/// </remarks>
public sealed class MachineInventory : IHandshakeSource
{
    private readonly SystemRoot _root;
    private readonly Func<string, IReadOnlyList<InterfaceAddress>> _addressesOf;
    private readonly string _hwidFile;
    private string? _hwid;

    public MachineInventory() : this("/", PrimaryNetwork.SystemAddresses, Hwid.DefaultFile) { }

    internal MachineInventory(string root, Func<string, IReadOnlyList<InterfaceAddress>> addressesOf, string hwidFile)
    {
        _root = new SystemRoot(root);
        _addressesOf = addressesOf;
        _hwidFile = hwidFile;
    }

    public ValueTask<HandshakePayload> CreateAsync(CancellationToken cancellationToken) => ValueTask.FromResult(Create());

    internal HandshakePayload Create()
    {
        _hwid ??= Hwid.ReadOrCreate(_hwidFile);
        var dmi = DmiInfo.Read(_root);
        var network = PrimaryNetwork.Read(_root, _addressesOf);

        return new HandshakePayload
        {
            Hwid = _hwid,
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

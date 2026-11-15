using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SentinelAgente.Inventory;

internal sealed record InterfaceAddress(IPAddress Address, int PrefixLength);

internal sealed record NetworkData(string? MacAddress, string? LocalIp);

/// <summary>
/// O MAC e o IP da placa por onde a máquina sai para a rede: a da rota padrão
/// de menor métrica, como o Fleet escolhe (<c>networkInterfaceQuery</c>), e
/// física antes de virtual, como o GLPI Agent separa (<c>Linux/Networks.pm</c>).
/// </summary>
/// <remarks>
/// A placa física é a que tem <c>/sys/class/net/&lt;placa&gt;/device</c>. A VPN
/// (<c>tun0</c>, <c>wg0</c>) que vira rota padrão fica para trás; a bridge do
/// servidor de VMs (<c>br0</c>) serve quando é a única rota.
/// </remarks>
internal static class PrimaryNetwork
{
    private const uint RouteUp = 0x1;

    public static NetworkData Read(SystemRoot root, Func<string, IReadOnlyList<InterfaceAddress>> addressesOf)
    {
        var route = DefaultRoutes(root)
            .OrderByDescending(r => root.HasDirectory($"sys/class/net/{r.Interface}/device"))
            .ThenBy(r => r.Metric)
            .FirstOrDefault();
        if (route is null) return new NetworkData(null, null);

        var mac = root.ReadValue($"sys/class/net/{route.Interface}/address")?.ToLowerInvariant();
        if (mac == "00:00:00:00:00:00") mac = null;

        return new NetworkData(mac, LocalIp(addressesOf(route.Interface), route.Gateway)?.ToString());
    }

    /// <summary>O IPv4 da placa na mesma rede do gateway; sem gateway, o primeiro.</summary>
    private static IPAddress? LocalIp(IReadOnlyList<InterfaceAddress> addresses, IPAddress gateway)
    {
        var ipv4 = addresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork).ToList();
        return ipv4.FirstOrDefault(a => new IPNetwork(Network(a), a.PrefixLength).Contains(gateway))?.Address
            ?? ipv4.FirstOrDefault()?.Address;
    }

    private static IPAddress Network(InterfaceAddress address)
    {
        var bytes = address.Address.GetAddressBytes();
        for (var bit = address.PrefixLength; bit < 32; bit++) bytes[bit / 8] &= (byte)~(0x80 >> (bit % 8));
        return new IPAddress(bytes);
    }

    private sealed record Route(string Interface, IPAddress Gateway, int Metric);

    /// <summary>
    /// As rotas padrão do <c>/proc/net/route</c>: destino e máscara zero, no ar.
    /// Os endereços vêm em hexadecimal na ordem de bytes da máquina.
    /// </summary>
    private static IEnumerable<Route> DefaultRoutes(SystemRoot root)
    {
        foreach (var line in (root.ReadText("proc/net/route") ?? "").Split('\n').Skip(1))
        {
            var columns = line.Split((char[])['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 8 ||
                !uint.TryParse(columns[1], NumberStyles.HexNumber, null, out var destination) ||
                !uint.TryParse(columns[2], NumberStyles.HexNumber, null, out var gateway) ||
                !uint.TryParse(columns[3], NumberStyles.HexNumber, null, out var flags) ||
                !int.TryParse(columns[6], out var metric) ||
                !uint.TryParse(columns[7], NumberStyles.HexNumber, null, out var mask))
                continue;

            if (destination == 0 && mask == 0 && (flags & RouteUp) != 0)
                yield return new Route(columns[0], new IPAddress(BitConverter.GetBytes(gateway)), metric);
        }
    }

    /// <summary>Os endereços da placa, pelo sistema.</summary>
    public static IReadOnlyList<InterfaceAddress> SystemAddresses(string interfaceName) =>
        NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.Name == interfaceName)?
            .GetIPProperties().UnicastAddresses
            .Select(u => new InterfaceAddress(u.Address, u.PrefixLength))
            .ToList() ?? [];
}

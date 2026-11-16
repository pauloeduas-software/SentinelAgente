namespace SentinelAgente.Inventory;

/// <summary>
/// Os nomes do banco de IDs do PCI, o mesmo que o <c>lspci</c> e o GLPI Agent
/// usam: no Debian e no Ubuntu em <c>/usr/share/misc</c> (pacote <c>pci.ids</c>), no
/// Fedora em <c>/usr/share/hwdata</c>.
/// </summary>
/// <remarks>
/// O arquivo tem 1,5 MB: só os fabricantes pedidos são guardados, e a leitura
/// para nas classes, que vêm depois de todos os dispositivos. Sem o arquivo,
/// nome nenhum, e quem chamou fica com o id.
/// </remarks>
internal sealed class PciIds
{
    private static readonly string[] Files = ["usr/share/misc/pci.ids", "usr/share/hwdata/pci.ids"];

    private readonly Dictionary<string, string> _vendors = [];
    private readonly Dictionary<string, string> _devices = [];

    /// <summary>O nome do fabricante (<c>10de</c>), ou <c>null</c>.</summary>
    public string? Vendor(string vendor) => _vendors.GetValueOrDefault(vendor);

    /// <summary>O nome do dispositivo (<c>10de:1f99</c>), ou <c>null</c>.</summary>
    public string? Device(string pciId) => _devices.GetValueOrDefault(pciId);

    public static PciIds Read(SystemRoot root, IReadOnlySet<string> vendors)
    {
        var ids = new PciIds();
        foreach (var file in Files)
        {
            try
            {
                ids.Load(File.ReadLines(root.Combine(file)), vendors);
                return ids;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // O outro lugar, ou nome nenhum.
            }
        }
        return ids;
    }

    private void Load(IEnumerable<string> lines, IReadOnlySet<string> vendors)
    {
        string? vendor = null;
        foreach (var line in lines)
        {
            if (line.Length == 0 || line[0] == '#') continue;
            if (line.StartsWith("C ")) break;

            if (line[0] != '\t')
            {
                // "10de  NVIDIA Corporation"
                vendor = Id(line, 0) is { } id && vendors.Contains(id) ? id : null;
                if (vendor is not null) _vendors[vendor] = line[6..].Trim();
            }
            else if (vendor is not null && line.Length > 1 && line[1] != '\t' && Id(line, 1) is { } device)
            {
                // "\t1f99  TU117M [GeForce GTX 1650 Mobile / Max-Q]"; o subsistema, com dois tabs, não serve.
                _devices[$"{vendor}:{device}"] = line[7..].Trim();
            }
        }
    }

    // Os quatro dígitos hexadecimais e os dois espaços que separam o nome.
    private static string? Id(string line, int start) =>
        line.Length > start + 6 && line.AsSpan(start + 4, 2) is "  " && !line.AsSpan(start, 4).ContainsAnyExcept("0123456789abcdef")
            ? line.Substring(start, 4)
            : null;
}

using SentinelAgente.Protocol;

namespace SentinelAgente.Inventory;

/// <summary>
/// As placas de vídeo, como o GLPI Agent (<c>Generic/PCI/Videos.pm</c>): os
/// dispositivos PCI de vídeo, com o nome e o chip do <c>pci.ids</c> (A22). A
/// classe é a do <c>/sys/bus/pci/devices/&lt;slot&gt;/class</c>, <c>0x03</c> (VGA, 3D e
/// tela), em vez do nome que o lspci escreve, que é o que o GLPI compara.
/// </summary>
/// <remarks>
/// "Navi 22 [Radeon RX 6700 XT]" vira chip "Navi 22" e nome "Radeon RX 6700 XT",
/// a mesma divisão do GLPI; sem colchete, o nome é o texto inteiro, e sem o
/// <c>pci.ids</c>, o id. A memória é a VRAM que o <c>amdgpu</c> mostra: o GLPI
/// manda o tamanho da janela do PCI, que é 256 MB numa placa de 8 GB, e a NVIDIA
/// e a Intel não têm arquivo para ela. A resolução, que o GLPI tira do
/// <c>xrandr</c>, não vai: o serviço não tem tela.
/// </remarks>
internal static class GpuInfo
{
    private const string Devices = "sys/bus/pci/devices";

    /// <summary>As placas, na ordem do slot; nenhuma, <c>null</c>.</summary>
    public static IReadOnlyList<GpuPayload>? Read(SystemRoot root)
    {
        if (!root.HasDirectory(Devices)) return null;

        var cards = new List<(string Address, string Vendor, string Device, long? Vram)>();
        foreach (var directory in Directory.EnumerateDirectories(root.Combine(Devices)))
        {
            var address = Path.GetFileName(directory);
            if (root.ReadValue($"{Devices}/{address}/class") is not { } type || !type.StartsWith("0x03")) continue;
            if (Hex(root.ReadValue($"{Devices}/{address}/vendor")) is not { } vendor) continue;
            if (Hex(root.ReadValue($"{Devices}/{address}/device")) is not { } device) continue;

            var vram = long.TryParse(root.ReadValue($"{Devices}/{address}/mem_info_vram_total"), out var bytes) && bytes > 0
                ? bytes
                : (long?)null;
            cards.Add((address, vendor, device, vram));
        }

        if (cards.Count == 0) return null;
        var ids = PciIds.Read(root, cards.Select(card => card.Vendor).ToHashSet());

        return cards
            .OrderBy(card => card.Address, StringComparer.Ordinal)
            .Select(card =>
            {
                var pciId = $"{card.Vendor}:{card.Device}";
                var (chipset, name) = Split(ids.Device(pciId));
                return new GpuPayload(name ?? pciId, ids.Vendor(card.Vendor), chipset, card.Vram, Slot(card.Address), pciId);
            })
            .ToList();
    }

    // "0x10DE" → "10de", a grafia do pci.ids.
    private static string? Hex(string? value) =>
        value is { Length: 6 } && value.StartsWith("0x") ? value[2..].ToLowerInvariant() : null;

    // "0000:01:00.0" → "01:00.0", como o lspci escreve quando há um domínio só.
    private static string Slot(string address) => address.StartsWith("0000:") ? address[5..] : address;

    private static (string? Chipset, string? Name) Split(string? device)
    {
        if (device is null) return (null, null);
        var open = device.LastIndexOf(" [", StringComparison.Ordinal);
        return open > 0 && device.EndsWith(']')
            ? (device[..open], device[(open + 2)..^1])
            : (null, device);
    }
}

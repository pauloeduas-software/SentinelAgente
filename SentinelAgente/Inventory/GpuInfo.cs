using SentinelAgente.Protocol;

namespace SentinelAgente.Inventory;

/// <summary>
/// As placas de vídeo, como o GLPI Agent (<c>Generic/PCI/Videos.pm</c>): os
/// dispositivos PCI de vídeo, com o nome do <c>pci.ids</c> e a memória (A22, A24, A28). A
/// classe é a do <c>/sys/bus/pci/devices/&lt;slot&gt;/class</c>, <c>0x03</c> (VGA, 3D e
/// tela), em vez do nome que o lspci escreve, que é o que o GLPI compara.
/// </summary>
/// <remarks>
/// "Navi 22 [Radeon RX 6700 XT]" vira o nome "Radeon RX 6700 XT", a mesma divisão
/// do GLPI, e o chip fica de fora; sem colchete, o nome é o texto inteiro, e sem o
/// <c>pci.ids</c>, o par fabricante:dispositivo. A memória é a VRAM que o <c>amdgpu</c> mostra, e a
/// da NVIDIA vem do <see cref="NvidiaSmi"/> (A28): o GLPI manda o tamanho da janela do PCI,
/// que é 256 MB numa placa de 8 GB. A integrada, AMD ou Intel, vai sem memória: ela não tem
/// memória própria. A AMD integrada sem o <c>hwmon</c> (<c>amdgpu.dpm=0</c>) não se distingue
/// da dedicada, e vai com a reserva da BIOS (A28). A resolução, que o GLPI tira do
/// <c>xrandr</c>, não vai: o serviço não tem tela.
/// </remarks>
internal static class GpuInfo
{
    private const string Devices = "sys/bus/pci/devices";
    private const string Nvidia = "10de";

    /// <summary>
    /// As placas, na ordem do slot. Sem placa, a lista vazia: o servidor só apaga a
    /// placa que saiu com ela, e o campo ausente deixaria a velha na tela. Sem o
    /// <c>/sys/bus/pci/devices</c>, <c>null</c>: não há o que dizer. A memória da
    /// NVIDIA, pelo slot, só é pedida quando há uma placa dela.
    /// </summary>
    public static IReadOnlyList<GpuPayload>? Read(SystemRoot root, Func<IReadOnlyDictionary<string, long>> nvidiaMemory)
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
                && !Integrated(root, address)
                ? bytes
                : (long?)null;
            cards.Add((address, vendor, device, vram));
        }

        if (cards.Count == 0) return [];
        var ids = PciIds.Read(root, cards.Select(card => card.Vendor).ToHashSet());
        var nvidia = cards.Any(card => card.Vendor == Nvidia && card.Vram is null)
            ? nvidiaMemory()
            : new Dictionary<string, long>();

        return cards
            .OrderBy(card => card.Address, StringComparer.Ordinal)
            .Select(card =>
            {
                var pciId = $"{card.Vendor}:{card.Device}";
                var vram = card.Vram ?? (card.Vendor == Nvidia && nvidia.TryGetValue(card.Address, out var bytes) ? bytes : null);
                return new GpuPayload(Name(ids.Device(pciId)) ?? pciId, vram);
            })
            .ToList();
    }

    /// <summary>
    /// A AMD integrada: o <c>amdgpu</c> mostra nela, como VRAM, o pedaço da RAM que a BIOS
    /// reserva, e ela vai sem memória, como a Intel (A28). O sinal é o sensor <c>vddnb</c>,
    /// que o driver só cria na integrada (<c>hwmon_attributes_visible</c>, no <c>amdgpu_pm.c</c>).
    /// </summary>
    private static bool Integrated(SystemRoot root, string address)
    {
        var hwmon = $"{Devices}/{address}/hwmon";
        if (!root.HasDirectory(hwmon)) return false;
        return Directory.EnumerateDirectories(root.Combine(hwmon))
            .Any(sensor => root.ReadValue($"{hwmon}/{Path.GetFileName(sensor)}/in1_label") == "vddnb");
    }

    // "0x10DE" → "10de", a grafia do pci.ids.
    private static string? Hex(string? value) =>
        value is { Length: 6 } && value.StartsWith("0x") ? value[2..].ToLowerInvariant() : null;

    private static string? Name(string? device)
    {
        if (device is null) return null;
        var open = device.LastIndexOf(" [", StringComparison.Ordinal);
        return open > 0 && device.EndsWith(']') ? device[(open + 2)..^1] : device;
    }
}

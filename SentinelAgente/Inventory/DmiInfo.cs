using System.Text.RegularExpressions;
using SentinelAgente.Identity;

namespace SentinelAgente.Inventory;

internal sealed record DmiData(string? BiosSerial, string? SystemUuid, string? Manufacturer, string? HardwareModel);

/// <summary>
/// O que o firmware diz da máquina, pelo <c>/sys/class/dmi/id</c>. Valor de
/// fábrica vai nulo, e o SentinelWeb mantém o que já tinha.
/// </summary>
internal static partial class DmiInfo
{
    private const string DmiDirectory = "sys/class/dmi/id";

    /// <summary>
    /// Fabricante e modelo que a fábrica não preencheu: a lista do GLPI Agent
    /// (<c>isInvalidBiosValue</c>, em <c>lib/GLPI/Agent/Tools/Generic.pm</c>).
    /// </summary>
    private static readonly HashSet<string> FactoryNames =
    [
        "system manufacturer", "system product name", "system version", "chassis manufacturer",
        "chassis version", "to be filled by o.e.m.", "to be filled by oem", "default string",
        "not specified", "not applicable", "not available", "not present", "not installed",
        "n/a", "none", "unknown", "o.e.m.", "oem",
    ];

    public static DmiData Read(SystemRoot root)
    {
        var serial = Value(root, "product_serial");
        var uuid = Value(root, "product_uuid");
        var manufacturer = Name(root, "sys_vendor");

        return new DmiData(
            Hwid.IsRealSerial(serial) ? serial : null,
            Hwid.IsRealUuid(uuid) ? uuid : null,
            manufacturer,
            Model(root, manufacturer));
    }

    /// <summary>
    /// A Lenovo grava o código da peça no <c>product_name</c> ("20HRCTO1WW") e o
    /// nome no <c>product_version</c> ("ThinkPad X1 Carbon 5th"). A troca e o
    /// filtro são os do GLPI Agent (<c>Linux/Bios.pm</c>).
    /// </summary>
    private static string? Model(SystemRoot root, string? manufacturer)
    {
        if (string.Equals(manufacturer, "LENOVO", StringComparison.OrdinalIgnoreCase) &&
            Name(root, "product_version") is { } version && LenovoFamily().IsMatch(version))
            return version;

        return Name(root, "product_name");
    }

    private static string? Name(SystemRoot root, string attribute) =>
        Value(root, attribute) is { } value && !FactoryNames.Contains(value.ToLowerInvariant()) ? value : null;

    private static string? Value(SystemRoot root, string attribute) => root.ReadValue($"{DmiDirectory}/{attribute}");

    [GeneratedRegex("^(Think|Idea|Yoga|Netfinity|Netvista|Intelli)")]
    private static partial Regex LenovoFamily();
}

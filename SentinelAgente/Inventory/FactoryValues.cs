using System.Text.RegularExpressions;

namespace SentinelAgente.Inventory;

/// <summary>
/// O serial e o UUID que a fábrica deixou em branco, que o handshake manda
/// nulos: o SentinelWeb reconhece a máquina por eles (a CV de lá), e um valor
/// de fábrica casaria máquinas diferentes. São as listas do SentinelWeb
/// (<c>server/modules/reconciliation/helpers/normalize-identity.helper.ts</c>,
/// as decisões D46 e CT de lá): mudou lá, muda aqui.
/// </summary>
internal static partial class FactoryValues
{
    private static readonly HashSet<string> FactorySerials = new[]
    {
        "to be filled by o.e.m.", "to be filled by oem", "system serial number", "default string",
        "chassis serial number", "not applicable", "not specified", "none", "n/a", "na", "null",
        "unknown", "invalid", "o.e.m.", "oem", "0123456789", "123456789", "00000000",
        "default", "base board serial number", "baseboard serial number", "not available", "not present",
        "1234567890", "1234567",
    }.Select(Compact).ToHashSet();

    /// <summary>
    /// Zeros, só <c>f</c>, o da AMI, os dois que o osquery acrescenta
    /// (<c>kPlaceholderHardwareUUIDList</c>, em <c>osquery/core/system.cpp</c>) e
    /// os dois de fábrica na ordem de bytes que o kernel usa no SMBIOS anterior
    /// ao 2.6 (<c>dmi_save_uuid</c>, em <c>drivers/firmware/dmi_scan.c</c>).
    /// </summary>
    private static readonly HashSet<string> UselessUuids =
    [
        "00000000-0000-0000-0000-000000000000",
        "ffffffff-ffff-ffff-ffff-ffffffffffff",
        "03000200-0400-0500-0006-000700080009",
        "03020100-0504-0706-0809-0a0b0c0d0e0f",
        "10000000-0000-8000-0040-000000000000",
        "00020003-0004-0005-0006-000700080009",
        "00010203-0405-0607-0809-0a0b0c0d0e0f",
    ];

    /// <summary>
    /// Serial de verdade: sem os separadores, fora da lista, com 4 caracteres ou
    /// mais e sem ser um caractere repetido. Com os separadores, <c>0000-0000</c>
    /// passaria.
    /// </summary>
    public static bool IsRealSerial(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var compact = Compact(value);
        return !FactorySerials.Contains(compact) && compact.Length >= 4 && !RepeatedCharacter().IsMatch(compact);
    }

    /// <summary>UUID de verdade: no formato de UUID e fora da lista.</summary>
    public static bool IsRealUuid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var lower = value.Trim().ToLowerInvariant().Replace("{", "").Replace("}", "");
        return UuidFormat().IsMatch(lower) && !UselessUuids.Contains(lower);
    }

    /// <summary>Minúsculas, sem <c>-</c>, <c>:</c> e espaço: o mesmo <c>compactar</c> do SentinelWeb.</summary>
    private static string Compact(string value) =>
        new(value.ToLowerInvariant().Where(c => c is not ('-' or ':') && !char.IsWhiteSpace(c)).ToArray());

    [GeneratedRegex(@"^(.)\1+$")]
    private static partial Regex RepeatedCharacter();

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
    private static partial Regex UuidFormat();
}

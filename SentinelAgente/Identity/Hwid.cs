using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SentinelAgente.Identity;

/// <summary>A máquina não tem de onde tirar o HWID; a mensagem diz o que foi lido.</summary>
public sealed class HwidException(string message) : Exception(message);

/// <summary>
/// O HWID, a chave da máquina no SentinelWeb (A13): o SHA-256 do primeiro dado
/// do firmware que não for falso, nesta ordem — o serial do produto, o serial da
/// placa-mãe e o UUID. Sem nenhum, o agente não sobe: não inventa identidade,
/// porque número sorteado e <c>/etc/machine-id</c> mudam quando o sistema é
/// reinstalado.
/// </summary>
/// <remarks>
/// A ordem não muda e a fila só cresce no fim: mexer no começo dá outro HWID a
/// máquinas já cadastradas, e o servidor prende o token ao HWID.
/// </remarks>
public static partial class Hwid
{
    public const string DmiDirectory = "/sys/class/dmi/id";

    /// <summary>
    /// Os seriais que a fábrica deixou em branco. A mesma lista do SentinelWeb
    /// (<c>server/modules/reconciliation/helpers/normalize-identity.helper.ts</c>,
    /// a decisão D46 de lá): mudou lá, muda aqui.
    /// </summary>
    private static readonly HashSet<string> FactorySerials =
    [
        "to be filled by o.e.m.", "to be filled by oem", "system serial number", "default string",
        "chassis serial number", "not applicable", "not specified", "none", "n/a", "na", "null",
        "unknown", "invalid", "o.e.m.", "oem", "0123456789", "123456789", "00000000",
    ];

    /// <summary>
    /// UUIDs que não identificam nada: os três do SentinelWeb e os dois que o
    /// osquery acrescenta (<c>kPlaceholderHardwareUUIDList</c>, em
    /// <c>osquery/core/system.cpp</c>).
    /// </summary>
    private static readonly HashSet<string> UselessUuids =
    [
        "00000000-0000-0000-0000-000000000000",
        "ffffffff-ffff-ffff-ffff-ffffffffffff",
        "03000200-0400-0500-0006-000700080009",
        "03020100-0504-0706-0809-0a0b0c0d0e0f",
        "10000000-0000-8000-0040-000000000000",
    ];

    /// <summary>
    /// Calcula o HWID. Os três arquivos são só do root (<c>0400</c> no kernel):
    /// sem sudo, a leitura falha e o erro diz isso.
    /// </summary>
    public static string Read(string dmiDirectory = DmiDirectory)
    {
        var productSerial = ReadAttribute(dmiDirectory, "product_serial");
        if (IsRealSerial(productSerial))
            return Hash(productSerial!);

        var boardSerial = ReadAttribute(dmiDirectory, "board_serial");
        if (IsRealSerial(boardSerial))
            return Hash(boardSerial!);

        var uuid = ReadAttribute(dmiDirectory, "product_uuid");
        if (IsRealUuid(uuid))
            return Hash(uuid!);

        throw new HwidException(
            $"Nenhum dado do firmware identifica esta máquina: product_serial \"{productSerial}\", " +
            $"board_serial \"{boardSerial}\" e product_uuid \"{uuid}\" são valores de fábrica.");
    }

    /// <summary>
    /// A conta do agente antigo, que a A13 manteve: tira <c>-</c>, <c>:</c> e
    /// espaço, passa para minúsculas e devolve o SHA-256 em hexadecimal minúsculo.
    /// </summary>
    internal static string Hash(string value)
    {
        var normalized = value.Trim().Replace("-", "").Replace(":", "").Replace(" ", "").ToLowerInvariant();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    /// <summary>Serial de verdade: fora da lista, com 4 caracteres ou mais e sem ser um caractere repetido.</summary>
    internal static bool IsRealSerial(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var lower = value.Trim().ToLowerInvariant();
        return !FactorySerials.Contains(lower) && lower.Length >= 4 && !RepeatedCharacter().IsMatch(lower);
    }

    /// <summary>UUID de verdade: no formato de UUID e fora da lista.</summary>
    internal static bool IsRealUuid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var lower = value.Trim().ToLowerInvariant().Replace("{", "").Replace("}", "");
        return UuidFormat().IsMatch(lower) && !UselessUuids.Contains(lower);
    }

    /// <summary>O valor do arquivo, ou <c>null</c> quando o firmware não o tem.</summary>
    private static string? ReadAttribute(string dmiDirectory, string name)
    {
        var path = Path.Combine(dmiDirectory, name);
        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            throw new HwidException($"Sem permissão para ler \"{path}\": o agente precisa rodar como root (sudo).");
        }
    }

    [GeneratedRegex(@"^(.)\1+$")]
    private static partial Regex RepeatedCharacter();

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
    private static partial Regex UuidFormat();
}

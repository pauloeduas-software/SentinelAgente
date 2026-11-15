using System.Text.RegularExpressions;

namespace SentinelAgente.Inventory;

/// <summary>
/// O sistema como <c>NAME VERSION_ID</c> do os-release ("Ubuntu 24.04"), e não
/// o <c>PRETTY_NAME</c>: ele leva a versão pontual ("24.04.1 LTS"), e cada
/// atualização viraria uma mudança de hardware no SentinelWeb (A14).
/// </summary>
internal static partial class OsRelease
{
    public static string Read(SystemRoot root)
    {
        var fields = Parse(root.ReadText("etc/os-release") ?? root.ReadText("usr/lib/os-release"));
        // Sem NAME, o padrão do os-release é "Linux". Nunca nulo: o servidor
        // gravaria "desconhecido" e contaria uma mudança.
        var name = fields.GetValueOrDefault("NAME") ?? "Linux";
        var version = fields.GetValueOrDefault("VERSION_ID");

        // A distribuição contínua põe a data do snapshot na versão (o openSUSE
        // Tumbleweed: "20241115"), e cada atualização seria uma mudança.
        return version is null || SnapshotDate().IsMatch(version) ? name : $"{name} {version}";
    }

    /// <summary>As linhas <c>CHAVE=valor</c>, sem as aspas; comentário e valor vazio ficam de fora.</summary>
    internal static Dictionary<string, string> Parse(string? text)
    {
        var fields = new Dictionary<string, string>();
        foreach (var line in (text ?? "").Split('\n'))
        {
            var trimmed = line.Trim();
            var equals = trimmed.IndexOf('=');
            if (trimmed.StartsWith('#') || equals <= 0) continue;

            var value = trimmed[(equals + 1)..].Trim().Trim('"', '\'').Trim();
            if (value.Length > 0) fields[trimmed[..equals]] = value;
        }
        return fields;
    }

    [GeneratedRegex("^[0-9]{8}$")]
    private static partial Regex SnapshotDate();
}

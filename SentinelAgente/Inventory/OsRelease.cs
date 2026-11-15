namespace SentinelAgente.Inventory;

/// <summary>
/// O sistema como <c>NAME VERSION_ID</c> do os-release ("Ubuntu 24.04"), e não
/// o <c>PRETTY_NAME</c>: ele leva a versão pontual ("24.04.1 LTS"), e cada
/// atualização viraria uma mudança de hardware no SentinelWeb (A14).
/// </summary>
internal static class OsRelease
{
    public static string Read(SystemRoot root)
    {
        var fields = Parse(root.ReadText("etc/os-release") ?? root.ReadText("usr/lib/os-release"));
        var name = fields.GetValueOrDefault("NAME");
        var version = fields.GetValueOrDefault("VERSION_ID");

        // Sem NAME, o padrão do os-release é "Linux". Nunca nulo: o servidor
        // gravaria "desconhecido" e contaria uma mudança.
        if (name is null) return fields.GetValueOrDefault("PRETTY_NAME") ?? "Linux";
        return version is null ? name : $"{name} {version}";
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
}

namespace SentinelAgente.Inventory;

/// <summary>
/// O modelo do processador, o <c>model name</c> do <c>/proc/cpuinfo</c>. O ARM
/// não tem esse campo, e aí vai nulo: a máquina ARM sem DMI nem sobe (A13), e a
/// que tem é rara demais para uma tabela de peças (A14).
/// </summary>
internal static class CpuInfo
{
    public static string? Read(SystemRoot root)
    {
        foreach (var line in (root.ReadText("proc/cpuinfo") ?? "").Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0 || line[..colon].Trim() != "model name") continue;

            // O Xeon antigo alinha o nome com espaços: "CPU           E5-2670".
            var model = string.Join(' ', line[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return model.Length > 0 ? model : null;
        }
        return null;
    }
}

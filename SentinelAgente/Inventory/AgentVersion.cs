using System.Reflection;

namespace SentinelAgente.Inventory;

/// <summary>
/// A versão deste agente, a do pacote (<c>&lt;Version&gt;</c> do csproj), que o
/// handshake leva para o SentinelWeb achar quem ficou com o agente velho (A25).
/// </summary>
internal static class AgentVersion
{
    public static string Current { get; } = Read(typeof(AgentVersion).Assembly);

    /// <summary>
    /// A versão informativa sem o <c>+commit</c> que o SDK acrescenta: a lista de
    /// Agentes do SentinelWeb mostra a versão como chegou, e cada build da mesma
    /// versão pareceria outra.
    /// </summary>
    internal static string Read(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational)) return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..plus];
    }
}

using SentinelAgente.Protocol;

namespace SentinelAgente.Software;

/// <summary>
/// Os programas instalados, que o handshake leva (A10, A22): os dois gerenciadores
/// que estiverem na máquina, como o GLPI Agent, que roda cada módulo cujo
/// programa existe. O Ubuntu com o <c>rpm</c> instalado devolve a lista dele vazia.
/// </summary>
/// <remarks>
/// O gerenciador que falha vai para o log e não leva o outro junto. Sem nenhum
/// dos dois, ou com os dois falhando, a lista vai nula, e o campo não vai para o
/// fio. A ordem é a do nome, para a mesma máquina dar a mesma lista.
/// </remarks>
internal sealed partial class InstalledSoftware(IPackageQuery query, ILogger<InstalledSoftware> logger)
{
    private static readonly (string Program, string[] Arguments, Func<string, IEnumerable<SoftwarePayload>> Parse)[] Managers =
    [
        (Dpkg.Program, Dpkg.Arguments, Dpkg.Parse),
        (Rpm.Program, Rpm.Arguments, Rpm.Parse),
    ];

    public async Task<IReadOnlyList<SoftwarePayload>?> ReadAsync(CancellationToken cancellationToken)
    {
        List<SoftwarePayload>? packages = null;
        foreach (var manager in Managers)
        {
            try
            {
                if (await query.RunAsync(manager.Program, manager.Arguments, cancellationToken) is not { } output) continue;
                (packages ??= []).AddRange(manager.Parse(output));
            }
            catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                LogManagerFailed(e, manager.Program);
            }
        }

        if (packages is null)
        {
            LogNoManager();
            return null;
        }

        return packages
            .OrderBy(package => package.Name, StringComparer.Ordinal)
            .ThenBy(package => package.Arch, StringComparer.Ordinal)
            .ToList();
    }

    [LoggerMessage(LogLevel.Warning, "A lista de programas pelo {Program} falhou; o handshake vai sem ela.")]
    private partial void LogManagerFailed(Exception exception, string program);

    [LoggerMessage(LogLevel.Warning, "Nenhum gerenciador de pacotes respondeu (dpkg-query, rpm): o handshake vai sem a lista de programas.")]
    private partial void LogNoManager();
}

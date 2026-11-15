using System.Collections.Frozen;
using SentinelAgente.Connection;
using SentinelAgente.Protocol;

namespace SentinelAgente.Commands;

/// <summary>
/// A lista fechada (A5): a ação do servidor só escolhe uma linha da tabela, e
/// nada do texto dela vira argumento. Fora da tabela, o comando é recusado e
/// vai para o log.
/// </summary>
/// <remarks>
/// O <c>systemctl</c> pede a ação ao logind e volta assim que ela entra na fila,
/// sem esperar a máquina desligar (A18). Ele respeita os bloqueios do logind: a
/// recusa, como a da máquina que não suspende, vai para o log com o motivo.
/// </remarks>
internal sealed partial class PowerCommands(
    IProgramRunner runner,
    TimeSpan timeout,
    ILogger<PowerCommands> logger) : ICommandHandler
{
    internal const string Systemctl = "/usr/bin/systemctl";

    /// <summary>O tempo do systemctl pôr a ação na fila; ele costuma voltar em menos de um segundo.</summary>
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private static readonly FrozenDictionary<string, string> Verbs = new Dictionary<string, string>
    {
        ["SHUTDOWN"] = "poweroff",
        ["REBOOT"] = "reboot",
        ["SUSPEND"] = "suspend",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public Task HandleAsync(CommandPayload command, CancellationToken cancellationToken)
    {
        if (!Verbs.TryGetValue(command.Action, out var verb))
        {
            LogRefused(command.Action, command.CommandId);
            return Task.CompletedTask;
        }

        // Dispara e volta: a recepção fica parada enquanto este método não volta
        // (ICommandHandler). E o fim da conexão não interrompe o systemctl: a
        // ação já foi pedida.
        _ = Task.Run(() => ExecuteAsync(command, verb));
        return Task.CompletedTask;
    }

    /// <summary>Roda o systemctl e diz no log como terminou. Não lança.</summary>
    internal async Task ExecuteAsync(CommandPayload command, string verb)
    {
        using var deadline = new CancellationTokenSource(timeout);
        try
        {
            var result = await runner.RunAsync(Systemctl, [verb], deadline.Token);
            if (result.ExitCode == 0) LogAccepted(command.Action, command.CommandId);
            else LogRejected(command.Action, command.CommandId, result.ExitCode, result.Error);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            LogTimedOut(command.Action, command.CommandId, timeout);
        }
        catch (Exception e)
        {
            LogFailed(e, command.Action, command.CommandId);
        }
    }

    [LoggerMessage(LogLevel.Warning, "Comando {Action} recusado ({CommandId}): só SHUTDOWN, REBOOT e SUSPEND são aceitos.")]
    private partial void LogRefused(string action, string commandId);

    [LoggerMessage(LogLevel.Information, "Comando {Action} ({CommandId}) aceito pelo systemd.")]
    private partial void LogAccepted(string action, string commandId);

    [LoggerMessage(LogLevel.Error, "O systemd recusou o comando {Action} ({CommandId}), código {ExitCode}: {Error}")]
    private partial void LogRejected(string action, string commandId, int exitCode, string error);

    [LoggerMessage(LogLevel.Error, "O comando {Action} ({CommandId}) não respondeu em {Timeout} e foi interrompido.")]
    private partial void LogTimedOut(string action, string commandId, TimeSpan timeout);

    [LoggerMessage(LogLevel.Error, "O comando {Action} ({CommandId}) não conseguiu rodar o systemctl.")]
    private partial void LogFailed(Exception exception, string action, string commandId);
}

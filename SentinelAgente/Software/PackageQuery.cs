using System.Diagnostics;

namespace SentinelAgente.Software;

/// <summary>Roda o programa que lista os pacotes e devolve o que ele escreveu; nos testes, uma gravação.</summary>
internal interface IPackageQuery
{
    /// <summary>
    /// A saída do programa, ou <c>null</c> quando ele não está instalado. Saída
    /// com erro, ou que passa do tempo, lança, com o stderr na mensagem.
    /// </summary>
    Task<string?> RunAsync(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

/// <summary>
/// O <see cref="Process"/> sem shell, como o do <c>Commands</c>, mas com a saída:
/// o executável é um caminho fixo, e cada argumento vai separado.
/// </summary>
internal sealed class ProcessPackageQuery : IPackageQuery
{
    /// <summary>O <c>rpm -qa</c> leva alguns segundos numa máquina cheia; travado, o handshake não espera mais que isto.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);

    public async Task<string?> RunAsync(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (!File.Exists(program)) return null;

        var start = new ProcessStartInfo(program)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        // O resumo e o grupo do rpm saem traduzidos com o idioma do sistema.
        start.Environment["LC_ALL"] = "C.UTF-8";

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"{program} não iniciou.");

        // Os dois lidos junto com a espera: o cano cheio trava o processo.
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"{program} passou de {Timeout.TotalSeconds:0} s.");
        }

        var text = await output;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{program} saiu com {process.ExitCode}: {(await error).Trim()}");
        return text;
    }
}

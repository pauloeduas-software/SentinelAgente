using System.Diagnostics;

namespace SentinelAgente.Commands;

/// <summary>Como o programa terminou: o código de saída e o que ele disse no stderr.</summary>
internal readonly record struct ProgramResult(int ExitCode, string Error);

/// <summary>Roda um programa com os argumentos em lista; nos testes, uma gravação.</summary>
internal interface IProgramRunner
{
    /// <summary>Espera o programa terminar. Cancelado, mata o processo e lança.</summary>
    Task<ProgramResult> RunAsync(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

/// <summary>
/// O <see cref="Process"/> sem shell: o executável é chamado direto, e cada
/// argumento vai separado, sem nada que o shell interprete.
/// </summary>
internal sealed class ProcessRunner : IProgramRunner
{
    public async Task<ProgramResult> RunAsync(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(program)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"{program} não iniciou.");

        // Os dois lidos junto com a espera: o cano cheio trava o processo.
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        await output;
        return new ProgramResult(process.ExitCode, (await error).Trim());
    }
}

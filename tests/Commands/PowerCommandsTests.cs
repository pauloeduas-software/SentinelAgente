using System.Collections.Concurrent;
using SentinelAgente.Commands;
using SentinelAgente.Protocol;
using SentinelAgente.Tests.Connection;

namespace SentinelAgente.Tests.Commands;

public sealed class PowerCommandsTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly ListLogger<PowerCommands> _log = new();

    private sealed class FakeRunner(Func<CancellationToken, Task<ProgramResult>>? run = null) : IProgramRunner
    {
        public ConcurrentQueue<string> Calls { get; } = new();
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ProgramResult> RunAsync(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Calls.Enqueue(string.Join(' ', [program, .. arguments]));
            Called.TrySetResult();
            return run is null ? new ProgramResult(0, "") : await run(cancellationToken);
        }
    }

    private PowerCommands Create(FakeRunner runner, TimeSpan? timeout = null) =>
        new(runner, timeout ?? PowerCommands.DefaultTimeout, _log);

    [Theory]
    [InlineData("SHUTDOWN", "/usr/bin/systemctl poweroff")]
    [InlineData("REBOOT", "/usr/bin/systemctl reboot")]
    [InlineData("SUSPEND", "/usr/bin/systemctl suspend")]
    public async Task CadaAcaoChamaOSystemctlSemShell(string action, string expected)
    {
        var runner = new FakeRunner();

        await Create(runner).HandleAsync(new CommandPayload(action, "c-1"), TestContext.Current.CancellationToken);
        await runner.Called.Task.WaitAsync(Wait, TestContext.Current.CancellationToken);

        Assert.Equal([expected], runner.Calls);
    }

    [Theory]
    [InlineData("reboot")]
    [InlineData("REBOOT; rm -rf /")]
    [InlineData("RUN")]
    [InlineData("")]
    public async Task AcaoForaDaListaERecusadaENaoRodaNada(string action)
    {
        var runner = new FakeRunner();

        await Create(runner).HandleAsync(new CommandPayload(action, "c-2"), TestContext.Current.CancellationToken);

        Assert.Empty(runner.Calls);
        Assert.Contains(_log.Lines, line => line.Contains("recusado") && line.Contains("c-2"));
    }

    [Fact]
    public async Task VoltaSemEsperarOSystemctl()
    {
        var never = new TaskCompletionSource<ProgramResult>();
        var runner = new FakeRunner(_ => never.Task);

        var handled = Create(runner).HandleAsync(new CommandPayload("REBOOT", "c-3"), TestContext.Current.CancellationToken);

        Assert.True(handled.IsCompleted);
        await runner.Called.Task.WaitAsync(Wait, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OAceiteVaiParaOLog()
    {
        await Create(new FakeRunner()).ExecuteAsync(new CommandPayload("SHUTDOWN", "c-4"), "poweroff");

        Assert.Contains(_log.Lines, line => line.Contains("aceito") && line.Contains("c-4"));
    }

    [Fact]
    public async Task ARecusaDoSystemdVaiParaOLogComOMotivo()
    {
        var runner = new FakeRunner(_ => Task.FromResult(new ProgramResult(1,
            "Call to Suspend failed: Sleep verb 'suspend' is not configured or configuration is not supported by kernel")));

        await Create(runner).ExecuteAsync(new CommandPayload("SUSPEND", "c-5"), "suspend");

        var line = Assert.Single(_log.Lines);
        Assert.Contains("c-5", line);
        Assert.Contains("código 1", line);
        Assert.Contains("not supported by kernel", line);
    }

    [Fact]
    public async Task OSystemctlQueNaoVoltaEInterrompido()
    {
        var runner = new FakeRunner(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new ProgramResult(0, "");
        });

        await Create(runner, TimeSpan.FromMilliseconds(50)).ExecuteAsync(new CommandPayload("REBOOT", "c-6"), "reboot");

        Assert.Contains(_log.Lines, line => line.Contains("não voltou") && line.Contains("c-6"));
    }

    [Fact]
    public async Task OSystemctlQueNaoRodaVaiParaOLogSemLancar()
    {
        var runner = new FakeRunner(_ => throw new System.ComponentModel.Win32Exception(2, "No such file or directory"));

        await Create(runner).ExecuteAsync(new CommandPayload("REBOOT", "c-7"), "reboot");

        Assert.Contains(_log.Lines, line => line.Contains("não conseguiu rodar") && line.Contains("c-7"));
    }
}

public sealed class ProcessRunnerTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    // O sh aqui é só o programa de teste: o agente chama o systemctl direto.
    [Fact]
    public async Task DevolveOCodigoEOStderr()
    {
        var result = await new ProcessRunner().RunAsync("/bin/sh", ["-c", "echo saída; echo motivo >&2; exit 3"],
            TestContext.Current.CancellationToken);

        Assert.Equal(new ProgramResult(3, "motivo"), result);
    }

    [Fact]
    public async Task OsArgumentosVaoSemPassarPeloShell()
    {
        var result = await new ProcessRunner().RunAsync("/bin/sh", ["-c", "printf %s \"$1\" >&2", "sh", "a; b $(c)"],
            TestContext.Current.CancellationToken);

        Assert.Equal("a; b $(c)", result.Error);
    }

    [Fact]
    public async Task CanceladoMataOProcesso()
    {
        var directory = Directory.CreateTempSubdirectory("sentinel-comando-");
        try
        {
            // O shell grava o próprio PID e vira o sleep: o PID é o do processo que tem de morrer.
            var pidFile = Path.Combine(directory.FullName, "pid");
            using var cancel = new CancellationTokenSource();
            var run = new ProcessRunner().RunAsync("/bin/sh", ["-c", "echo $$ > \"$0\"; exec sleep 30", pidFile], cancel.Token);
            Assert.True(await EventuallyAsync(() => File.Exists(pidFile) && File.ReadAllText(pidFile).EndsWith('\n')));
            var pid = File.ReadAllText(pidFile).Trim();

            cancel.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.True(await EventuallyAsync(() => Dead(pid)));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    // Morto é o que sumiu do /proc ou virou zumbi à espera de quem o colha.
    private static bool Dead(string pid)
    {
        try
        {
            var stat = File.ReadAllText($"/proc/{pid}/stat");
            return stat[stat.LastIndexOf(')') + 2] == 'Z';
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static async Task<bool> EventuallyAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) return false;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        return true;
    }

    [Fact]
    public async Task OFilhoNaoHerdaOSocketDeAvisoDoSystemd()
    {
        var anterior = Environment.GetEnvironmentVariable("NOTIFY_SOCKET");
        Environment.SetEnvironmentVariable("NOTIFY_SOCKET", "/run/systemd/notify");
        try
        {
            var result = await new ProcessRunner().RunAsync("/bin/sh", ["-c", "printf %s \"${NOTIFY_SOCKET-ausente}\" >&2"],
                TestContext.Current.CancellationToken);

            Assert.Equal("ausente", result.Error);
            Assert.Equal("/run/systemd/notify", Environment.GetEnvironmentVariable("NOTIFY_SOCKET"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOTIFY_SOCKET", anterior);
        }
    }
}

using System.Text.Json;
using System.Threading.Channels;
using SentinelAgente.Connection;
using SentinelAgente.Protocol;
using SentinelAgente.Telemetry;
using SentinelAgente.Tests.Connection;

namespace SentinelAgente.Tests.Telemetry;

// O código de saída é do processo inteiro: os testes que mexem nele não rodam juntos.
[Collection("Código de saída")]
public sealed class TelemetryServiceTests : IAsyncDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private static readonly TelemetryTimings Fast = new()
    {
        Interval = TimeSpan.FromMilliseconds(100),
        FirstWindow = TimeSpan.FromMilliseconds(20),
        ConnectedPoll = TimeSpan.FromMilliseconds(10),
    };

    private readonly FakeLink _link = new();
    private readonly ListLogger<TelemetryService> _log = new();
    private TelemetryService? _service;

    private sealed class FakeLink : IServerLink
    {
        public volatile bool Connected;
        public volatile bool Accepts = true;
        public volatile bool Throws;
        public Channel<string> Sent { get; } = Channel.CreateUnbounded<string>();

        public bool IsConnected => Connected;

        private TaskCompletionSource _refresh = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RefreshRequested => Volatile.Read(ref _refresh).Task;

        /// <summary>O que a conexão faz quando chega o <c>REFRESH</c>.</summary>
        public void RequestRefresh() =>
            Interlocked.Exchange(ref _refresh, new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();

        public Task<bool> TrySendAsync(OutgoingMessage message, CancellationToken cancellationToken)
        {
            if (Throws) throw new InvalidOperationException("envio quebrado");
            if (!Accepts) return Task.FromResult(false);
            Sent.Writer.TryWrite(System.Text.Encoding.UTF8.GetString(message.Json.Span));
            return Task.FromResult(true);
        }
    }

    // Conta as leituras: a primeira é a de partida, que não vai para o fio.
    private sealed class CountingCpu : ITelemetryCollector
    {
        public int Reads;

        public void Collect(TelemetrySample sample) => sample.CpuUsagePercentage = Interlocked.Increment(ref Reads);
    }

    private sealed class Broken : ITelemetryCollector
    {
        public void Collect(TelemetrySample sample) => throw new IOException("sem /proc");
    }

    private sealed class Memory : ITelemetryCollector
    {
        public void Collect(TelemetrySample sample) => sample.RamTotalBytes = 1024;
    }

    private Task StartAsync(params ITelemetryCollector[] collectors) => StartAsync(() => "abc123", collectors);

    private async Task StartAsync(Func<string> hwid, params ITelemetryCollector[] collectors)
    {
        _service = new TelemetryService(_link, collectors, Fast, hwid, _log);
        await _service.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Espera_a_conexao_e_manda_a_amostra_com_o_hwid()
    {
        var cpu = new CountingCpu();
        await StartAsync(cpu);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(0, cpu.Reads);

        _link.Connected = true;
        using var first = JsonDocument.Parse(await ReadAsync());

        Assert.Equal("Telemetry", first.RootElement.GetProperty("Type").GetString());
        var payload = first.RootElement.GetProperty("Payload");
        Assert.Equal("abc123", payload.GetProperty("Hwid").GetString());
        // A leitura 1 foi a de partida; a amostra é a 2.
        Assert.Equal(2, payload.GetProperty("CpuUsagePercentage").GetDouble());
    }

    [Fact]
    public async Task O_pedido_do_servidor_manda_a_amostra_na_hora_e_o_seguinte_tambem()
    {
        _link.Connected = true;
        // A hora de verdade e a janela longa: sem o pedido, nada iria no tempo do teste.
        _service = new TelemetryService(
            _link, [new CountingCpu()], Fast with { Interval = TimeSpan.FromHours(1), FirstWindow = TimeSpan.FromHours(1) },
            () => "abc123", _log);
        await _service.StartAsync(CancellationToken.None);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // O pedido na janela de partida manda a primeira amostra.
        _link.RequestRefresh();
        Assert.StartsWith("""{"Type":"Telemetry""", await ReadAsync());

        // E o seguinte, já no relógio da hora.
        _link.RequestRefresh();
        Assert.StartsWith("""{"Type":"Telemetry""", await ReadAsync());
        Assert.False(_link.Sent.Reader.TryRead(out _));
    }

    [Fact]
    public async Task O_pedido_antes_de_a_conexao_ser_notada_nao_se_perde()
    {
        // A conexão notada só depois do pedido, e a janela de partida longa.
        _service = new TelemetryService(
            _link, [new CountingCpu()], Fast with { Interval = TimeSpan.FromHours(1), FirstWindow = TimeSpan.FromHours(1) },
            () => "abc123", _log);
        await _service.StartAsync(CancellationToken.None);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        _link.RequestRefresh();
        _link.Connected = true;

        Assert.StartsWith("""{"Type":"Telemetry""", await ReadAsync());
    }

    [Fact]
    public async Task Sem_conexao_a_amostra_e_descartada_e_a_seguinte_vai()
    {
        _link.Connected = true;
        _link.Accepts = false;
        await StartAsync(new CountingCpu());

        Assert.True(await EventuallyAsync(() => _log.Lines.Any(line => line.Contains("descartada"))));
        _link.Accepts = true;

        Assert.StartsWith("""{"Type":"Telemetry""", await ReadAsync());
    }

    [Fact]
    public async Task Envio_que_lanca_nao_para_as_amostras_seguintes()
    {
        _link.Connected = true;
        _link.Throws = true;
        await StartAsync(new CountingCpu());

        Assert.True(await EventuallyAsync(() => _log.Lines.Any(line => line.Contains("A amostra de telemetria falhou"))));
        _link.Throws = false;

        Assert.StartsWith("""{"Type":"Telemetry""", await ReadAsync());
    }

    [Fact]
    public async Task Coletor_que_falha_nao_impede_os_outros()
    {
        _link.Connected = true;
        await StartAsync(new Broken(), new Memory());

        using var first = JsonDocument.Parse(await ReadAsync());

        Assert.Equal(1024, first.RootElement.GetProperty("Payload").GetProperty("RamTotalBytes").GetInt64());
        Assert.Contains(_log.Lines, line => line.Contains("Broken"));
    }

    [Fact]
    public async Task Erro_fora_da_amostra_para_o_servico_com_erro()
    {
        _link.Connected = true;
        try
        {
            await StartAsync(() => throw new InvalidOperationException("sem HWID"), new CountingCpu());

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.ExecuteTask!);
            Assert.Equal(1, Environment.ExitCode);
            Assert.Contains(_log.Lines, line => line.Contains("A telemetria parou"));
        }
        finally
        {
            Environment.ExitCode = 0;
        }
    }

    private async Task<string> ReadAsync()
    {
        using var timeout = new CancellationTokenSource(Wait);
        return await _link.Sent.Reader.ReadAsync(timeout.Token);
    }

    private static async Task<bool> EventuallyAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(10);
        }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_service is null) return;
        await _service.StopAsync(CancellationToken.None);
        _service.Dispose();
    }
}

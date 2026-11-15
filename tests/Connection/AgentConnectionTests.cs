using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using SentinelAgente.Config;
using SentinelAgente.Connection;
using SentinelAgente.Protocol;

namespace SentinelAgente.Tests.Connection;

public sealed class AgentConnectionTests : IAsyncLifetime
{
    private const string Token = "token-da-maquina";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly string _directory = Directory.CreateTempSubdirectory("sentinel-conexao-").FullName;
    private readonly RecordingCommands _commands = new();
    private readonly ListLogger<AgentConnection> _log = new();
    private readonly List<AgentConnection> _started = [];
    private FakeAgentHub? _hub;

    // Os tempos de verdade em milissegundos: o teste não espera 5 s por uma reconexão.
    private static readonly ConnectionTimings Fast = new()
    {
        BackoffInitial = TimeSpan.FromMilliseconds(50),
        BackoffMax = TimeSpan.FromMilliseconds(200),
        ConnectTimeout = TimeSpan.FromSeconds(5),
        CloseTimeout = TimeSpan.FromSeconds(2),
    };

    private string TokenFile => Path.Combine(_directory, "estado", "token");
    private string EnrollSecretFile => Path.Combine(_directory, "enroll-secret");

    [Fact]
    public async Task Abre_com_o_token_e_manda_o_handshake_primeiro()
    {
        _hub = await StartHubAsync();
        var connection = await StartAsync(_hub.Url);

        using var first = JsonDocument.Parse(await ReadAsync(_hub.Received));

        Assert.Equal("Handshake", first.RootElement.GetProperty("Type").GetString());
        var payload = first.RootElement.GetProperty("Payload");
        Assert.Equal("abc123", payload.GetProperty("Hwid").GetString());
        Assert.Equal("pc-teste", payload.GetProperty("Hostname").GetString());
        Assert.False(payload.TryGetProperty("CpuModel", out _), "campo nulo não vai para o fio");
        Assert.True(await EventuallyAsync(() => connection.IsConnected));
    }

    [Fact]
    public async Task Cadastra_com_o_segredo_grava_o_token_e_apaga_o_segredo()
    {
        _hub = await StartHubAsync(registerToken: false);
        WriteSecret(EnrollSecretFile, FakeAgentHub.EnrollSecret);

        await StartAsync(_hub.Url, token: null);

        Assert.Equal("abc123", await ReadAsync(_hub.Enrolled));
        using var first = JsonDocument.Parse(await ReadAsync(_hub.Received));
        Assert.Equal("Handshake", first.RootElement.GetProperty("Type").GetString());

        Assert.False(File.Exists(EnrollSecretFile), "o segredo da frota não fica na máquina");
        Assert.Equal(OwnerOnly, File.GetUnixFileMode(TokenFile));
        Assert.True(_hub.Tokens.ContainsKey(File.ReadAllText(TokenFile)));
    }

    [Fact]
    public async Task Segredo_recusado_nao_conecta_e_o_segredo_fica()
    {
        _hub = await StartHubAsync(registerToken: false);
        WriteSecret(EnrollSecretFile, "segredo-errado");

        var connection = await StartAsync(_hub.Url, token: null);

        Assert.True(await EventuallyAsync(() => _log.Lines.Any(line => line.Contains("recusou o segredo de instalação (401)"))));
        Assert.False(connection.IsConnected);
        Assert.True(File.Exists(EnrollSecretFile));
        Assert.False(File.Exists(TokenFile));
    }

    [Fact]
    public async Task Cadastro_recusado_com_a_maquina_ja_conectada()
    {
        _hub = await StartHubAsync(registerToken: false);
        _hub.EnrollStatus = 409;
        WriteSecret(EnrollSecretFile, FakeAgentHub.EnrollSecret);

        var connection = await StartAsync(_hub.Url, token: null);

        Assert.True(await EventuallyAsync(() => _log.Lines.Any(line => line.Contains("(409)"))));
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task Token_revogado_leva_401_e_o_log_diz_como_voltar()
    {
        _hub = await StartHubAsync(registerToken: false);
        var connection = await StartAsync(_hub.Url);

        Assert.True(await EventuallyAsync(() => _hub.Refused >= 1));
        Assert.True(await EventuallyAsync(() => _log.Lines.Any(line =>
            line.Contains("recusou o token desta máquina (401)") && line.Contains(EnrollSecretFile))));
        Assert.False(connection.IsConnected);
        Assert.False(_hub.Received.Reader.TryRead(out _));
        Assert.False(await connection.TrySendAsync(Messages.CreatePing("abc123"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Entrega_o_comando_do_servidor()
    {
        _hub = await StartHubAsync();
        await StartAsync(_hub.Url);
        var server = await ReadAsync(_hub.Accepted);
        await ReadAsync(_hub.Received);

        await SendTextAsync(server, """{"Type":"Command","Payload":{"Action":"REBOOT","CommandId":"6f1c"}}""");

        var command = await ReadAsync(_commands.Received);
        Assert.Equal(new CommandPayload("REBOOT", "6f1c"), command);
    }

    [Fact]
    public async Task Reconecta_e_apresenta_de_novo_quando_o_servidor_fecha()
    {
        _hub = await StartHubAsync();
        await StartAsync(_hub.Url);
        var server = await ReadAsync(_hub.Accepted);
        await ReadAsync(_hub.Received);

        // O SentinelWeb encerrando fecha com 1001, "Servidor encerrando".
        await server.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "Servidor encerrando", TestContext.Current.CancellationToken);

        await ReadAsync(_hub.Accepted);
        using var again = JsonDocument.Parse(await ReadAsync(_hub.Received));
        Assert.Equal("Handshake", again.RootElement.GetProperty("Type").GetString());
    }

    [Fact]
    public async Task Manda_ping_quando_fica_parado()
    {
        _hub = await StartHubAsync();
        await StartAsync(_hub.Url, Fast with { PingIntervalMin = TimeSpan.FromMilliseconds(100), PingIntervalMax = TimeSpan.FromMilliseconds(200) });
        await ReadAsync(_hub.Received);

        using var ping = JsonDocument.Parse(await ReadAsync(_hub.Received));

        Assert.Equal("Ping", ping.RootElement.GetProperty("Type").GetString());
        Assert.Equal("abc123", ping.RootElement.GetProperty("Payload").GetProperty("Hwid").GetString());
    }

    [Fact]
    public async Task Parar_o_servico_fecha_a_conexao_normalmente()
    {
        _hub = await StartHubAsync();
        var connection = await StartAsync(_hub.Url);
        await ReadAsync(_hub.Received);

        await connection.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(WebSocketCloseStatus.NormalClosure, await ReadAsync(_hub.Closed));
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public async Task Com_wss_confia_so_na_CA_configurada_no_cadastro_e_na_conexao()
    {
        using var certificates = new TestCertificates();
        using var serverCertificate = certificates.IssueServer();
        _hub = await StartHubAsync(serverCertificate, registerToken: false);
        WriteSecret(EnrollSecretFile, FakeAgentHub.EnrollSecret);

        // Sem o CaFile, a CA de teste não está entre as do sistema: o cadastro nem sai.
        var withoutCa = await StartAsync(_hub.Url, token: null);
        Assert.True(await EventuallyAsync(() => _log.Lines.Any(line => line.Contains("Falha no cadastro"))));
        Assert.False(withoutCa.IsConnected);
        Assert.False(_hub.Enrolled.Reader.TryRead(out _));
        await withoutCa.StopAsync(TestContext.Current.CancellationToken);

        await StartAsync(_hub.Url, token: null, caFile: certificates.WriteAuthorityPem(_directory));
        Assert.Equal("abc123", await ReadAsync(_hub.Enrolled));
        using var first = JsonDocument.Parse(await ReadAsync(_hub.Received));
        Assert.Equal("Handshake", first.RootElement.GetProperty("Type").GetString());
    }

    private async Task<FakeAgentHub> StartHubAsync(
        System.Security.Cryptography.X509Certificates.X509Certificate2? certificate = null, bool registerToken = true)
    {
        var hub = await FakeAgentHub.StartAsync(certificate);
        if (registerToken) hub.Tokens[Token] = "abc123";
        return hub;
    }

    private async Task<AgentConnection> StartAsync(
        Uri url, ConnectionTimings? timings = null, string? token = Token, string? caFile = null)
    {
        if (token is not null && !File.Exists(TokenFile)) SecretFile.Write(TokenFile, token);

        var options = Options.Create(new ServerOptions
        {
            Url = url, TokenFile = TokenFile, EnrollSecretFile = EnrollSecretFile, CaFile = caFile,
        });
        var connection = new AgentConnection(options, new FixedHandshake(), _commands, timings ?? Fast, _log);
        _started.Add(connection);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static void WriteSecret(string path, string content)
    {
        File.WriteAllText(path, content + "\n");
        File.SetUnixFileMode(path, OwnerOnly);
    }

    private static async Task<T> ReadAsync<T>(Channel<T> channel)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Wait);
        return await channel.Reader.ReadAsync(timeout.Token);
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

    private static Task SendTextAsync(WebSocket socket, string text) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, TestContext.Current.CancellationToken);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _started)
        {
            await connection.StopAsync(CancellationToken.None);
            connection.Dispose();
        }
        if (_hub is not null) await _hub.DisposeAsync();
        Directory.Delete(_directory, recursive: true);
    }

    private sealed class FixedHandshake : IHandshakeSource
    {
        public ValueTask<HandshakePayload> CreateAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new HandshakePayload { Hwid = "abc123", Hostname = "pc-teste", OsVersion = "Ubuntu 24.04" });
    }

    private sealed class RecordingCommands : ICommandHandler
    {
        public Channel<CommandPayload> Received { get; } = Channel.CreateUnbounded<CommandPayload>();

        public Task HandleAsync(CommandPayload command, CancellationToken cancellationToken)
        {
            Received.Writer.TryWrite(command);
            return Task.CompletedTask;
        }
    }
}

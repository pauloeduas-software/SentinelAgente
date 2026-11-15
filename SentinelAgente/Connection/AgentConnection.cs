using System.Net;
using System.Net.WebSockets;
using Microsoft.Extensions.Options;
using SentinelAgente.Config;
using SentinelAgente.Protocol;

namespace SentinelAgente.Connection;

/// <summary>
/// O WebSocket com o <c>/agent-hub</c>. Cada volta do laço é uma conexão: abre
/// com o token, apresenta a máquina, escuta até cair, espera e tenta de novo.
/// Não desiste nunca: máquina que não conecta não tem como avisar que caiu.
/// </summary>
public sealed partial class AgentConnection(
    IOptions<ServerOptions> options,
    IHandshakeSource handshake,
    ICommandHandler commands,
    ConnectionTimings timings,
    ILogger<AgentConnection> logger) : BackgroundService, IServerLink
{
    // Um comando tem uns 100 bytes; mensagem maior que isto derruba a conexão
    // antes de ocupar memória.
    private const int MaxIncomingBytes = 64 * 1024;

    private readonly ServerOptions _options = options.Value;
    private ServerCertificate? _certificate;
    private Enrollment? _enrollment;
    private volatile Session? _session;

    private enum Outcome { Served, Failed, Unauthorized }

    public bool IsConnected => _session is not null;

    public async Task<bool> TrySendAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        var session = _session;
        if (session is null) return false;

        try
        {
            await session.SendAsync(message, cancellationToken);
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // A conexão caiu no meio do envio: o fim da sessão cancela o envio
            // preso, e o socket pode dar qualquer erro. Para quem chamou é tudo
            // "não chegou"; o laço de recepção vai perceber e reconectar.
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var url = _options.Url!;
        _certificate = _options.CaFile is { } caFile ? ServerCertificate.FromPemFile(caFile) : null;
        _enrollment = new Enrollment(url, _certificate, timings.ConnectTimeout);
        var backoff = new Backoff(timings.BackoffInitial, timings.BackoffMax);

        try
        {
            // A apresentação montada uma vez antes da primeira conexão: o HWID que
            // não abre para aqui, com o motivo no journal, e não depois de o
            // servidor responder (A20).
            await handshake.CreateAsync(stoppingToken);

            while (true)
            {
                var outcome = await ConnectAndServeAsync(url, stoppingToken);

                if (outcome == Outcome.Served) backoff.Reset();
                // Token recusado não se conserta tentando de novo em segundos:
                // espera o teto, e relê o arquivo na próxima.
                var delay = outcome == Outcome.Unauthorized ? backoff.Max : backoff.Next();
                LogRetrying(delay);
                await Task.Delay(delay, stoppingToken);
            }
        }
        catch (Exception) when (stoppingToken.IsCancellationRequested)
        {
            // O serviço está parando: o que caiu junto com a conexão não é falha.
        }
        catch (Exception e)
        {
            // O host do .NET para com código 0 quando o serviço lança, e o systemd
            // só reinicia o que sai com erro.
            LogFatal(e);
            Environment.ExitCode = 1;
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // O fechamento educado vai antes de cancelar a recepção: com ele o
        // SentinelWeb põe a máquina OFFLINE na hora, e não depois de 2 minutos.
        if (_session is { } session) await session.CloseAsync("Agente encerrando", timings.CloseTimeout);
        await base.StopAsync(cancellationToken);
    }

    private async Task<Outcome> ConnectAndServeAsync(Uri url, CancellationToken stoppingToken)
    {
        // O segredo de instalação presente é o pedido de cadastro: na primeira
        // subida, e quando alguém o põe de novo depois de o token ser revogado.
        if (File.Exists(_options.EnrollSecretFile) && await EnrollAsync(stoppingToken) is { } refused)
        {
            // O segredo que sobrou (não deu para apagar, ou a instalação o pôs de
            // novo) não tira a máquina do ar: com o token gravado, ela conecta, e
            // o servidor diz se ele ainda vale.
            if (!File.Exists(_options.TokenFile)) return refused;
            LogEnrollSkipped(_options.TokenFile);
        }

        string token;
        try
        {
            // Relido a cada conexão: o cadastro novo vale sem reiniciar o agente.
            token = SecretFile.Read(_options.TokenFile);
        }
        catch (Exception e) when (e is SecretFileException or IOException or UnauthorizedAccessException)
        {
            LogTokenUnreadable(e.Message);
            return Outcome.Failed;
        }

        using var socket = CreateSocket(token);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(timings.ConnectTimeout);
            await socket.ConnectAsync(url, timeout.Token);
        }
        catch (WebSocketException) when (socket.HttpStatusCode == HttpStatusCode.Unauthorized)
        {
            // O agente não se recadastra sozinho: ele apagou o segredo, e é isso
            // que faz a revogação valer. Quem decide voltar põe o segredo de novo.
            LogUnauthorized(url, _options.EnrollSecretFile);
            return Outcome.Unauthorized;
        }
        catch (Exception e) when (!stoppingToken.IsCancellationRequested &&
                                  e is WebSocketException or HttpRequestException or OperationCanceledException)
        {
            var reason = e is OperationCanceledException
                ? $"sem resposta em {timings.ConnectTimeout.TotalSeconds:0} s"
                : e.GetBaseException().Message;
            LogConnectFailed(url, reason);
            return Outcome.Failed;
        }

        using var session = new Session(socket);
        try
        {
            // O handshake vai primeiro, em toda conexão: é ele que liga esta
            // conexão ao HWID no servidor, e sem ele o comando não chega.
            var hello = await handshake.CreateAsync(stoppingToken);
            await session.SendAsync(Messages.CreateHandshake(hello), stoppingToken);
            _session = session;
            LogConnected(url, hello.Hwid);

            using var sessionEnd = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var pinger = PingWhenIdleAsync(session, hello.Hwid, sessionEnd.Token);

            try
            {
                var reason = await ReceiveAsync(session, stoppingToken);
                LogDisconnected(reason);
            }
            finally
            {
                await sessionEnd.CancelAsync();
                await pinger;
            }

            // O servidor fecha com 1008 quando o HWID do handshake não é o do
            // token: a placa ou o firmware mudou depois do cadastro, ou o token veio
            // numa imagem clonada. Tentar de novo dá no mesmo, e só o recadastro
            // conserta (A11).
            if (session.Socket.CloseStatus == WebSocketCloseStatus.PolicyViolation)
            {
                LogHwidRefused(hello.Hwid, _options.EnrollSecretFile);
                return Outcome.Unauthorized;
            }
            return Stable(session);
        }
        catch (Exception e) when (!stoppingToken.IsCancellationRequested && e is WebSocketException or ObjectDisposedException)
        {
            LogDisconnected(e.GetBaseException().Message);
            return _session is null ? Outcome.Failed : Stable(session);
        }
        finally
        {
            _session = null;
        }
    }

    /// <summary>
    /// A conexão que caiu logo depois do handshake conta como falha: sem isso, o
    /// proxy que aceita e derruba faria o agente reconectar a cada poucos segundos.
    /// </summary>
    private Outcome Stable(Session session) =>
        session.Age >= timings.StableAfter ? Outcome.Served : Outcome.Failed;

    /// <summary>
    /// Troca o segredo de instalação pelo token da máquina, grava o token e apaga
    /// o segredo. Devolve <c>null</c> quando deu certo, ou o motivo de não seguir.
    /// </summary>
    private async Task<Outcome?> EnrollAsync(CancellationToken stoppingToken)
    {
        string secret;
        try
        {
            secret = SecretFile.Read(_options.EnrollSecretFile);
        }
        catch (Exception e) when (e is SecretFileException or IOException or UnauthorizedAccessException)
        {
            LogEnrollSecretUnreadable(e.Message);
            return Outcome.Failed;
        }

        var hwid = (await handshake.CreateAsync(stoppingToken)).Hwid;
        var result = await _enrollment!.EnrollAsync(secret, hwid, stoppingToken);

        switch (result.Outcome)
        {
            case EnrollOutcome.Enrolled:
                try
                {
                    SecretFile.Write(_options.TokenFile, result.Token!);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // O segredo fica: na próxima volta, o cadastro emite outro token.
                    LogTokenNotWritten(_options.TokenFile, e.Message);
                    return Outcome.Failed;
                }
                try
                {
                    // O segredo vale para a frota inteira: guardado aqui, quem
                    // tomasse esta máquina cadastraria outras.
                    File.Delete(_options.EnrollSecretFile);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    LogEnrollSecretNotDeleted(_options.EnrollSecretFile, e.Message);
                }
                LogEnrolled(hwid, _options.TokenFile);
                return null;
            case EnrollOutcome.SecretRefused:
                LogEnrollSecretRefused(_options.EnrollSecretFile);
                return Outcome.Unauthorized;
            case EnrollOutcome.AlreadyConnected:
                LogEnrollConflict(hwid);
                return Outcome.Unauthorized;
            default:
                LogEnrollFailed(result.Reason ?? "sem motivo");
                return Outcome.Failed;
        }
    }

    private ClientWebSocket CreateSocket(string token)
    {
        var socket = new ClientWebSocket();
        // O token no header, nunca na URL: header o log do servidor oculta.
        socket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
        // Para separar o 401 (token errado) das outras falhas.
        socket.Options.CollectHttpResponseDetails = true;
        socket.Options.KeepAliveInterval = timings.KeepAliveInterval;
        socket.Options.KeepAliveTimeout = timings.KeepAliveTimeout;
        if (_certificate is not null) socket.Options.RemoteCertificateValidationCallback = _certificate.Validate;
        return socket;
    }

    /// <summary>Escuta até a conexão fechar, e devolve o motivo, para o log.</summary>
    private async Task<string> ReceiveAsync(Session session, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxIncomingBytes];

        while (true)
        {
            var count = 0;
            ValueWebSocketReceiveResult result;
            do
            {
                if (count == buffer.Length) return $"mensagem do servidor maior que {MaxIncomingBytes / 1024} KiB";

                result = await session.Socket.ReceiveAsync(buffer.AsMemory(count), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    var closed = $"o servidor fechou ({(int?)session.Socket.CloseStatus} {session.Socket.CloseStatusDescription})";
                    await session.CloseAsync("", timings.CloseTimeout);
                    return closed;
                }

                count += result.Count;
            } while (!result.EndOfMessage);

            var message = Messages.Read(buffer.AsSpan(0, count));
            if (message is null) LogUnreadable(count);
            else if (message.Command is { } command) await HandleCommandAsync(command, cancellationToken);
            else LogUnexpectedType(message.Type);
        }
    }

    private async Task HandleCommandAsync(CommandPayload command, CancellationToken cancellationToken)
    {
        LogCommandReceived(command.Action, command.CommandId);
        try
        {
            await commands.HandleAsync(command, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // O comando que falhou não derruba a conexão.
            LogCommandFailed(e, command.Action, command.CommandId);
        }
    }

    private async Task PingWhenIdleAsync(Session session, string hwid, CancellationToken cancellationToken)
    {
        try
        {
            // O sorteio é um por ping: sorteado de novo a cada espera, o silêncio
            // passaria do máximo.
            var interval = NextPingInterval();
            while (true)
            {
                var idle = session.IdleFor;
                if (idle < interval)
                {
                    await Task.Delay(interval - idle, cancellationToken);
                    continue;
                }

                await session.SendAsync(Messages.CreatePing(hwid), cancellationToken);
                interval = NextPingInterval();
            }
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
            // A sessão acabou; quem decide reconectar é a recepção.
        }
    }

    private TimeSpan NextPingInterval() =>
        timings.PingIntervalMin + (timings.PingIntervalMax - timings.PingIntervalMin) * Random.Shared.NextDouble();

    /// <summary>
    /// Uma conexão de pé. O WebSocket aceita um envio por vez, e quem envia são
    /// várias funções ao mesmo tempo: o envio passa por uma trava.
    /// </summary>
    /// <remarks>
    /// Quem chama desiste só da espera pela trava: cancelar um envio no meio
    /// aborta o WebSocket inteiro, então o envio em curso vai até o fim da sessão.
    /// </remarks>
    private sealed class Session(ClientWebSocket socket) : IDisposable
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly CancellationTokenSource _lifetime = new();
        private readonly long _started = Environment.TickCount64;
        private long _lastSent = Environment.TickCount64;

        public ClientWebSocket Socket => socket;

        public TimeSpan Age => TimeSpan.FromMilliseconds(Environment.TickCount64 - _started);

        public TimeSpan IdleFor => TimeSpan.FromMilliseconds(Environment.TickCount64 - Interlocked.Read(ref _lastSent));

        public async Task SendAsync(OutgoingMessage message, CancellationToken cancellationToken)
        {
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                await socket.SendAsync(message.Json, WebSocketMessageType.Text, endOfMessage: true, _lifetime.Token);
                Interlocked.Exchange(ref _lastSent, Environment.TickCount64);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>Manda o fechamento, sem esperar a resposta e sem lançar.</summary>
        public async Task CloseAsync(string description, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                await _sendLock.WaitAsync(cts.Token);
                try
                {
                    if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, description, cts.Token);
                }
                finally
                {
                    _sendLock.Release();
                }
            }
            catch (Exception e) when (e is OperationCanceledException or WebSocketException or ObjectDisposedException)
            {
                // Fechar é cortesia: se não deu, o socket é descartado do mesmo jeito.
            }
        }

        // A trava não é descartada: quem ainda espera nela ficaria preso para
        // sempre, e o SemaphoreSlim sem AvailableWaitHandle não segura recurso.
        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }

    [LoggerMessage(LogLevel.Information, "Conectado a {Url} como {Hwid}.")]
    private partial void LogConnected(Uri url, string hwid);

    [LoggerMessage(LogLevel.Warning, "Conexão encerrada: {Reason}.")]
    private partial void LogDisconnected(string reason);

    [LoggerMessage(LogLevel.Warning, "Falha ao conectar em {Url}: {Reason}.")]
    private partial void LogConnectFailed(Uri url, string reason);

    [LoggerMessage(LogLevel.Error,
        "{Url} recusou o token desta máquina (401): ele foi revogado ou substituído. " +
        "Para cadastrar de novo, ponha o segredo de instalação em {EnrollSecretFile}.")]
    private partial void LogUnauthorized(Uri url, string enrollSecretFile);

    [LoggerMessage(LogLevel.Error, "Sem token para conectar: {Reason}")]
    private partial void LogTokenUnreadable(string reason);

    [LoggerMessage(LogLevel.Information, "Máquina {Hwid} cadastrada: token gravado em {TokenFile}, segredo de instalação apagado.")]
    private partial void LogEnrolled(string hwid, string tokenFile);

    [LoggerMessage(LogLevel.Error, "O servidor recusou o segredo de instalação (401). Confira {EnrollSecretFile} contra o AGENT_ENROLL_SECRET do servidor.")]
    private partial void LogEnrollSecretRefused(string enrollSecretFile);

    [LoggerMessage(LogLevel.Error, "O servidor recusou o cadastro (409): {Hwid} já está conectada com outro token.")]
    private partial void LogEnrollConflict(string hwid);

    [LoggerMessage(LogLevel.Warning, "Falha no cadastro: {Reason}.")]
    private partial void LogEnrollFailed(string reason);

    [LoggerMessage(LogLevel.Error, "Sem segredo de instalação para o cadastro: {Reason}")]
    private partial void LogEnrollSecretUnreadable(string reason);

    [LoggerMessage(LogLevel.Warning, "O token foi gravado, mas o segredo de instalação continua em {EnrollSecretFile}: {Reason}. Apague-o à mão.")]
    private partial void LogEnrollSecretNotDeleted(string enrollSecretFile, string reason);

    [LoggerMessage(LogLevel.Warning, "O cadastro não deu certo, mas há token em {TokenFile}: a conexão segue com ele.")]
    private partial void LogEnrollSkipped(string tokenFile);

    [LoggerMessage(LogLevel.Error, "O token veio do servidor, mas não pôde ser gravado em {TokenFile}: {Reason}.")]
    private partial void LogTokenNotWritten(string tokenFile, string reason);

    [LoggerMessage(LogLevel.Critical, "A conexão parou por um erro que não se conserta tentando de novo.")]
    private partial void LogFatal(Exception exception);

    [LoggerMessage(LogLevel.Error,
        "O servidor recusou o HWID {Hwid}: o token desta máquina foi emitido para outro. " +
        "O hardware mudou ou o token veio de outra máquina; para cadastrar de novo, ponha o segredo de instalação em {EnrollSecretFile}.")]
    private partial void LogHwidRefused(string hwid, string enrollSecretFile);

    [LoggerMessage(LogLevel.Information, "Nova tentativa em {Delay}.")]
    private partial void LogRetrying(TimeSpan delay);

    [LoggerMessage(LogLevel.Information, "Comando {Action} recebido ({CommandId}).")]
    private partial void LogCommandReceived(string action, string commandId);

    [LoggerMessage(LogLevel.Error, "O comando {Action} ({CommandId}) falhou.")]
    private partial void LogCommandFailed(Exception exception, string action, string commandId);

    [LoggerMessage(LogLevel.Warning, "Mensagem do servidor descartada: {Bytes} bytes fora do protocolo.")]
    private partial void LogUnreadable(int bytes);

    [LoggerMessage(LogLevel.Warning, "Mensagem do servidor descartada: tipo {Type} desconhecido.")]
    private partial void LogUnexpectedType(string type);
}

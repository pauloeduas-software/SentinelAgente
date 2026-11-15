using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SentinelAgente.Tests.Connection;

/// <summary>
/// Um /agent-hub de mentira, com o comportamento do SentinelWeb que o agente
/// precisa respeitar (`agent.maestro.ts` e a decisão CR de lá): o cadastro troca o
/// segredo de instalação por um token; o hub dá 401 antes do upgrade a quem não
/// tem um token emitido; e o servidor pode mandar comando e fechar com 1001.
/// </summary>
internal sealed class FakeAgentHub : IAsyncDisposable
{
    public const string EnrollSecret = "segredo-de-instalacao";

    private readonly WebApplication _app;
    private readonly CancellationTokenSource _stopping = new();
    private int _refused;

    private FakeAgentHub(WebApplication app) => _app = app;

    public required Uri Url { get; init; }

    /// <summary>Os tokens que o hub aceita: os emitidos pelo cadastro e os que o teste põe.</summary>
    public ConcurrentDictionary<string, string> Tokens { get; } = new();

    /// <summary>Quando definido, o cadastro responde este status em vez de emitir token.</summary>
    public int? EnrollStatus { get; set; }

    /// <summary>O HWID de cada cadastro feito.</summary>
    public Channel<string> Enrolled { get; } = Channel.CreateUnbounded<string>();

    /// <summary>O texto de cada mensagem que chegou, em ordem.</summary>
    public Channel<string> Received { get; } = Channel.CreateUnbounded<string>();

    /// <summary>O socket de cada conexão aceita, para o teste falar por ele.</summary>
    public Channel<WebSocket> Accepted { get; } = Channel.CreateUnbounded<WebSocket>();

    /// <summary>Como o agente fechou cada conexão.</summary>
    public Channel<WebSocketCloseStatus?> Closed { get; } = Channel.CreateUnbounded<WebSocketCloseStatus?>();

    /// <summary>
    /// Quando ligado, o hub para de ler depois da primeira mensagem: o envio
    /// seguinte do agente enche o buffer e fica preso, como numa rede que travou.
    /// </summary>
    public bool StopReadingAfterHandshake { get; set; }

    /// <summary>Quantas conexões levaram 401.</summary>
    public int Refused => Volatile.Read(ref _refused);

    public static async Task<FakeAgentHub> StartAsync(X509Certificate2? certificate = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(System.Net.IPAddress.Loopback, 0, listen =>
        {
            if (certificate is not null) listen.UseHttps(certificate);
        }));

        var app = builder.Build();
        app.UseWebSockets();

        FakeAgentHub? hub = null;
        app.MapPost("/agent-hub/enroll", context => hub!.EnrollAsync(context));
        app.Map("/agent-hub", context => hub!.HandleAsync(context));

        await app.StartAsync();
        var address = new Uri(app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single());

        var scheme = certificate is null ? "ws" : "wss";
        var host = certificate is null ? address.Host : "localhost";
        hub = new FakeAgentHub(app) { Url = new Uri($"{scheme}://{host}:{address.Port}/agent-hub") };
        return hub;
    }

    private async Task EnrollAsync(HttpContext context)
    {
        if (context.Request.Headers.Authorization != $"Bearer {EnrollSecret}")
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (EnrollStatus is { } status)
        {
            context.Response.StatusCode = status;
            return;
        }

        using var body = await JsonDocument.ParseAsync(context.Request.Body);
        var hwid = body.RootElement.GetProperty("Hwid").GetString()!;
        var token = $"token-de-{hwid}-{Guid.NewGuid():N}";
        Tokens[token] = hwid;
        Enrolled.Writer.TryWrite(hwid);

        context.Response.StatusCode = StatusCodes.Status201Created;
        await context.Response.WriteAsJsonAsync(new { Token = token });
    }

    private async Task HandleAsync(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal) || !Tokens.ContainsKey(header["Bearer ".Length..]))
        {
            Interlocked.Increment(ref _refused);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        Accepted.Writer.TryWrite(socket);

        var buffer = new byte[64 * 1024];
        try
        {
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Closed.Writer.TryWrite(result.CloseStatus);
                    if (socket.State == WebSocketState.CloseReceived)
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    return;
                }

                Received.Writer.TryWrite(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (StopReadingAfterHandshake)
                {
                    // Até a conexão cair ou o hub parar: o Kestrel espera o handler terminar.
                    using var stop = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, _stopping.Token);
                    await Task.Delay(Timeout.Infinite, stop.Token).ContinueWith(_ => { });
                    return;
                }
            }
        }
        catch (WebSocketException)
        {
            Closed.Writer.TryWrite(null);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

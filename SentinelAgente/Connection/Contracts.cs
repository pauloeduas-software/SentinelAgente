using SentinelAgente.Protocol;

namespace SentinelAgente.Connection;

// As três pontas que ligam a conexão às funções sem que ela as conheça: o
// Inventory implementa a apresentação, o Commands recebe os comandos, e quem
// manda dado (a telemetria) usa o IServerLink.

/// <summary>Monta a apresentação da máquina, a cada conexão.</summary>
public interface IHandshakeSource
{
    ValueTask<HandshakePayload> CreateAsync(CancellationToken cancellationToken);
}

/// <summary>Recebe os comandos que o servidor manda.</summary>
/// <remarks>
/// Roda dentro da recepção: enquanto ele não volta, a resposta ao ping do
/// WebSocket não é lida, e passados 15 s a conexão cai. Quem implementa dispara
/// a ação e volta, sem esperar o processo terminar.
/// </remarks>
public interface ICommandHandler
{
    Task HandleAsync(CommandPayload command, CancellationToken cancellationToken);
}

/// <summary>O caminho até o servidor, para quem tem algo a mandar.</summary>
public interface IServerLink
{
    /// <summary>Se há conexão de pé, com o handshake já feito.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Manda a mensagem se a conexão está de pé. Devolve <c>false</c> sem
    /// conexão: quem chamou decide o que fazer com o dado (A9).
    /// </summary>
    Task<bool> TrySendAsync(OutgoingMessage message, CancellationToken cancellationToken);
}

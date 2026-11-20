using SentinelAgente.Protocol;

namespace SentinelAgente.Connection;

// As três pontas que ligam a conexão às funções sem que ela as conheça: o
// Inventory implementa a apresentação, o Commands recebe os comandos, e quem
// manda dado (a telemetria) usa o IServerLink, que também avisa quando o servidor
// pede os dados agora.

/// <summary>
/// Monta a apresentação da máquina a cada handshake (na conexão, no reenvio diário e
/// no <c>REFRESH</c>), e também uma vez na subida e no cadastro, que pega dela o HWID.
/// </summary>
public interface IHandshakeSource
{
    ValueTask<HandshakePayload> CreateAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Recebe todo <c>Command</c> que não é o <c>REFRESH</c>, que a conexão trata (A26):
/// quem implementa recusa a <c>Action</c> fora da lista (A5).
/// </summary>
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
    /// conexão ou quando ela cai no meio do envio: quem chamou decide o que
    /// fazer com o dado. Só lança quando quem chamou cancela.
    /// </summary>
    Task<bool> TrySendAsync(OutgoingMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Completa quando o servidor pede os dados agora (<c>REFRESH</c>, A26). Cada
    /// pedido completa a tarefa da vez e põe outra no lugar: quem espera relê a
    /// propriedade a cada volta, e a tarefa abandonada não consome pedido nenhum.
    /// </summary>
    Task RefreshRequested { get; }
}

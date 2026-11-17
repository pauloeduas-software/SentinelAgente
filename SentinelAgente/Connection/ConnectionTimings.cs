namespace SentinelAgente.Connection;

/// <summary>
/// Os tempos da conexão. Não estão no agent.json porque quem instala não tem
/// por que mexer: a reconexão e o ping do WebSocket são os do Fleet
/// (<c>orbit/pkg/wstransport/manager.go</c>), o <c>Ping</c> é o do Tactical RMM,
/// e os testes trocam por milissegundos.
/// </summary>
public sealed record ConnectionTimings
{
    /// <summary>A primeira espera depois de uma falha; dobra a cada falha seguida.</summary>
    public TimeSpan BackoffInitial { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>O teto da espera, e a espera depois de o servidor recusar o token.</summary>
    public TimeSpan BackoffMax { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Quanto a conexão precisa ficar de pé para zerar a espera: a que cai antes
    /// conta como falha, e a seguinte espera mais.
    /// </summary>
    public TimeSpan StableAfter { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Quanto o upgrade para WebSocket pode demorar.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// O ping do WebSocket e quanto se espera pela resposta. Sem ele, a conexão
    /// que morreu sem aviso (suspensão, troca de rede) parece viva por horas.
    /// </summary>
    public TimeSpan KeepAliveInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan KeepAliveTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Sem mandar nada por um tempo sorteado entre os dois, vai um <c>Ping</c>
    /// do protocolo, como o <c>checkin_hello</c> do Tactical RMM. O SentinelWeb
    /// põe a máquina OFFLINE depois de 2 minutos sem mensagem (o máximo daqui
    /// mais 60 s de folga), e o ping do WebSocket não conta para isso. O sorteio
    /// evita que a frota que reconectou junto pingue junto.
    /// </summary>
    public TimeSpan PingIntervalMin { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan PingIntervalMax { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// De quanto em quanto a conexão de pé manda o handshake de novo, com o
    /// inventário relido (A25): sem isso, a peça trocada e o IP novo só chegavam
    /// na próxima reconexão, que numa máquina sempre ligada pode levar semanas.
    /// Um dia, como o inventário do GLPI Agent.
    /// </summary>
    public TimeSpan InventoryInterval { get; init; } = TimeSpan.FromDays(1);

    /// <summary>Quanto o fechamento educado pode demorar quando o agente encerra.</summary>
    public TimeSpan CloseTimeout { get; init; } = TimeSpan.FromSeconds(5);
}

namespace SentinelAgente.Telemetry;

/// <summary>
/// Os tempos da telemetria. Não estão no agent.json, como os da conexão: a hora
/// é a do Fleet (A12), a primeira janela é a do Tactical RMM, e os testes trocam
/// por milissegundos.
/// </summary>
public sealed record TelemetryTimings
{
    /// <summary>Entre uma amostra e a seguinte, o <c>detail_update_interval</c> do Fleet.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// A média da primeira amostra, que não tem anterior: a leitura de partida e
    /// a primeira ficam a esse tempo uma da outra, como o <c>cpu.Percent(10s)</c>
    /// do Tactical RMM.
    /// </summary>
    public TimeSpan FirstWindow { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>De quanto em quanto se confere se a conexão subiu, antes da primeira amostra.</summary>
    public TimeSpan ConnectedPoll { get; init; } = TimeSpan.FromSeconds(2);
}

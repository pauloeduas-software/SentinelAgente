using SentinelAgente.Connection;
using SentinelAgente.Protocol;

namespace SentinelAgente.Telemetry;

/// <summary>
/// A amostra de hora em hora (A12). A primeira vai quando a conexão sobe pela
/// primeira vez, para a tela não passar uma hora vazia; dali em diante, uma por
/// hora, com ou sem conexão.
/// </summary>
/// <remarks>
/// Sem conexão, a amostra é descartada (A17): a leitura anterior avança do mesmo
/// jeito, e a próxima é a média da última hora, e não de todas desde a queda.
/// Coletor que falha não derruba o serviço: a parte dele vai zerada, e o motivo,
/// para o log.
/// </remarks>
internal sealed partial class TelemetryService(
    IServerLink link,
    IEnumerable<ITelemetryCollector> collectors,
    TelemetryTimings timings,
    Func<string> hwid,
    ILogger<TelemetryService> logger) : BackgroundService
{
    private readonly ITelemetryCollector[] _collectors = collectors.ToArray();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!link.IsConnected) await Task.Delay(timings.ConnectedPoll, stoppingToken);

            // A conexão só sobe com o HWID lido (A13): aqui ele não falta.
            var id = hwid();

            // A leitura de partida: CPU e rede precisam de uma anterior para a média.
            Collect();
            await Task.Delay(timings.FirstWindow, stoppingToken);

            using var timer = new PeriodicTimer(timings.Interval);
            do
            {
                await SendSampleAsync(id, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // O serviço está parando.
        }
    }

    /// <summary>
    /// Uma volta: o erro que escapar dela vai para o log e não para o serviço,
    /// como a saída do Telegraf que falha. Uma hora ruim não para as seguintes.
    /// </summary>
    private async Task SendSampleAsync(string hwid, CancellationToken stoppingToken)
    {
        try
        {
            var message = Messages.CreateTelemetry(Collect().ToPayload(hwid));
            if (!await link.TrySendAsync(message, stoppingToken)) LogDiscarded();
        }
        catch (Exception e) when (e is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            LogSampleFailed(e);
        }
    }

    private TelemetrySample Collect()
    {
        var sample = new TelemetrySample();
        foreach (var collector in _collectors)
        {
            try
            {
                collector.Collect(sample);
            }
            catch (Exception e)
            {
                LogCollectorFailed(e, collector.GetType().Name);
            }
        }
        return sample;
    }

    [LoggerMessage(LogLevel.Information, "Amostra de telemetria descartada: sem conexão com o servidor.")]
    private partial void LogDiscarded();

    [LoggerMessage(LogLevel.Error, "A amostra de telemetria falhou; a próxima vai na hora seguinte.")]
    private partial void LogSampleFailed(Exception exception);

    [LoggerMessage(LogLevel.Warning, "O coletor {Collector} falhou; a parte dele vai zerada nesta amostra.")]
    private partial void LogCollectorFailed(Exception exception, string collector);
}

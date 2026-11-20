using SentinelAgente.Connection;
using SentinelAgente.Protocol;

namespace SentinelAgente.Telemetry;

/// <summary>
/// A amostra de hora em hora (A12). A primeira vai 10 s depois de a conexão subir
/// pela primeira vez, para a tela não passar uma hora vazia; dali em diante, uma por
/// hora, com ou sem conexão. O pedido do servidor (A26) manda uma na hora, sem
/// mexer no relógio da hora.
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
            // O pedido é olhado ANTES de cada espera e de cada envio: o que chegasse
            // enquanto a amostra sai, ou antes de a conexão ser notada, se perderia, e o
            // servidor esperaria a amostra à toa. O que chega no meio manda outra, e vale.
            var refresh = link.RefreshRequested;
            while (!link.IsConnected) await Task.Delay(timings.ConnectedPoll, stoppingToken);

            // O HWID com que a conexão se apresentou: a amostra vai com o mesmo.
            var id = hwid();

            // A leitura de partida: a CPU precisa de uma anterior para a média. O
            // pedido do servidor encurta a janela: a média fica mais curta, e vale.
            Collect();
            await Task.WhenAny(Task.Delay(timings.FirstWindow, stoppingToken), refresh);
            stoppingToken.ThrowIfCancellationRequested();

            // O tique fica de uma volta para a outra: o PeriodicTimer aceita uma espera
            // por vez, e a volta que o pedido acordou deixa a dele pendente.
            using var timer = new PeriodicTimer(timings.Interval);
            var tick = timer.WaitForNextTickAsync(stoppingToken).AsTask();
            while (true)
            {
                refresh = link.RefreshRequested;
                await SendSampleAsync(id, stoppingToken);

                var woke = await Task.WhenAny(tick, refresh);
                if (woke != tick) continue;
                if (!await tick) return;
                tick = timer.WaitForNextTickAsync(stoppingToken).AsTask();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // O serviço está parando.
        }
        catch (Exception e)
        {
            // Como na conexão: o host do .NET para com código 0 quando o serviço
            // lança, e o systemd só reinicia o que sai com erro.
            LogFatal(e);
            Environment.ExitCode = 1;
            throw;
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

    [LoggerMessage(LogLevel.Critical, "A telemetria parou por um erro que não se conserta tentando de novo.")]
    private partial void LogFatal(Exception exception);

    [LoggerMessage(LogLevel.Information, "Amostra de telemetria descartada: sem conexão com o servidor.")]
    private partial void LogDiscarded();

    [LoggerMessage(LogLevel.Error, "A amostra de telemetria falhou; a próxima vai na hora seguinte.")]
    private partial void LogSampleFailed(Exception exception);

    [LoggerMessage(LogLevel.Warning, "O coletor {Collector} falhou; a parte dele fica zerada.")]
    private partial void LogCollectorFailed(Exception exception, string collector);
}

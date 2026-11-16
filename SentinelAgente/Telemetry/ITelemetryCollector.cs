namespace SentinelAgente.Telemetry;

/// <summary>
/// Um coletor por métrica, como os <c>plugins/inputs</c> do Telegraf: cada um
/// preenche a sua parte da amostra e não sabe das outras. Métrica nova é pasta
/// nova e um registro no <see cref="TelemetryRegistration"/>.
/// </summary>
/// <remarks>
/// Quem compara com a leitura anterior (a CPU) guarda essa leitura: a
/// amostra sai com a média desde a anterior (A16).
/// </remarks>
public interface ITelemetryCollector
{
    void Collect(TelemetrySample sample);
}

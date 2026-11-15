using SentinelAgente.Inventory;

namespace SentinelAgente.Telemetry.Cpu;

/// <summary>
/// O uso da CPU desde a amostra anterior, pela diferença da linha <c>cpu</c> do
/// <c>/proc/stat</c>, como o Telegraf (<c>plugins/inputs/cpu/cpu.go</c>). Com
/// uma amostra por hora, é a média da hora, e não o pico de um segundo (A16).
/// </summary>
/// <remarks>
/// Sem leitura anterior não há valor: o serviço faz uma leitura de partida e
/// espera a primeira janela antes da primeira amostra.
/// </remarks>
internal sealed class CpuUsage(SystemRoot root) : ITelemetryCollector
{
    private CpuTimes? _previous;

    public void Collect(TelemetrySample sample)
    {
        if (Read(root) is not { } current) return;
        if (_previous is { } previous) sample.CpuUsagePercentage = Percent(previous, current);
        _previous = current;
    }

    /// <summary>O tempo total e o parado, em ticks, desde o boot.</summary>
    internal readonly record struct CpuTimes(ulong Total, ulong Idle);

    internal static CpuTimes? Read(SystemRoot root)
    {
        foreach (var line in (root.ReadText("proc/stat") ?? "").Split('\n'))
        {
            if (!line.StartsWith("cpu ")) continue;

            var fields = line[4..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var ticks = new List<ulong>();
            foreach (var field in fields)
            {
                if (!ulong.TryParse(field, out var value)) return null;
                ticks.Add(value);
            }
            if (ticks.Count < 4) return null;

            // user nice system idle iowait irq softirq steal: guest e guest_nice
            // já estão dentro de user e nice, e somá-los contaria duas vezes.
            var total = ticks.Take(8).Aggregate(0UL, (sum, value) => sum + value);
            var idle = ticks[3] + (ticks.Count > 4 ? ticks[4] : 0);
            return new CpuTimes(total, idle);
        }
        return null;
    }

    /// <summary>A porcentagem ocupada entre duas leituras; contador que volta para trás dá 0.</summary>
    internal static double Percent(CpuTimes before, CpuTimes after)
    {
        if (after.Total <= before.Total) return 0;

        var total = after.Total - before.Total;
        // O iowait pode voltar para trás em alguns kernels: o parado nunca é negativo.
        var idle = after.Idle > before.Idle ? Math.Min(after.Idle - before.Idle, total) : 0;
        return Math.Round(100.0 * (total - idle) / total, 1);
    }
}

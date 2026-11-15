using SentinelAgente.Inventory;
using SentinelAgente.Protocol;

namespace SentinelAgente.Telemetry.Processes;

/// <summary>
/// Os processos que mais ocupam RAM: o <c>VmRSS</c> do <c>/proc/&lt;pid&gt;/status</c>,
/// um por PID, com o nome do <c>/proc/&lt;pid&gt;/comm</c>, como o Tactical RMM
/// (<c>agent/process.go</c>) (A16).
/// </summary>
/// <remarks>
/// Processos com o mesmo nome não se juntam: o protocolo leva um <c>Pid</c> por
/// item. Thread de kernel não tem <c>VmRSS</c> e fica de fora, como o processo
/// que terminou entre a lista e a leitura.
/// </remarks>
internal sealed class TopProcesses(SystemRoot root) : ITelemetryCollector
{
    public const int Count = 10;

    public void Collect(TelemetrySample sample) => sample.TopProcesses = Read();

    internal IReadOnlyList<ProcessPayload> Read()
    {
        var processes = new List<(int Pid, string Name, long RssKb)>();

        foreach (var directory in Directory.EnumerateDirectories(root.Combine("proc")))
        {
            if (!int.TryParse(Path.GetFileName(directory), out var pid)) continue;
            if (Rss(root.ReadText($"proc/{pid}/status")) is not { } rss || rss <= 0) continue;
            if (root.ReadValue($"proc/{pid}/comm") is not { } name) continue;

            processes.Add((pid, name, rss));
        }

        return processes
            .OrderByDescending(process => process.RssKb)
            .ThenBy(process => process.Pid)
            .Take(Count)
            .Select(process => new ProcessPayload(process.Pid, process.Name, Math.Round(process.RssKb / 1024.0, 1)))
            .ToList();
    }

    private static long? Rss(string? status)
    {
        foreach (var line in (status ?? "").Split('\n'))
        {
            if (!line.StartsWith("VmRSS:")) continue;
            return long.TryParse(line["VmRSS:".Length..].Trim().Split(' ')[0], out var kb) ? kb : null;
        }
        return null;
    }
}

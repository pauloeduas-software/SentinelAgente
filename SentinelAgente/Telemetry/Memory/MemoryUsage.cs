using SentinelAgente.Inventory;

namespace SentinelAgente.Telemetry.Memory;

/// <summary>
/// A RAM pelo <c>/proc/meminfo</c>: o <c>MemTotal</c> cru, e o usado é o que
/// falta para o <c>MemAvailable</c>, a conta do <c>free</c> e do gopsutil
/// (<c>mem/mem_linux.go</c>). Cache que o kernel devolve quando pedem não conta
/// como usado (A16).
/// </summary>
/// <remarks>
/// O total vai cru, sem arredondar, como no <c>free</c>. O handshake não leva a
/// RAM (A24).
/// </remarks>
internal sealed class MemoryUsage(SystemRoot root) : ITelemetryCollector
{
    public void Collect(TelemetrySample sample)
    {
        long? total = null, available = null;
        foreach (var line in (root.ReadText("proc/meminfo") ?? "").Split('\n'))
        {
            if (line.StartsWith("MemTotal:")) total = Kilobytes(line);
            else if (line.StartsWith("MemAvailable:")) available = Kilobytes(line);
        }

        if (total is not > 0 || available is null) return;
        sample.RamTotalBytes = total.Value * 1024;
        sample.RamUsedBytes = Math.Max(0, total.Value - available.Value) * 1024;
    }

    private static long? Kilobytes(string line)
    {
        var value = line[(line.IndexOf(':') + 1)..].Trim().Split(' ')[0];
        return long.TryParse(value, out var kb) ? kb : null;
    }
}

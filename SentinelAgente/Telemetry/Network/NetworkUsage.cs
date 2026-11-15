using SentinelAgente.Inventory;
using SentinelAgente.Protocol;

namespace SentinelAgente.Telemetry.Network;

/// <summary>
/// O tráfego das placas físicas, pelo <c>/proc/net/dev</c>: os contadores desde
/// o boot, somados, e a velocidade média desde a amostra anterior (A16).
/// </summary>
/// <remarks>
/// Física é a placa com <c>/sys/class/net/&lt;placa&gt;/device</c>. O que passa
/// pelo túnel da VPN, pela bridge, pelo docker e pelas veth também passa pela
/// placa física, e somar os dois contaria o mesmo byte duas vezes. Placa cujo
/// contador voltou para trás (a que saiu e voltou, como o adaptador USB) não
/// soma na velocidade daquela amostra.
/// </remarks>
internal sealed class NetworkUsage(SystemRoot root, TimeProvider time) : ITelemetryCollector
{
    private Dictionary<string, Counters>? _previous;
    private long _previousAt;

    internal readonly record struct Counters(ulong Received, ulong Sent);

    public void Collect(TelemetrySample sample)
    {
        var current = Read();
        var now = time.GetTimestamp();

        double rxKbps = 0, txKbps = 0;
        if (_previous is not null && time.GetElapsedTime(_previousAt, now).TotalSeconds is var seconds and > 0)
        {
            ulong received = 0, sent = 0;
            foreach (var (name, counters) in current)
            {
                if (!_previous.TryGetValue(name, out var before)) continue;
                if (counters.Received >= before.Received) received += counters.Received - before.Received;
                if (counters.Sent >= before.Sent) sent += counters.Sent - before.Sent;
            }
            rxKbps = Kbps(received, seconds);
            txKbps = Kbps(sent, seconds);
        }

        _previous = current;
        _previousAt = now;

        var totalReceived = current.Values.Aggregate(0UL, (sum, counters) => sum + counters.Received);
        var totalSent = current.Values.Aggregate(0UL, (sum, counters) => sum + counters.Sent);
        sample.Network = new NetworkPayload((long)totalReceived, (long)totalSent, rxKbps, txKbps);
    }

    /// <summary>Os contadores de cada placa física.</summary>
    internal Dictionary<string, Counters> Read()
    {
        var counters = new Dictionary<string, Counters>();

        // As duas primeiras linhas são o cabeçalho.
        foreach (var line in (root.ReadText("proc/net/dev") ?? "").Split('\n').Skip(2))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;

            var name = line[..colon].Trim();
            if (!root.HasDirectory($"sys/class/net/{name}/device")) continue;

            // bytes packets errs drop fifo frame compressed multicast, e o mesmo do envio.
            var fields = line[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 9) continue;
            if (!ulong.TryParse(fields[0], out var received) || !ulong.TryParse(fields[8], out var sent)) continue;

            counters[name] = new Counters(received, sent);
        }
        return counters;
    }

    private static double Kbps(ulong bytes, double seconds) => Math.Round(bytes * 8 / 1000.0 / seconds, 1);
}

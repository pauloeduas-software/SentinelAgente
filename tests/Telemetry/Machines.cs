using SentinelAgente.Inventory;

namespace SentinelAgente.Tests.Telemetry;

/// <summary>As máquinas do testdata e um relógio que só anda quando o teste manda.</summary>
internal static class Machines
{
    public static SystemRoot Root(string name) => new(Path.Combine(AppContext.BaseDirectory, "testdata", "maquinas", name));
}

internal sealed class ManualTime : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
}

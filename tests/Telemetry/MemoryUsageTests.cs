using SentinelAgente.Telemetry;
using SentinelAgente.Telemetry.Memory;

namespace SentinelAgente.Tests.Telemetry;

public sealed class MemoryUsageTests
{
    [Fact]
    public void O_usado_e_o_que_falta_para_o_disponivel()
    {
        var sample = new TelemetrySample();

        new MemoryUsage(Machines.Root("thinkpad")).Collect(sample);

        Assert.Equal(7_972_384L * 1024, sample.RamTotalBytes);
        Assert.Equal((7_972_384L - 5_386_652) * 1024, sample.RamUsedBytes);
    }
}

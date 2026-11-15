using SentinelAgente.Inventory;
using SentinelAgente.Telemetry;
using SentinelAgente.Telemetry.Cpu;

namespace SentinelAgente.Tests.Telemetry;

public sealed class CpuUsageTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("sentinel-cpu-").FullName;

    [Fact]
    public void Le_o_total_e_o_parado_sem_contar_o_guest_duas_vezes()
    {
        var times = CpuUsage.Read(Machines.Root("thinkpad"));

        // user nice system idle iowait irq softirq steal; o guest já está no user.
        Assert.Equal(new CpuUsage.CpuTimes(67_273_075, 61_234_598 + 45_120), times);
    }

    [Fact]
    public void A_amostra_e_a_media_desde_a_leitura_anterior()
    {
        var cpu = new CpuUsage(new SystemRoot(_directory));

        WriteStat("cpu  1000 0 500 8000 500 0 0 0 0 0");
        var first = Collect(cpu);
        // 1000 ticks a mais, 250 deles parados: 75% ocupado.
        WriteStat("cpu  1500 0 750 8200 550 0 0 0 0 0");
        var second = Collect(cpu);

        Assert.Equal(0, first.CpuUsagePercentage);
        Assert.Equal(75, second.CpuUsagePercentage);
    }

    [Fact]
    public void Contador_que_volta_para_tras_da_zero()
    {
        var before = new CpuUsage.CpuTimes(10_000, 8_000);

        Assert.Equal(0, CpuUsage.Percent(before, new CpuUsage.CpuTimes(9_000, 7_000)));
        Assert.Equal(0, CpuUsage.Percent(before, before));
    }

    [Fact]
    public void Iowait_que_volta_para_tras_nao_faz_o_parado_negativo()
    {
        var percent = CpuUsage.Percent(new CpuUsage.CpuTimes(10_000, 8_000), new CpuUsage.CpuTimes(11_000, 7_990));

        Assert.Equal(100, percent);
    }

    [Fact]
    public void Sem_proc_stat_nao_preenche()
    {
        var sample = Collect(new CpuUsage(new SystemRoot(_directory)));

        Assert.Equal(0, sample.CpuUsagePercentage);
    }

    private static TelemetrySample Collect(CpuUsage cpu)
    {
        var sample = new TelemetrySample();
        cpu.Collect(sample);
        return sample;
    }

    private void WriteStat(string line)
    {
        Directory.CreateDirectory(Path.Combine(_directory, "proc"));
        File.WriteAllText(Path.Combine(_directory, "proc", "stat"), line + "\ncpu0 0 0 0 0 0 0 0 0 0 0\n");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

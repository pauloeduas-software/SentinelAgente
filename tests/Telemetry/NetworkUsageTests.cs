using SentinelAgente.Inventory;
using SentinelAgente.Protocol;
using SentinelAgente.Telemetry;
using SentinelAgente.Telemetry.Network;

namespace SentinelAgente.Tests.Telemetry;

public sealed class NetworkUsageTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("sentinel-rede-").FullName;
    private readonly ManualTime _time = new();

    private static NetworkPayload? Collect(NetworkUsage network)
    {
        var sample = new TelemetrySample();
        network.Collect(sample);
        return sample.Network;
    }

    [Fact]
    public void Conta_so_a_placa_fisica_sem_o_loopback()
    {
        var network = Collect(new NetworkUsage(Machines.Root("thinkpad"), _time));

        Assert.Equal(new NetworkPayload(8_734_512_390, 912_345_678, 0, 0), network);
    }

    [Fact]
    public void Docker_e_veth_ficam_de_fora_e_as_duas_placas_somam()
    {
        var network = Collect(new NetworkUsage(Machines.Root("notebook-dell"), _time));

        Assert.Equal(9_123_456_789L + 512_000_000, network?.BytesReceived);
        Assert.Equal(1_234_567_890L + 128_000_000, network?.BytesSent);
    }

    [Theory]
    [InlineData("servidor-kvm", 845_123_456_789L)]
    [InlineData("pc-montado", 3_456_789_012L)]
    public void Bridge_vm_e_tunel_nao_contam_o_mesmo_byte_de_novo(string machine, long received)
    {
        var network = Collect(new NetworkUsage(Machines.Root(machine), _time));

        Assert.Equal(received, network?.BytesReceived);
    }

    [Fact]
    public void A_velocidade_e_a_media_desde_a_amostra_anterior()
    {
        var network = new NetworkUsage(new SystemRoot(_directory), _time);

        WriteDev(("eth0", 1_000_000, 500_000));
        Collect(network);
        // 10 s depois: 12,5 MB recebidos e 1,25 MB enviados.
        _time.Advance(TimeSpan.FromSeconds(10));
        WriteDev(("eth0", 13_500_000, 1_750_000));
        var second = Collect(network);

        Assert.Equal(new NetworkPayload(13_500_000, 1_750_000, 10_000, 1_000), second);
    }

    [Fact]
    public void Placa_cujo_contador_voltou_nao_soma_na_velocidade()
    {
        var network = new NetworkUsage(new SystemRoot(_directory), _time);

        WriteDev(("eth0", 1_000_000, 1_000_000), ("eth1", 9_000_000, 9_000_000));
        Collect(network);
        _time.Advance(TimeSpan.FromSeconds(8));
        // O adaptador USB saiu e voltou: o contador dele zerou.
        WriteDev(("eth0", 2_000_000, 1_000_000), ("eth1", 100, 100));
        var second = Collect(network);

        Assert.Equal(1_000, second?.RxSpeedKbps);
        Assert.Equal(0, second?.TxSpeedKbps);
    }

    private void WriteDev(params (string Name, long Received, long Sent)[] cards)
    {
        var lines = new List<string>
        {
            "Inter-|   Receive                                                |  Transmit",
            " face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed",
        };
        foreach (var (name, received, sent) in cards)
        {
            Directory.CreateDirectory(Path.Combine(_directory, "sys", "class", "net", name, "device"));
            lines.Add($"  {name}: {received} 10 0 0 0 0 0 0 {sent} 10 0 0 0 0 0 0");
        }
        Directory.CreateDirectory(Path.Combine(_directory, "proc", "net"));
        File.WriteAllLines(Path.Combine(_directory, "proc", "net", "dev"), lines);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

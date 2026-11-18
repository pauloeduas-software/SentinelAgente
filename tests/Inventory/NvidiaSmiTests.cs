using SentinelAgente.Inventory;

namespace SentinelAgente.Tests.Inventory;

public sealed class NvidiaSmiTests
{
    [Fact]
    public void Cada_placa_vai_pelo_slot_do_sys_com_a_memoria_em_bytes()
    {
        const string Output = "00000000:01:00.0, 8192\n00000000:0A:00.0, 24576\n";

        Assert.Equal(
            new Dictionary<string, long>
            {
                ["0000:01:00.0"] = 8L * 1024 * 1024 * 1024,
                ["0000:0a:00.0"] = 24L * 1024 * 1024 * 1024,
            },
            NvidiaSmi.Parse(Output));
    }

    [Fact]
    public void A_placa_sem_memoria_fica_de_fora()
    {
        const string Output = "00000000:01:00.0, [N/A]\n00000000:02:00.0, 4096\n";

        Assert.Equal(["0000:02:00.0"], NvidiaSmi.Parse(Output).Keys);
    }

    [Fact]
    public void Saida_sem_formato_nao_vira_placa()
    {
        Assert.Empty(NvidiaSmi.Parse("NVIDIA-SMI has failed because it couldn't communicate with the NVIDIA driver.\n"));
    }
}

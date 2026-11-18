using SentinelAgente.Inventory;
using SentinelAgente.Protocol;

namespace SentinelAgente.Tests.Inventory;

public sealed class GpuInfoTests
{
    private static IReadOnlyList<GpuPayload>? Read(string machine) =>
        Read(machine, () => new Dictionary<string, long>());

    private static IReadOnlyList<GpuPayload>? Read(string machine, Func<IReadOnlyDictionary<string, long>> nvidiaMemory) =>
        GpuInfo.Read(new SystemRoot(Path.Combine(AppContext.BaseDirectory, "testdata", "maquinas", machine)), nvidiaMemory);

    [Fact]
    public void O_notebook_hibrido_manda_as_duas_placas_na_ordem_do_slot()
    {
        Assert.Equal(
        [
            new GpuPayload("UHD Graphics 620", null),
            new GpuPayload("GeForce MX150", null),
        ], Read("thinkpad"));
    }

    [Fact]
    public void A_nvidia_vai_com_a_memoria_do_nvidia_smi_pelo_slot()
    {
        Assert.Equal(
        [
            new GpuPayload("UHD Graphics 620", null),
            new GpuPayload("GeForce MX150", 2L * 1024 * 1024 * 1024),
        ], Read("thinkpad", () => new Dictionary<string, long> { ["0000:01:00.0"] = 2L * 1024 * 1024 * 1024 }));
    }

    [Fact]
    public void Sem_placa_nvidia_o_nvidia_smi_nao_roda()
    {
        Read("pc-montado", () => throw new InvalidOperationException("o nvidia-smi rodou"));
    }

    [Fact]
    public void A_amd_vai_com_a_vram_e_o_audio_hdmi_fica_de_fora()
    {
        Assert.Equal(
        [
            new GpuPayload("Radeon RX 6700/6700 XT/6750 XT / 6800M/6850M XT", 12 * 1024L * 1024 * 1024),
        ], Read("pc-montado"));
    }

    [Fact]
    public void A_amd_integrada_vai_sem_a_reserva_da_bios()
    {
        Assert.Equal([new GpuPayload("1002:1638", null)], Read("notebook-ryzen"));
    }

    [Fact]
    public void Sem_o_pci_ids_a_placa_fica_com_o_id()
    {
        Assert.Equal([new GpuPayload("1234:1111", null)], Read("servidor-kvm"));
    }

    [Fact]
    public void Sem_placa_de_video_o_campo_nao_vai()
    {
        Assert.Null(Read("notebook-dell"));
    }
}

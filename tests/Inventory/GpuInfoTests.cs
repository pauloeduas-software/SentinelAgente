using SentinelAgente.Inventory;
using SentinelAgente.Protocol;

namespace SentinelAgente.Tests.Inventory;

public sealed class GpuInfoTests
{
    private static IReadOnlyList<GpuPayload>? Read(string machine) =>
        GpuInfo.Read(new SystemRoot(Path.Combine(AppContext.BaseDirectory, "testdata", "maquinas", machine)));

    [Fact]
    public void O_notebook_hibrido_manda_as_duas_placas_na_ordem_do_slot()
    {
        Assert.Equal(
        [
            new GpuPayload("UHD Graphics 620", "Intel Corporation", null, null, "00:02.0", "8086:5917"),
            new GpuPayload("GeForce MX150", "NVIDIA Corporation", "GP108M", null, "01:00.0", "10de:1d10"),
        ], Read("thinkpad"));
    }

    [Fact]
    public void A_amd_vai_com_a_vram_e_o_audio_hdmi_fica_de_fora()
    {
        Assert.Equal(
        [
            new GpuPayload("Radeon RX 6700/6700 XT/6750 XT / 6800M/6850M XT", "Advanced Micro Devices, Inc. [AMD/ATI]", "Navi 22",
                12 * 1024L * 1024 * 1024, "03:00.0", "1002:73df"),
        ], Read("pc-montado"));
    }

    [Fact]
    public void Sem_o_pci_ids_a_placa_fica_com_o_id()
    {
        Assert.Equal([new GpuPayload("1234:1111", null, null, null, "00:01.0", "1234:1111")], Read("servidor-kvm"));
    }

    [Fact]
    public void Sem_placa_de_video_o_campo_nao_vai()
    {
        Assert.Null(Read("notebook-dell"));
    }
}

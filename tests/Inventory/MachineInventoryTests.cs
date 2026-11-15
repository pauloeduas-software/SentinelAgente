using System.Net;
using SentinelAgente.Identity;
using SentinelAgente.Inventory;
using SentinelAgente.Protocol;

namespace SentinelAgente.Tests.Inventory;

public sealed class MachineInventoryTests : IDisposable
{
    private const long GiB = 1024L * 1024 * 1024;

    private readonly string _directory = Directory.CreateTempSubdirectory("sentinel-raiz-").FullName;

    private static string Machine(string name) => Path.Combine(AppContext.BaseDirectory, "testdata", "maquinas", name);

    // Os endereços que o sistema daria a cada placa.
    private static readonly Dictionary<string, InterfaceAddress[]> Addresses = new()
    {
        ["enxa0cec8d1e2f3"] = [Ip("fe80::a2ce:c8ff:fed1:e2f3", 64), Ip("192.168.1.37", 24)],
        ["wlp2s0"] = [Ip("192.168.1.52", 24)],
        ["enp3s0"] = [Ip("172.17.0.1", 16), Ip("192.168.0.15", 24)],
        ["tun0"] = [Ip("10.8.0.6", 24)],
        ["wlp3s0"] = [Ip("192.168.1.80", 24)],
        ["br0"] = [Ip("10.10.0.5", 16)],
    };

    private static InterfaceAddress Ip(string address, int prefix) => new(IPAddress.Parse(address), prefix);

    private static HandshakePayload Read(string root) =>
        new MachineInventory(root, name => Addresses.GetValueOrDefault(name) ?? []).Create();

    [Fact]
    public void Monta_a_ficha_do_notebook()
    {
        Assert.Equal(new HandshakePayload
        {
            Hwid = Hwid.Hash("B4C04F4B"),
            Hostname = "nb-financeiro-07",
            OsVersion = "Ubuntu 24.04",
            CpuModel = "Intel(R) Core(TM) i5-10210U CPU @ 1.60GHz",
            MacAddress = "a0:ce:c8:d1:e2:f3",
            LocalIp = "192.168.1.37",
            BiosSerial = "B4C04F4B",
            SystemUuid = "4c4c4544-0034-4310-8030-b4c04f4b5032",
            Manufacturer = "Dell Inc.",
            HardwareModel = "Latitude 5410",
            RamTotalBytes = 16 * GiB,
        }, Read(Machine("notebook-dell")));
    }

    [Fact]
    public void A_dock_de_menor_metrica_ganha_do_wifi()
    {
        var payload = Read(Machine("notebook-dell"));

        Assert.Equal("a0:ce:c8:d1:e2:f3", payload.MacAddress);
    }

    [Fact]
    public void Valor_de_fabrica_vai_nulo()
    {
        var payload = Read(Machine("pc-montado"));

        Assert.Equal(Hwid.Hash("200164387600001"), payload.Hwid);
        Assert.Null(payload.BiosSerial);
        Assert.Null(payload.SystemUuid);
        Assert.Null(payload.Manufacturer);
        Assert.Null(payload.HardwareModel);
    }

    [Fact]
    public void A_vpn_fica_atras_da_placa_fisica()
    {
        var payload = Read(Machine("pc-montado"));

        Assert.Equal("d8:bb:c1:0a:1b:2c", payload.MacAddress);
        Assert.Equal("192.168.0.15", payload.LocalIp);
    }

    [Fact]
    public void O_nome_vai_sem_o_dominio()
    {
        Assert.Equal("pc-almox", Read(Machine("pc-montado")).Hostname);
    }

    [Fact]
    public void O_sistema_vai_sem_a_versao_pontual()
    {
        Assert.Equal("Debian GNU/Linux 12", Read(Machine("pc-montado")).OsVersion);
        Assert.Equal("Fedora Linux 41", Read(Machine("thinkpad")).OsVersion);
    }

    [Fact]
    public void O_modelo_da_lenovo_e_o_nome_e_nao_o_codigo()
    {
        var payload = Read(Machine("thinkpad"));

        Assert.Equal("LENOVO", payload.Manufacturer);
        Assert.Equal("ThinkPad X1 Carbon 5th", payload.HardwareModel);
    }

    [Fact]
    public void A_ram_e_arredondada_para_cima_em_gib()
    {
        // MemTotal de 7.972.384 kB, uns 7,6 GiB.
        Assert.Equal(8 * GiB, Read(Machine("thinkpad")).RamTotalBytes);
    }

    [Fact]
    public void A_bridge_serve_quando_e_a_unica_rota()
    {
        var payload = Read(Machine("servidor-kvm"));

        Assert.Equal("00:25:90:ab:cd:ef", payload.MacAddress);
        Assert.Equal("10.10.0.5", payload.LocalIp);
    }

    [Fact]
    public void Sistema_sem_versao_vai_so_com_o_nome()
    {
        Assert.Equal("Arch Linux", Read(Machine("servidor-kvm")).OsVersion);
    }

    [Fact]
    public void Os_espacos_do_nome_do_processador_viram_um()
    {
        Assert.Equal("Intel(R) Xeon(R) CPU E5-2670 0 @ 2.60GHz", Read(Machine("servidor-kvm")).CpuModel);
    }

    [Fact]
    public void Sem_rede_nem_os_release_o_sistema_ainda_vai()
    {
        Write("sys/class/dmi/id/product_serial", "B4C04F4B");
        Write("proc/sys/kernel/hostname", "pc-01");

        var payload = Read(_directory);

        Assert.Equal("Linux", payload.OsVersion);
        Assert.Null(payload.MacAddress);
        Assert.Null(payload.LocalIp);
        Assert.Null(payload.CpuModel);
        Assert.Null(payload.RamTotalBytes);
    }

    [Fact]
    public void Sem_dado_do_firmware_nao_sobe()
    {
        Write("sys/class/dmi/id/product_serial", "Default string");

        Assert.Throws<HwidException>(() => Read(_directory));
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_directory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

using System.Net;
using SentinelAgente.Inventory;
using SentinelAgente.Protocol;

namespace SentinelAgente.Tests.Inventory;

public sealed class MachineInventoryTests : IDisposable
{
    private const long GiB = 1024L * 1024 * 1024;

    // O HWID inventado, gravado antes de cada teste: ele não sai do firmware (A20).
    private const string TestHwid = "7d3a1c20-5b4e-4f61-9a2b-1c0d9e8f7a99";

    private readonly string _directory = Directory.CreateTempSubdirectory("sentinel-raiz-").FullName;
    private readonly string _hwidFile;

    public MachineInventoryTests()
    {
        _hwidFile = Path.Combine(_directory, "hwid");
        File.WriteAllText(_hwidFile, TestHwid + "\n");
    }

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

    private HandshakePayload Read(string root) =>
        new MachineInventory(root, name => Addresses.GetValueOrDefault(name) ?? [], _hwidFile).Create();

    [Fact]
    public void Monta_a_ficha_do_notebook()
    {
        Assert.Equal(new HandshakePayload
        {
            Hwid = TestHwid,
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
    public void No_pc_montado_vai_o_serial_da_placa_e_o_resto_de_fabrica_vai_nulo()
    {
        var payload = Read(Machine("pc-montado"));

        // O serial do produto é de fábrica, e o da placa toma o lugar, como no GLPI.
        Assert.Equal("200164387600001", payload.BiosSerial);
        Assert.Null(payload.SystemUuid);
        Assert.Null(payload.Manufacturer);
        Assert.Null(payload.HardwareModel);
    }

    [Fact]
    public void O_bond_de_menor_metrica_ganha_do_wifi()
    {
        Write("sys/class/dmi/id/product_serial", "B4C04F4B");
        Write("proc/net/route",
            "Iface\tDestination\tGateway \tFlags\tRefCnt\tUse\tMetric\tMask\t\tMTU\tWindow\tIRTT\n" +
            "wlp2s0\t00000000\t0101A8C0\t0003\t0\t0\t600\t00000000\t0\t0\t0\n" +
            "bond0\t00000000\t0101A8C0\t0003\t0\t0\t100\t00000000\t0\t0\t0\n");
        // O bond não tem device/, mas é Ethernet: a placa por onde o servidor sai.
        Write("sys/class/net/bond0/type", "1");
        Write("sys/class/net/bond0/address", "00:25:90:00:00:01");
        Write("sys/class/net/wlp2s0/type", "1");
        Write("sys/class/net/wlp2s0/device/uevent", "");
        Write("sys/class/net/wlp2s0/address", "3c:a9:f4:5e:61:08");

        Assert.Equal("00:25:90:00:00:01", Read(_directory).MacAddress);
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

    [Theory]
    [InlineData("Lenovo V14 G2 ITL")]
    [InlineData("ThinkPad X1 Carbon 5th")]
    public void Na_lenovo_o_modelo_e_sempre_o_product_version(string version)
    {
        Write("sys/class/dmi/id/product_serial", "PF0XYZ12");
        Write("sys/class/dmi/id/sys_vendor", "LENOVO");
        Write("sys/class/dmi/id/product_name", "82KB");
        Write("sys/class/dmi/id/product_version", version);

        Assert.Equal(version, Read(_directory).HardwareModel);
    }

    [Fact]
    public void Na_lenovo_sem_product_version_fica_o_codigo()
    {
        Write("sys/class/dmi/id/product_serial", "PF0XYZ12");
        Write("sys/class/dmi/id/sys_vendor", "LENOVO");
        Write("sys/class/dmi/id/product_name", "82KB");
        Write("sys/class/dmi/id/product_version", "None");

        Assert.Equal("82KB", Read(_directory).HardwareModel);
    }

    [Fact]
    public void O_modelo_lixo_da_supermicro_vai_nulo()
    {
        Write("sys/class/dmi/id/product_serial", "B4C04F4B");
        Write("sys/class/dmi/id/sys_vendor", "Supermicro");
        Write("sys/class/dmi/id/product_name", "Super Server");

        Assert.Null(Read(_directory).HardwareModel);
    }

    [Theory]
    [InlineData("NAME=\"openSUSE Tumbleweed\"\nVERSION_ID=\"20241115\"\nPRETTY_NAME=\"openSUSE Tumbleweed\"\n", "openSUSE Tumbleweed")]
    [InlineData("VERSION_ID=1.0\nPRETTY_NAME=\"Distro 1.0.3\"\n", "Linux 1.0")]
    public void O_sistema_nao_muda_com_snapshot_nem_volta_ao_pretty_name(string osRelease, string expected)
    {
        Write("sys/class/dmi/id/product_serial", "B4C04F4B");
        Write("etc/os-release", osRelease);

        Assert.Equal(expected, Read(_directory).OsVersion);
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
    public void Sem_dado_do_firmware_a_maquina_sobe_sem_serial()
    {
        Write("sys/class/dmi/id/product_serial", "Default string");
        Write("sys/class/dmi/id/board_serial", "Default string");

        var payload = Read(_directory);

        Assert.Equal(TestHwid, payload.Hwid);
        Assert.Null(payload.BiosSerial);
        Assert.Null(payload.SystemUuid);
    }

    [Fact]
    public void O_hwid_lido_uma_vez_vale_para_quem_mais_o_manda()
    {
        var inventory = new MachineInventory(_directory, _ => [], _hwidFile);
        var handshake = inventory.Create();

        // Apagado com o agente rodando, o arquivo não troca o HWID: a telemetria
        // continua com o da conexão, e não sorteia outro.
        File.Delete(_hwidFile);

        Assert.Equal(handshake.Hwid, inventory.Hwid);
        Assert.False(File.Exists(_hwidFile));
    }

    [Fact]
    public async Task A_lista_de_programas_entra_no_handshake_a_cada_conexao()
    {
        var reads = 0;
        var inventory = new MachineInventory(Machine("notebook-dell"), _ => [], _hwidFile, _ =>
        {
            reads++;
            return Task.FromResult<IReadOnlyList<SoftwarePayload>?>([new SoftwarePayload("bash", "5.2.21-2ubuntu4", "amd64", "deb")]);
        });

        await inventory.CreateAsync(TestContext.Current.CancellationToken);
        var payload = await inventory.CreateAsync(TestContext.Current.CancellationToken);

        Assert.Equal("bash", Assert.Single(payload.InstalledSoftware!).Name);
        Assert.Equal(2, reads);
    }

    [Fact]
    public void As_placas_de_video_entram_no_handshake()
    {
        Assert.Equal(["UHD Graphics 620", "GeForce MX150"], Read(Machine("thinkpad")).Gpus!.Select(gpu => gpu.Name));
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_directory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

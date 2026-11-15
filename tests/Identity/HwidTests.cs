using SentinelAgente.Identity;

namespace SentinelAgente.Tests.Identity;

public sealed class HwidTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("sentinel-dmi-").FullName;

    private static string Dmi(string machine) => Path.Combine(AppContext.BaseDirectory, "testdata", "dmi", machine);

    [Fact]
    public void Usa_o_serial_do_produto_quando_ele_e_de_verdade()
    {
        Assert.Equal(Hwid.Hash("B4C04F4B"), Hwid.Read(Dmi("notebook-dell")));
    }

    [Fact]
    public void Cai_no_serial_da_placa_quando_o_do_produto_e_de_fabrica()
    {
        Assert.Equal(Hwid.Hash("200164387600001"), Hwid.Read(Dmi("pc-montado")));
    }

    [Fact]
    public void Cai_no_uuid_quando_os_dois_seriais_sao_de_fabrica()
    {
        Assert.Equal(Hwid.Hash("6f1a2b3c-4d5e-4f60-8a71-92b3c4d5e6f7"), Hwid.Read(Dmi("vm-virtualbox")));
    }

    [Theory]
    [InlineData("0000-0000")]
    [InlineData("00 00 00 00")]
    [InlineData("SystemSerialNumber")]
    [InlineData("Base Board Serial Number")]
    [InlineData("A-B-C")]
    public void O_serial_de_fabrica_se_reconhece_sem_os_separadores(string serial)
    {
        Assert.False(Hwid.IsRealSerial(serial));
    }

    [Theory]
    [InlineData("00020003-0004-0005-0006-000700080009")]
    [InlineData("00010203-0405-0607-0809-0a0b0c0d0e0f")]
    public void O_uuid_de_fabrica_da_ami_na_outra_ordem_de_bytes_nao_serve(string uuid)
    {
        Assert.False(Hwid.IsRealUuid(uuid));
    }

    [Fact]
    public void O_serial_da_placa_com_barras_da_dell_serve()
    {
        Assert.True(Hwid.IsRealSerial("/B4C04F4B/CNCMK0004L0123/"));
    }

    [Fact]
    public void Nao_inventa_identidade_quando_nada_serve()
    {
        var error = Assert.Throws<HwidException>(() => Hwid.Read(Dmi("mini-pc-ami")));
        Assert.Contains("Default string", error.Message);
    }

    [Fact]
    public void Arquivo_que_o_firmware_nao_tem_conta_como_ausente()
    {
        File.WriteAllText(Path.Combine(_directory, "product_uuid"), "6f1a2b3c-4d5e-4f60-8a71-92b3c4d5e6f7\n");

        Assert.Equal(Hwid.Hash("6f1a2b3c4d5e4f608a7192b3c4d5e6f7"), Hwid.Read(_directory));
    }

    [Fact]
    public void Sem_root_o_erro_pede_sudo()
    {
        Assert.SkipWhen(Environment.UserName == "root", "o root lê o arquivo mesmo sem permissão");
        var path = Path.Combine(_directory, "product_serial");
        File.WriteAllText(path, "B4C04F4B");
        File.SetUnixFileMode(path, UnixFileMode.None);

        var error = Assert.Throws<HwidException>(() => Hwid.Read(_directory));
        Assert.Contains("sudo", error.Message);
    }

    [Fact]
    public void A_conta_e_a_do_agente_antigo()
    {
        // SHA-256 de "abc123": o "-", o ":", o espaço e a maiúscula não mudam o HWID.
        Assert.Equal("6ca13d52ca70c883e0f0bb101e425a89e8624de51db2d2392593af6a84118090", Hwid.Hash(" AB-C:12 3\n"));
    }

    [Theory]
    [InlineData("To Be Filled By O.E.M.")]
    [InlineData("Default string")]
    [InlineData("System Serial Number")]
    [InlineData("123456789")]
    [InlineData("0123456789")]
    [InlineData("0")]
    [InlineData("N/A")]
    [InlineData("0000000000")]
    [InlineData("xxxxxxxx")]
    [InlineData("")]
    public void Serial_de_fabrica_nao_serve(string serial)
    {
        Assert.False(Hwid.IsRealSerial(serial));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
    [InlineData("03000200-0400-0500-0006-000700080009")]
    [InlineData("03020100-0504-0706-0809-0a0b0c0d0e0f")]
    [InlineData("10000000-0000-8000-0040-000000000000")]
    [InlineData("nao-e-uuid")]
    public void Uuid_de_fabrica_nao_serve(string uuid)
    {
        Assert.False(Hwid.IsRealUuid(uuid));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

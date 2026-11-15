using SentinelAgente.Identity;

namespace SentinelAgente.Tests.Identity;

public sealed class HwidTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("sentinel-hwid-").FullName;

    private string File(string name = "hwid") => Path.Combine(_directory, name);

    [Fact]
    public void Na_primeira_vez_sorteia_um_uuid_e_grava()
    {
        var hwid = Hwid.ReadOrCreate(File());

        Assert.True(Guid.TryParseExact(hwid, "D", out _));
        Assert.Equal(hwid, System.IO.File.ReadAllText(File()).Trim());
    }

    [Fact]
    public void Depois_devolve_o_mesmo()
    {
        var first = Hwid.ReadOrCreate(File());

        Assert.Equal(first, Hwid.ReadOrCreate(File()));
    }

    [Fact]
    public void Le_o_gravado_em_minusculas()
    {
        System.IO.File.WriteAllText(File(), "7D3A1C20-5B4E-4F61-9A2B-1C0D9E8F7A01\n");

        Assert.Equal("7d3a1c20-5b4e-4f61-9a2b-1c0d9e8f7a01", Hwid.ReadOrCreate(File()));
    }

    [Fact]
    public void Cria_a_pasta_que_falta()
    {
        var path = Path.Combine(_directory, "var", "lib", "sentinel-agent", "hwid");

        Hwid.ReadOrCreate(path);

        Assert.True(System.IO.File.Exists(path));
    }

    [Fact]
    public void Nao_e_segredo_e_os_outros_leem()
    {
        Hwid.ReadOrCreate(File());

        Assert.True(System.IO.File.GetUnixFileMode(File()).HasFlag(UnixFileMode.OtherRead));
    }

    [Fact]
    public void Arquivo_mexido_nao_troca_a_identidade_em_silencio()
    {
        System.IO.File.WriteAllText(File(), "nao-e-uuid\n");

        var error = Assert.Throws<HwidException>(() => Hwid.ReadOrCreate(File()));
        Assert.Contains("Apague-o", error.Message);
        Assert.Equal("nao-e-uuid\n", System.IO.File.ReadAllText(File()));
    }

    [Fact]
    public void Sem_root_o_erro_pede_sudo()
    {
        Assert.SkipWhen(Environment.UserName == "root", "o root lê o arquivo mesmo sem permissão");
        System.IO.File.WriteAllText(File(), "7d3a1c20-5b4e-4f61-9a2b-1c0d9e8f7a01\n");
        System.IO.File.SetUnixFileMode(File(), UnixFileMode.None);

        var error = Assert.Throws<HwidException>(() => Hwid.ReadOrCreate(File()));
        Assert.Contains("sudo", error.Message);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

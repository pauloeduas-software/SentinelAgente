using SentinelAgente.Protocol;
using SentinelAgente.Software;

namespace SentinelAgente.Tests.Software;

public sealed class PackagesTests
{
    public static string Output(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "testdata", "pacotes", name));

    [Fact]
    public void O_dpkg_vai_com_os_campos_do_glpi_e_o_tamanho_em_bytes()
    {
        var packages = Dpkg.Parse(Output("dpkg-query.txt")).ToList();

        Assert.Equal(new SoftwarePayload("openssh-server", "1:9.6p1-3ubuntu13.5", "amd64", "deb", Category: "net", SizeBytes: 2038 * 1024),
            packages.Single(package => package.Name == "openssh-server"));
    }

    [Fact]
    public void Do_dpkg_so_vai_o_que_esta_instalado()
    {
        var names = Dpkg.Parse(Output("dpkg-query.txt")).Select(package => package.Name).ToList();

        // O kernel removido deixou a configuração, e o outro quebrou no meio.
        Assert.DoesNotContain("linux-image-6.8.0-31-generic", names);
        Assert.DoesNotContain("pacote-quebrado", names);
        // O pacote segurado na versão está instalado.
        Assert.Contains("libreoffice-calc", names);
    }

    [Fact]
    public void O_pacote_de_duas_arquiteturas_vai_duas_vezes()
    {
        Assert.Equal(["amd64", "i386"],
            Dpkg.Parse(Output("dpkg-query.txt")).Where(package => package.Name == "libc6").Select(package => package.Arch));
    }

    [Fact]
    public void O_pacote_de_fora_sem_tamanho_nem_secao_vai_sem_eles()
    {
        Assert.Equal(new SoftwarePayload("zoom", "6.2.5.2440", "amd64", "deb"),
            Dpkg.Parse(Output("dpkg-query.txt")).Single(package => package.Name == "zoom"));
    }

    [Fact]
    public void O_rpm_vai_com_o_release_o_fabricante_e_a_data()
    {
        // 1731758400 é 2024-11-16 ao meio-dia UTC: o mesmo dia em quase todo fuso.
        Assert.Equal(new SoftwarePayload("google-chrome-stable", "131.0.6778.69-1", "x86_64", "rpm",
                Publisher: "Google Inc.", Category: "Applications/Internet", SizeBytes: 367113584, InstallDate: new DateOnly(2024, 11, 16)),
            Rpm.Parse(Output("rpm.txt")).Single(package => package.Name == "google-chrome-stable"));
    }

    [Fact]
    public void A_chave_do_repositorio_nao_e_programa()
    {
        Assert.DoesNotContain(Rpm.Parse(Output("rpm.txt")), package => package.Name == "gpg-pubkey");
    }

    [Fact]
    public void None_e_o_grupo_unspecified_vao_nulos()
    {
        var packages = Rpm.Parse(Output("rpm.txt")).ToDictionary(package => package.Name);

        Assert.Null(packages["vim-enhanced"].Category);
        Assert.Null(packages["pacote-local"].Publisher);
        Assert.Null(packages["pacote-local"].Category);
    }
}

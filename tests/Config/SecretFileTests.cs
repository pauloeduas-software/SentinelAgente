using SentinelAgente.Config;

namespace SentinelAgente.Tests.Config;

public sealed class SecretFileTests : IDisposable
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly string _directory = Directory.CreateTempSubdirectory("sentinel-segredo-").FullName;

    [Fact]
    public void Le_o_segredo_sem_a_quebra_de_linha()
    {
        var path = Write("abc123\n", OwnerOnly);

        Assert.Equal("abc123", SecretFile.Read(path));
    }

    [Theory]
    [InlineData(UnixFileMode.GroupRead)]
    [InlineData(UnixFileMode.OtherRead)]
    public void Recusa_o_arquivo_que_outro_usuario_le(UnixFileMode extra)
    {
        var path = Write("abc123", OwnerOnly | extra);

        var error = Assert.Throws<SecretFileException>(() => SecretFile.Read(path));
        Assert.Contains("chmod 600", error.Message);
    }

    [Theory]
    [InlineData("abc\n123")]
    [InlineData("abc 123")]
    [InlineData("abc\t123")]
    public void Recusa_o_segredo_com_espaco_ou_quebra_de_linha_no_meio(string content)
    {
        var path = Write(content, OwnerOnly);

        var error = Assert.Throws<SecretFileException>(() => SecretFile.Read(path));
        Assert.Contains("uma linha só", error.Message);
    }

    [Fact]
    public void Recusa_o_arquivo_vazio()
    {
        var path = Write("  \n", OwnerOnly);

        Assert.Throws<SecretFileException>(() => SecretFile.Read(path));
    }

    [Fact]
    public void Recusa_o_arquivo_que_nao_existe()
    {
        Assert.Throws<SecretFileException>(() => SecretFile.Read(Path.Combine(_directory, "nao-existe")));
    }

    [Fact]
    public void Grava_so_para_o_dono_criando_a_pasta()
    {
        var path = Path.Combine(_directory, "var-lib", "token");

        SecretFile.Write(path, "token-novo");

        Assert.Equal(OwnerOnly, File.GetUnixFileMode(path));
        Assert.Equal("token-novo", SecretFile.Read(path));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public void Substitui_o_anterior_sem_deixar_temporario()
    {
        var path = Path.Combine(_directory, "token");
        SecretFile.Write(path, "antigo");

        SecretFile.Write(path, "novo");

        Assert.Equal("novo", SecretFile.Read(path));
        Assert.Single(Directory.GetFiles(_directory));
    }

    private string Write(string content, UnixFileMode mode)
    {
        var path = Path.Combine(_directory, "segredo");
        File.WriteAllText(path, content);
        File.SetUnixFileMode(path, mode);
        return path;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

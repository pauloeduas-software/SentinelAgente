using SentinelAgente.Config;

namespace SentinelAgente.Tests.Config;

public sealed class ServerOptionsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("sentinel-config-").FullName;
    private readonly string _tokenFile;
    private readonly ServerOptionsValidator _validator = new();

    public ServerOptionsTests()
    {
        _tokenFile = Path.Combine(_directory, "token");
        File.WriteAllText(_tokenFile, "segredo");
    }

    [Theory]
    [InlineData("wss://sentinel.empresa.com/agent-hub")]
    [InlineData("ws://localhost:3001/agent-hub")]
    [InlineData("ws://127.0.0.1:3001/agent-hub")]
    public void Aceita_wss_e_ws_so_na_propria_maquina(string url)
    {
        Assert.True(Validate(url).Succeeded);
    }

    [Theory]
    [InlineData("ws://192.168.1.10:3001/agent-hub")]
    [InlineData("ws://sentinel.empresa.com/agent-hub")]
    public void Recusa_ws_pela_rede(string url)
    {
        var result = Validate(url);

        Assert.True(result.Failed);
        Assert.Contains("sem criptografia", result.FailureMessage);
    }

    [Theory]
    [InlineData("https://sentinel.empresa.com/agent-hub")]
    [InlineData("sentinel.empresa.com/agent-hub")]
    public void Recusa_o_que_nao_e_websocket(string url)
    {
        Assert.True(Validate(url).Failed);
    }

    [Fact]
    public void Recusa_sem_endereco()
    {
        var result = _validator.Validate(null, new ServerOptions { TokenFile = _tokenFile });

        Assert.Contains("Server:Url vazio", result.FailureMessage);
    }

    [Fact]
    public void Sobe_so_com_o_segredo_de_instalacao_para_se_cadastrar()
    {
        var result = _validator.Validate(null, new ServerOptions
        {
            Url = new Uri("wss://sentinel.empresa.com/agent-hub"),
            TokenFile = Path.Combine(_directory, "ainda-nao-existe"),
            EnrollSecretFile = _tokenFile,
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Recusa_sem_token_nem_segredo_e_a_CA_que_nao_existe()
    {
        var result = _validator.Validate(null, new ServerOptions
        {
            Url = new Uri("wss://sentinel.empresa.com/agent-hub"),
            TokenFile = Path.Combine(_directory, "nao-existe"),
            EnrollSecretFile = Path.Combine(_directory, "tambem-nao-existe"),
            CaFile = Path.Combine(_directory, "ca-que-nao-existe.pem"),
        });

        Assert.Contains("Nem token", result.FailureMessage);
        Assert.Contains("Server:CaFile", result.FailureMessage);
    }

    private Microsoft.Extensions.Options.ValidateOptionsResult Validate(string url) =>
        _validator.Validate(null, new ServerOptions { Url = new Uri(url, UriKind.RelativeOrAbsolute), TokenFile = _tokenFile });

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

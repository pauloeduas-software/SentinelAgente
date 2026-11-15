using System.Text;
using SentinelAgente.Protocol;

namespace SentinelAgente.Tests.Protocol;

public sealed class MessagesTests
{
    [Fact]
    public void O_handshake_vai_em_PascalCase_sem_os_campos_nulos()
    {
        var message = Messages.CreateHandshake(new HandshakePayload
        {
            Hwid = "abc123",
            Hostname = "pc-01",
            OsVersion = "Ubuntu 24.04",
            RamTotalBytes = 17_179_869_184,
        });

        Assert.Equal(
            """{"Type":"Handshake","Payload":{"Hwid":"abc123","Hostname":"pc-01","OsVersion":"Ubuntu 24.04","RamTotalBytes":17179869184}}""",
            Encoding.UTF8.GetString(message.Json.Span));
    }

    [Fact]
    public void O_ping_leva_o_hwid()
    {
        var message = Messages.CreatePing("abc123");

        Assert.Equal("""{"Type":"Ping","Payload":{"Hwid":"abc123"}}""", Encoding.UTF8.GetString(message.Json.Span));
    }

    [Theory]
    [InlineData("""{"Type":"Command","Payload":{"Action":"SHUTDOWN","CommandId":"6f1c"}}""")]
    [InlineData("""{"type":"Command","payload":{"action":"SHUTDOWN","commandId":"6f1c"}}""")]
    public void Le_o_comando_do_servidor(string json)
    {
        var message = Messages.Read(Encoding.UTF8.GetBytes(json));

        Assert.Equal(new CommandPayload("SHUTDOWN", "6f1c"), message?.Command);
    }

    [Fact]
    public void Outro_tipo_chega_sem_conteudo()
    {
        var message = Messages.Read("""{"Type":"Novidade","Payload":{}}"""u8);

        Assert.Equal("Novidade", message?.Type);
        Assert.Null(message?.Command);
    }

    [Fact]
    public void O_cadastro_manda_o_hwid_e_le_o_token()
    {
        Assert.Equal("""{"Hwid":"abc123"}""", Encoding.UTF8.GetString(Messages.CreateEnrollRequest("abc123")));
        Assert.Equal("f00d", Messages.ReadEnrollResponse("""{"Token":"f00d"}"""u8));
    }

    [Theory]
    [InlineData("""{"error":"Segredo de instalação inválido."}""")]
    [InlineData("""{"Token":"  "}""")]
    [InlineData("isto não é json")]
    public void A_resposta_do_cadastro_sem_token_e_nula(string json)
    {
        Assert.Null(Messages.ReadEnrollResponse(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("""{"Payload":{}}""")]
    [InlineData("""{"Type":"Command","Payload":{"Action":"SHUTDOWN"}}""")]
    [InlineData("""{"Type":"Command","Payload":"SHUTDOWN"}""")]
    [InlineData("[]")]
    public void Descarta_o_que_esta_fora_do_protocolo(string json)
    {
        Assert.Null(Messages.Read(Encoding.UTF8.GetBytes(json)));
    }
}

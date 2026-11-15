using SentinelAgente.Inventory;

namespace SentinelAgente.Tests.Inventory;

public sealed class OsReleaseTests
{
    [Fact]
    public void Tira_as_aspas_e_pula_comentario_e_valor_vazio()
    {
        var fields = OsRelease.Parse("# comentário\nNAME='Alpine Linux'\nVERSION_ID=3.20.3\nVARIANT=\"\"\n\n");

        Assert.Equal(new Dictionary<string, string> { ["NAME"] = "Alpine Linux", ["VERSION_ID"] = "3.20.3" }, fields);
    }
}

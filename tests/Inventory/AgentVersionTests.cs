using System.Text.RegularExpressions;
using SentinelAgente.Inventory;

namespace SentinelAgente.Tests.Inventory;

public sealed class AgentVersionTests
{
    [Fact]
    public void E_a_versao_do_pacote_sem_o_commit()
    {
        // O SDK põe o `+<commit>` na versão informativa: no fio, só a do csproj.
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$"), AgentVersion.Current);
    }
}

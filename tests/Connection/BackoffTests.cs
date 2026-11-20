using SentinelAgente.Connection;

namespace SentinelAgente.Tests.Connection;

public sealed class BackoffTests
{
    private static readonly TimeSpan Initial = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Max = TimeSpan.FromMinutes(5);

    [Fact]
    public void Dobra_a_cada_falha_e_sorteia_entre_a_metade_e_o_valor_cheio()
    {
        var backoff = new Backoff(Initial, Max, new Random(42));

        foreach (var ceiling in new[] { 5, 10, 20, 40, 80, 160 })
        {
            var delay = backoff.Next();
            Assert.InRange(delay, TimeSpan.FromSeconds(ceiling / 2.0), TimeSpan.FromSeconds(ceiling));
        }
    }

    [Fact]
    public void Para_no_teto_sem_estourar()
    {
        var backoff = new Backoff(Initial, Max, new Random(42));
        // 5 s, 10, 20, 40, 80, 160 e 320, que já passa do teto de 5 min.
        for (var i = 0; i < 7; i++) backoff.Next();

        for (var i = 0; i < 100; i++) Assert.InRange(backoff.Next(), Max / 2, Max);
    }

    [Fact]
    public void Volta_ao_comeco_depois_da_conexao_que_ficou_de_pe()
    {
        var backoff = new Backoff(Initial, Max, new Random(42));
        for (var i = 0; i < 10; i++) backoff.Next();

        backoff.Reset();

        Assert.InRange(backoff.Next(), Initial / 2, Initial);
    }

    [Fact]
    public void Maquinas_diferentes_nao_voltam_no_mesmo_instante()
    {
        var delays = Enumerable.Range(0, 50).Select(seed => new Backoff(Initial, Max, new Random(seed)).Next());

        Assert.True(delays.Distinct().Count() > 40);
    }
}

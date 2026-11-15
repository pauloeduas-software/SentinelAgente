namespace SentinelAgente.Connection;

/// <summary>
/// A espera entre uma tentativa de conexão e outra: dobra a cada falha, até o
/// teto, e sorteia entre a metade e o valor cheio ("equal jitter", o
/// <c>pkg/backoff/equal_jitter.go</c> do Elastic Agent). O sorteio é o que
/// impede todas as máquinas de voltarem no mesmo segundo quando o servidor
/// reinicia; a metade fixa, o que impede uma tentativa colada na outra.
/// </summary>
public sealed class Backoff(TimeSpan initial, TimeSpan max, Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;
    private int _failures;

    public TimeSpan Max => max;

    public TimeSpan Next()
    {
        // 2^30 já passa de qualquer teto: o expoente para aí e não estoura.
        var exponent = Math.Min(_failures, 30);
        _failures++;

        var ceiling = Math.Min(initial.Ticks * (1L << exponent), max.Ticks);
        var half = ceiling / 2;
        return TimeSpan.FromTicks(half + _random.NextInt64(ceiling - half + 1));
    }

    public void Reset() => _failures = 0;
}

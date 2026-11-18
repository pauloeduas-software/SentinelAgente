using System.Text.RegularExpressions;

namespace SentinelAgente.Tests.Repository;

// A regra do docs/referencia/arquitetura.md, cobrada aqui e não na revisão: a base não conhece as funções,
// e uma função usa outra sem ciclo. Quem quebra a regra lê na falha o arquivo e o que ele usou.
public sealed partial class ArchitectureTests
{
    private static readonly string[] Base = ["Config", "Connection", "Protocol"];
    private static readonly string[] Functions = ["Commands", "Identity", "Inventory", "Telemetry"];

    [Fact]
    public void Toda_pasta_do_agente_e_base_ou_funcao()
    {
        var folders = Directory.EnumerateDirectories(RepositoryRoot.Combine("SentinelAgente"))
            .Select(Path.GetFileName)
            .Where(name => name is not ("bin" or "obj"))
            .Order(StringComparer.Ordinal);

        // Pasta nova entra numa das duas listas daqui e na árvore do arquitetura.md.
        Assert.Equal(Base.Concat(Functions).Order(StringComparer.Ordinal), folders);
    }

    [Fact]
    public void A_base_nao_conhece_as_funcoes()
    {
        var violations = Base
            .SelectMany(folder => References(folder).Select(used => $"{used.File} usa {used.Function}"));

        Assert.Empty(violations);
    }

    [Fact]
    public void As_funcoes_nao_se_usam_em_ciclo()
    {
        var uses = Functions.ToDictionary(
            function => function,
            function => References(function).Select(used => used.Function).Where(used => used != function).ToHashSet());

        var cycles = Functions.Select(function => CycleFrom(function, uses, [function])).OfType<string>();

        Assert.Empty(cycles);
    }

    private static string? CycleFrom(string function, Dictionary<string, HashSet<string>> uses, List<string> path)
    {
        foreach (var next in uses[function].Order(StringComparer.Ordinal))
        {
            if (next == path[0]) return string.Join(" → ", path.Append(next));
            if (path.Contains(next)) continue;
            if (CycleFrom(next, uses, [.. path, next]) is { } cycle) return cycle;
        }
        return null;
    }

    // O nome completo (`using SentinelAgente.Inventory`) e o relativo, que o namespace pai deixa escrever
    // (`Inventory.SystemRoot`). Comentário e texto entre aspas não contam.
    private static IEnumerable<(string File, string Function)> References(string folder) =>
        RepositoryRoot.Files($"SentinelAgente/{folder}", ".cs")
            .SelectMany(file =>
            {
                var code = CommentOrString().Replace(File.ReadAllText(RepositoryRoot.Combine(file)), "");
                return Functions
                    .Where(function => Regex.IsMatch(code, $@"\bSentinelAgente\.{function}\b|(?<![\w.]){function}\.[A-Z]"))
                    .Select(function => (file, function));
            });

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/|@""(?:""""|[^""])*""|""(?:\\.|[^""\\\n])*""|'(?:\\.|[^'\\\n])'",
        RegexOptions.Singleline)]
    private static partial Regex CommentOrString();
}

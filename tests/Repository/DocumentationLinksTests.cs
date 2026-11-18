using System.Text.RegularExpressions;

namespace SentinelAgente.Tests.Repository;

// Link quebrado não dá erro em lugar nenhum: a decisão muda de título, o arquivo muda de nome, e o texto
// segue apontando para algo que não existe. Aqui cada link de Markdown é conferido até a âncora, e cada
// `docs/*.md` citado no código, até o arquivo.
public sealed partial class DocumentationLinksTests
{
    private static readonly string[] Documents =
        ["CLAUDE.md", "README.md", .. RepositoryRoot.Files("docs", ".md")];

    [Fact]
    public void Todo_link_de_markdown_chega_ao_arquivo_e_a_secao()
    {
        var broken = new List<string>();

        foreach (var document in Documents)
        {
            var text = InlineCode().Replace(WithoutCodeBlocks(Read(document)), "");

            foreach (Match link in MarkdownLink().Matches(text))
            {
                var destination = link.Groups[1].Value;
                if (UrlScheme().IsMatch(destination)) continue;

                var parts = Uri.UnescapeDataString(destination).Split('#', 2);
                var target = parts[0] == ""
                    ? document
                    : Path.GetRelativePath(RepositoryRoot.Path,
                        Path.GetFullPath(Path.Combine(RepositoryRoot.Path, Path.GetDirectoryName(document)!, parts[0])));

                var exists = File.Exists(RepositoryRoot.Combine(target)) || Directory.Exists(RepositoryRoot.Combine(target));
                var anchorMissing = parts.Length == 2 && target.EndsWith(".md") && exists && !AnchorsOf(target).Contains(parts[1]);
                if (!exists || anchorMissing) broken.Add($"{document}: {destination}");
            }
        }

        Assert.Empty(broken);
    }

    [Fact]
    public void Todo_documento_citado_no_codigo_existe()
    {
        var code = RepositoryRoot.Files("SentinelAgente", ".cs")
            .Concat(RepositoryRoot.Files("tests", ".cs"))
            .Concat(RepositoryRoot.Files("packaging", ".sh", ".service", ""));

        var broken = code
            .SelectMany(file => CitedDocument().Matches(Read(file)).Select(cited => (file, cited: cited.Value)))
            .Where(pair => !File.Exists(RepositoryRoot.Combine(pair.cited)))
            .Select(pair => $"{pair.file}: {pair.cited}");

        Assert.Empty(broken);
    }

    [Fact]
    public void A_ancora_e_a_do_github()
    {
        Assert.Equal("a11--a-conexão-endereço-na-configuração-wss-e-um-token-por-máquina",
            AnchorOf("A11 — A conexão: endereço na configuração, `wss://` e um token por máquina"));
    }

    // A mesma conta do GitHub: minúsculas, sem pontuação, e cada espaço vira um hífen. É por isso que o
    // travessão de `A11 — A conexão` deixa dois hífens na âncora.
    internal static string AnchorOf(string title)
    {
        var text = HeadingLinkText().Replace(title, "$1").Trim().ToLowerInvariant();
        text = NotAnchorCharacter().Replace(text, "");
        return Whitespace().Replace(text, "-");
    }

    private static HashSet<string> AnchorsOf(string document)
    {
        var seen = new Dictionary<string, int>();
        var anchors = new HashSet<string>();

        foreach (Match heading in Heading().Matches(WithoutCodeBlocks(Read(document))))
        {
            var anchor = AnchorOf(heading.Groups[1].Value);
            var repeats = seen.GetValueOrDefault(anchor);
            seen[anchor] = repeats + 1;
            anchors.Add(repeats == 0 ? anchor : $"{anchor}-{repeats}");
        }
        return anchors;
    }

    private static string Read(string relative) => File.ReadAllText(RepositoryRoot.Combine(relative));

    private static string WithoutCodeBlocks(string text) => CodeBlock().Replace(text, "");

    [GeneratedRegex(@"^```.*?^```", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex CodeBlock();

    [GeneratedRegex(@"`[^`\n]*`")]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"\]\(([^)\s]+)\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex HeadingLinkText();

    [GeneratedRegex(@"^[a-z]+:")]
    private static partial Regex UrlScheme();

    [GeneratedRegex(@"^#{1,6}\s+(.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex Heading();

    [GeneratedRegex(@"[^\p{L}\p{M}\p{N}\s_-]")]
    private static partial Regex NotAnchorCharacter();

    [GeneratedRegex(@"\s")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\bdocs/[\w/.-]+\.md\b")]
    private static partial Regex CitedDocument();
}

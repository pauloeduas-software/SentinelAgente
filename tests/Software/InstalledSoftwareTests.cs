using SentinelAgente.Software;
using SentinelAgente.Tests.Connection;

namespace SentinelAgente.Tests.Software;

public sealed class InstalledSoftwareTests
{
    private readonly ListLogger<InstalledSoftware> _logger = new();

    /// <summary>O que cada programa devolve; o que não está aqui não está instalado.</summary>
    private sealed class RecordedQuery(Dictionary<string, Func<string>> outputs) : IPackageQuery
    {
        public Task<string?> RunAsync(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
            Task.FromResult(outputs.TryGetValue(program, out var output) ? output() : null);
    }

    private Task<IReadOnlyList<SentinelAgente.Protocol.SoftwarePayload>?> Read(Dictionary<string, Func<string>> outputs) =>
        new InstalledSoftware(new RecordedQuery(outputs), _logger).ReadAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task No_debian_vai_a_lista_do_dpkg_em_ordem()
    {
        var packages = await Read(new() { [Dpkg.Program] = () => PackagesTests.Output("dpkg-query.txt") });

        Assert.NotNull(packages);
        Assert.All(packages, package => Assert.Equal("deb", package.From));
        Assert.Equal(packages.OrderBy(package => package.Name, StringComparer.Ordinal).Select(package => package.Name),
            packages.Select(package => package.Name));
    }

    [Fact]
    public async Task Com_os_dois_gerenciadores_vao_as_duas_listas()
    {
        var packages = await Read(new()
        {
            [Dpkg.Program] = () => PackagesTests.Output("dpkg-query.txt"),
            [Rpm.Program] = () => PackagesTests.Output("rpm.txt"),
        });

        Assert.NotNull(packages);
        Assert.Equal(["deb", "rpm"], packages.Select(package => package.From).Distinct().Order());
        Assert.Equal("adduser", packages[0].Name);
    }

    [Fact]
    public async Task O_gerenciador_que_falha_nao_leva_o_outro()
    {
        var packages = await Read(new()
        {
            [Dpkg.Program] = () => throw new InvalidOperationException("/usr/bin/dpkg-query saiu com 2: banco travado"),
            [Rpm.Program] = () => PackagesTests.Output("rpm.txt"),
        });

        Assert.All(packages!, package => Assert.Equal("rpm", package.From));
        Assert.Contains(_logger.Lines, line => line.Contains("/usr/bin/dpkg-query"));
    }

    [Fact]
    public async Task Sem_gerenciador_a_lista_vai_nula_e_o_log_diz()
    {
        Assert.Null(await Read([]));
        Assert.Contains(_logger.Lines, line => line.Contains("Nenhum gerenciador"));
    }
}

using Microsoft.Extensions.Options;

namespace SentinelAgente.Config;

/// <summary>
/// Onde está o SentinelWeb e como provar quem é cada lado. Seção <c>Server</c>
/// de <c>/etc/sentinel-agent/agent.json</c>, que a instalação escreve; o endereço
/// e os segredos não moram no código (A11), só o caminho padrão dos arquivos.
/// </summary>
public sealed class ServerOptions
{
    public const string Section = "Server";

    /// <summary>O <c>/agent-hub</c>, ex.: <c>wss://sentinel.empresa.com/agent-hub</c>.</summary>
    public Uri? Url { get; set; }

    /// <summary>
    /// O segredo de instalação (o <c>AGENT_ENROLL_SECRET</c> do servidor), posto pela
    /// instalação. Existindo, o agente se cadastra, guarda o token e apaga este arquivo.
    /// </summary>
    public string EnrollSecretFile { get; set; } = "/etc/sentinel-agent/enroll-secret";

    /// <summary>O token desta máquina, que o cadastro grava e a conexão lê.</summary>
    public string TokenFile { get; set; } = "/var/lib/sentinel-agent/token";

    /// <summary>
    /// Opcional: a CA (PEM) que assina o certificado do servidor, quando ela não
    /// é pública. Com ela, só essa CA vale: as do sistema deixam de valer.
    /// </summary>
    public string? CaFile { get; set; }
}

/// <summary>
/// Confere a configuração na subida: a errada derruba o agente antes de
/// conectar, com o motivo no journal, e o systemd tenta de novo a cada 30 s.
/// </summary>
public sealed class ServerOptionsValidator : IValidateOptions<ServerOptions>
{
    public ValidateOptionsResult Validate(string? name, ServerOptions options)
    {
        var errors = new List<string>();

        if (options.Url is null)
        {
            errors.Add("Server:Url vazio: informe o /agent-hub, ex.: wss://sentinel.empresa.com/agent-hub.");
        }
        else if (!options.Url.IsAbsoluteUri || options.Url.Scheme is not ("wss" or "ws"))
        {
            errors.Add($"Server:Url \"{options.Url}\" não é um endereço wss://.");
        }
        else if (options.Url.Scheme == "ws" && !options.Url.IsLoopback)
        {
            // O segredo, o token e os comandos iriam em texto claro pela rede:
            // ws:// só vale dentro da própria máquina, em desenvolvimento.
            errors.Add($"Server:Url \"{options.Url}\" usa ws://, sem criptografia. Use wss://; ws:// só vale para localhost.");
        }

        if (string.IsNullOrWhiteSpace(options.TokenFile))
            errors.Add("Server:TokenFile vazio: informe onde o token da máquina fica.");
        if (string.IsNullOrWhiteSpace(options.EnrollSecretFile))
            errors.Add("Server:EnrollSecretFile vazio: informe onde a instalação põe o segredo de instalação.");

        if (errors.Count == 0 && !File.Exists(options.TokenFile) && !File.Exists(options.EnrollSecretFile))
        {
            errors.Add(
                $"Nem token ({options.TokenFile}) nem segredo de instalação ({options.EnrollSecretFile}): " +
                "ponha o AGENT_ENROLL_SECRET do servidor no segundo, com chmod 600.");
        }

        if (options.CaFile is not null && !File.Exists(options.CaFile))
            errors.Add($"Server:CaFile \"{options.CaFile}\" não existe.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

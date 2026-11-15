using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SentinelAgente.Connection;

/// <summary>
/// Valida o certificado do servidor contra uma CA própria (o <c>Server:CaFile</c>),
/// para quem não usa CA pública: só ela vale, e as do sistema não. Sem CaFile,
/// esta classe não entra e vale a validação padrão do .NET, com as CAs do sistema.
/// </summary>
/// <remarks>
/// É o <c>--fleet-certificate</c> do Fleet, o <c>--cert</c> do Tactical RMM e o
/// <c>--certificate-authorities</c> do Elastic Agent. O nome no certificado
/// continua conferido.
/// </remarks>
public sealed class ServerCertificate(X509Certificate2Collection trustedRoots)
{
    public static ServerCertificate FromPemFile(string path)
    {
        var roots = new X509Certificate2Collection();
        roots.ImportFromPemFile(path);
        if (roots.Count == 0) throw new InvalidOperationException($"\"{path}\" não tem nenhum certificado PEM.");
        return new ServerCertificate(roots);
    }

    public bool Validate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (certificate is null) return false;

        // A cadeia o sistema não conhece (é de uma CA própria) e refazemos
        // abaixo; nome errado ou certificado ausente não têm conserto.
        if ((errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None) return false;

        using var custom = new X509Chain();
        custom.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        custom.ChainPolicy.CustomTrustStore.AddRange(trustedRoots);
        // Uma CA interna raramente publica lista de revogação: exigir derrubaria a conexão.
        custom.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        // Só certificado de servidor: o de cliente da mesma CA, com o mesmo nome, não serve.
        custom.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));

        // Os intermediários que o servidor mandou ajudam a montar a cadeia.
        if (chain is not null)
        {
            foreach (var element in chain.ChainElements) custom.ChainPolicy.ExtraStore.Add(element.Certificate);
        }

        var leaf = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
        return custom.Build(leaf);
    }
}

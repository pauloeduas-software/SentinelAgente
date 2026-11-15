using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SentinelAgente.Tests.Connection;

/// <summary>Uma CA de mentira e certificados de servidor assinados por ela.</summary>
internal sealed class TestCertificates : IDisposable
{
    private readonly ECDsa _caKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public TestCertificates(string name = "CA de teste do SentinelAgente")
    {
        var request = new CertificateRequest($"CN={name}", _caKey, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        Authority = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    public X509Certificate2 Authority { get; }

    /// <summary>Um certificado de servidor para <paramref name="dnsName"/>, com a chave privada.</summary>
    public X509Certificate2 IssueServer(string dnsName = "localhost")
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={dnsName}", key, HashAlgorithmName.SHA256);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(dnsName);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1")], false)); // autenticação de servidor

        using var issued = request.Create(
            Authority, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), RandomNumberGenerator.GetBytes(8));
        return issued.CopyWithPrivateKey(key);
    }

    /// <summary>Grava a CA em PEM, como o administrador entregaria no Server:CaFile.</summary>
    public string WriteAuthorityPem(string directory)
    {
        var path = Path.Combine(directory, "ca.pem");
        File.WriteAllText(path, Authority.ExportCertificatePem());
        return path;
    }

    public void Dispose()
    {
        Authority.Dispose();
        _caKey.Dispose();
    }
}

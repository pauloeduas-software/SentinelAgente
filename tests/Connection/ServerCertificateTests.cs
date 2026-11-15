using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using SentinelAgente.Connection;

namespace SentinelAgente.Tests.Connection;

public sealed class ServerCertificateTests
{
    [Fact]
    public void Aceita_o_certificado_assinado_pela_CA_configurada()
    {
        using var ca = new TestCertificates();
        using var server = ca.IssueServer();
        var validation = new ServerCertificate([ca.Authority]);

        Assert.True(validation.Validate(this, server, null, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void Recusa_o_certificado_de_outra_CA()
    {
        using var ours = new TestCertificates();
        using var theirs = new TestCertificates("Outra CA");
        using var server = theirs.IssueServer();
        var validation = new ServerCertificate([ours.Authority]);

        Assert.False(validation.Validate(this, server, null, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void Recusa_o_nome_errado_mesmo_com_a_CA_certa()
    {
        using var ca = new TestCertificates();
        using var server = ca.IssueServer();
        var validation = new ServerCertificate([ca.Authority]);

        var errors = SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;
        Assert.False(validation.Validate(this, server, null, errors));
    }

    [Fact]
    public void Le_a_CA_de_um_arquivo_PEM()
    {
        using var ca = new TestCertificates();
        using var server = ca.IssueServer();
        var directory = Directory.CreateTempSubdirectory("sentinel-ca-").FullName;
        try
        {
            var validation = ServerCertificate.FromPemFile(ca.WriteAuthorityPem(directory));

            Assert.True(validation.Validate(this, server, null, SslPolicyErrors.RemoteCertificateChainErrors));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Sem_certificado_recusa()
    {
        using var ca = new TestCertificates();
        var validation = new ServerCertificate([ca.Authority]);

        Assert.False(validation.Validate(this, null, null, SslPolicyErrors.RemoteCertificateNotAvailable));
    }
}

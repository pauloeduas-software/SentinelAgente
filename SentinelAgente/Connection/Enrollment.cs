using System.Net;
using System.Net.Http.Headers;
using SentinelAgente.Protocol;

namespace SentinelAgente.Connection;

/// <summary>O que o cadastro respondeu.</summary>
public enum EnrollOutcome
{
    /// <summary>201: o token da máquina veio.</summary>
    Enrolled,

    /// <summary>401: o segredo de instalação não confere com o do servidor.</summary>
    SecretRefused,

    /// <summary>409: esta máquina já está conectada com outro token.</summary>
    AlreadyConnected,

    /// <summary>Qualquer outra coisa: rede, certificado, resposta fora do protocolo.</summary>
    Failed,
}

public sealed record EnrollResult(EnrollOutcome Outcome, string? Token = null, string? Reason = null);

/// <summary>
/// O cadastro: troca o segredo de instalação pelo token desta máquina, em
/// <c>POST /agent-hub/enroll</c> (decisão CR do SentinelWeb). Mesmo servidor, mesmo
/// certificado e mesma criptografia do WebSocket: <c>wss://</c> vira <c>https://</c>.
/// </summary>
public sealed class Enrollment(Uri hubUrl, ServerCertificate? certificate, TimeSpan timeout)
{
    /// <summary>O endereço do cadastro, ao lado do hub: <c>wss://h/agent-hub</c> → <c>https://h/agent-hub/enroll</c>.</summary>
    public static Uri EnrollUrl(Uri hubUrl) => new UriBuilder(hubUrl)
    {
        Scheme = hubUrl.Scheme == "wss" ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
        Port = hubUrl.IsDefaultPort ? -1 : hubUrl.Port,
        Path = hubUrl.AbsolutePath.TrimEnd('/') + "/enroll",
    }.Uri;

    public async Task<EnrollResult> EnrollAsync(string secret, string hwid, CancellationToken cancellationToken)
    {
        using var handler = new SocketsHttpHandler();
        if (certificate is not null) handler.SslOptions.RemoteCertificateValidationCallback = certificate.Validate;
        using var client = new HttpClient(handler) { Timeout = timeout };

        using var request = new HttpRequestMessage(HttpMethod.Post, EnrollUrl(hubUrl))
        {
            Content = new ByteArrayContent(Messages.CreateEnrollRequest(hwid))
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") },
            },
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            switch (response.StatusCode)
            {
                case HttpStatusCode.Created:
                    var token = Messages.ReadEnrollResponse(await response.Content.ReadAsByteArrayAsync(cancellationToken));
                    return token is null
                        ? new EnrollResult(EnrollOutcome.Failed, Reason: "a resposta do cadastro não trouxe o token")
                        : new EnrollResult(EnrollOutcome.Enrolled, token);
                case HttpStatusCode.Unauthorized:
                    return new EnrollResult(EnrollOutcome.SecretRefused);
                case HttpStatusCode.Conflict:
                    return new EnrollResult(EnrollOutcome.AlreadyConnected);
                default:
                    return new EnrollResult(EnrollOutcome.Failed, Reason: $"o cadastro respondeu HTTP {(int)response.StatusCode}");
            }
        }
        catch (HttpRequestException e)
        {
            return new EnrollResult(EnrollOutcome.Failed, Reason: e.GetBaseException().Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new EnrollResult(EnrollOutcome.Failed, Reason: $"sem resposta em {timeout.TotalSeconds:0} s");
        }
    }
}

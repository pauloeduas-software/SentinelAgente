using SentinelAgente.Protocol;

namespace SentinelAgente.Software;

/// <summary>
/// Os pacotes do Fedora e do Red Hat pelo <c>rpm -qa</c>, com o formato do GLPI
/// Agent (<c>Generic/Softwares/RPM.pm</c>): nome, arquitetura, versão com o
/// release, data da instalação, tamanho em bytes, fabricante e grupo (A22).
/// </summary>
/// <remarks>
/// O <c>gpg-pubkey</c> fica de fora: é a chave de um repositório que o rpm guarda
/// como pacote, e não um programa. <c>(none)</c> e o grupo <c>Unspecified</c>, que
/// o Fedora põe em tudo, vão nulos.
/// </remarks>
internal static class Rpm
{
    public const string Program = "/usr/bin/rpm";

    // O rpm troca \t e \n do formato pelo caractere: sem shell, a barra chega como barra.
    public static readonly string[] Arguments =
    [
        "--query", "--all",
        "--queryformat=%{NAME}\\t%{ARCH}\\t%{VERSION}-%{RELEASE}\\t%{INSTALLTIME}\\t%{SIZE}\\t%{VENDOR}\\t%{GROUP}\\n",
    ];

    public static IEnumerable<SoftwarePayload> Parse(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            var fields = line.Split('\t');
            if (fields.Length < 7 || fields[0].Length == 0 || fields[0] == "gpg-pubkey") continue;

            yield return new SoftwarePayload(
                Name: fields[0],
                Version: fields[2],
                Arch: Known(fields[1]),
                From: "rpm",
                Publisher: Known(fields[5]),
                Category: Known(fields[6]) is { } group && group != "Unspecified" ? group : null,
                SizeBytes: long.TryParse(fields[4], out var bytes) ? bytes : null,
                InstallDate: long.TryParse(fields[3], out var seconds) ? LocalDate(seconds) : null);
        }
    }

    private static string? Known(string value) => value.Length > 0 && value != "(none)" ? value : null;

    // O dia no fuso da máquina, como o GLPI (localtime).
    private static DateOnly LocalDate(long unixSeconds) =>
        DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime().DateTime);
}

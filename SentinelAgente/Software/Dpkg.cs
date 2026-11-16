using SentinelAgente.Protocol;

namespace SentinelAgente.Software;

/// <summary>
/// Os pacotes do Debian e do Ubuntu pelo <c>dpkg-query</c>, com o formato do GLPI
/// Agent (<c>Generic/Softwares/Deb.pm</c>): nome, arquitetura, versão, tamanho em
/// KiB e seção, e só o que está instalado de fato (A22).
/// </summary>
/// <remarks>
/// O GLPI põe a distribuição (<c>lsb_release -i</c>) como editor de todos os
/// pacotes, o do Chrome inclusive; aqui o editor vai nulo, que é o que se sabe.
/// </remarks>
internal static class Dpkg
{
    public const string Program = "/usr/bin/dpkg-query";

    // O dpkg-query troca \t e \n do formato pelo caractere: sem shell, a barra chega como barra.
    public static readonly string[] Arguments =
        ["--show", "--showformat=${Package}\\t${Architecture}\\t${Version}\\t${Installed-Size}\\t${Section}\\t${Status}\\n"];

    public static IEnumerable<SoftwarePayload> Parse(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            var fields = line.Split('\t');
            if (fields.Length < 6 || fields[0].Length == 0) continue;

            // "install ok installed" e "hold ok installed"; o removido que deixou a
            // configuração ("deinstall ok config-files") e o que quebrou no meio, não.
            if (!fields[5].EndsWith(" installed")) continue;

            yield return new SoftwarePayload(
                Name: fields[0],
                Version: fields[2],
                Arch: NullIfEmpty(fields[1]),
                From: "deb",
                Category: NullIfEmpty(fields[4]),
                SizeBytes: long.TryParse(fields[3], out var kib) ? kib * 1024 : null);
        }
    }

    private static string? NullIfEmpty(string value) => value.Length > 0 ? value : null;
}

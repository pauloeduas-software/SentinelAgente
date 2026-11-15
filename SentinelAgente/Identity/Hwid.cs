namespace SentinelAgente.Identity;

/// <summary>O HWID não pôde ser lido ou gravado; a mensagem diz o que fazer.</summary>
public sealed class HwidException(string message) : Exception(message);

/// <summary>
/// O HWID, o id desta instalação do agente (A20): um UUID sorteado no primeiro
/// arranque e guardado em disco, como o <c>agentid</c> do GLPI Agent
/// (<c>_handlePersistentState</c>, em <c>lib/GLPI/Agent.pm</c>). Ele não vem do
/// hardware: quem diz que a máquina reinstalada é a mesma é o SentinelWeb, pelo
/// serial e pelo UUID do firmware que o handshake leva (a CV de lá).
/// </summary>
/// <remarks>
/// O arquivo some com o disco formatado e com o <c>apt purge</c>: a instalação
/// nova sorteia outro. Apagá-lo à mão dá o mesmo resultado, e o token, preso ao
/// HWID antigo, passa a ser recusado com 1008 até o recadastro.
/// </remarks>
public static class Hwid
{
    /// <summary>Ao lado do token, no <c>StateDirectory</c> do serviço.</summary>
    public const string DefaultFile = "/var/lib/sentinel-agent/hwid";

    /// <summary>O HWID gravado, ou um novo, gravado antes de voltar.</summary>
    public static string ReadOrCreate(string path = DefaultFile)
    {
        try
        {
            if (File.Exists(path))
            {
                var stored = File.ReadAllText(path).Trim();
                if (Guid.TryParseExact(stored, "D", out var id)) return id.ToString();

                // Sortear outro aqui trocaria a identidade em silêncio; o
                // arquivo só fica assim mexido à mão, e quem mexeu decide.
                throw new HwidException(
                    $"O arquivo \"{path}\" não tem um HWID válido. Apague-o para o agente sortear outro; " +
                    "o token desta máquina deixa de valer, e o recadastro pede o segredo de instalação.");
            }

            var created = Guid.NewGuid().ToString();
            Write(path, created);
            return created;
        }
        catch (UnauthorizedAccessException)
        {
            throw new HwidException($"Sem permissão para \"{path}\": o agente precisa rodar como root (sudo).");
        }
        catch (IOException e)
        {
            throw new HwidException($"Falha ao ler ou gravar \"{path}\": {e.Message}");
        }
    }

    /// <summary>
    /// Num temporário que depois toma o lugar do definitivo, como o token: o
    /// agente que cai no meio não deixa um HWID pela metade.
    /// </summary>
    private static void Write(string path, string hwid)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                // Não é segredo: ele vai no fio e aparece no painel.
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                 UnixFileMode.GroupRead | UnixFileMode.OtherRead,
            }))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(hwid + "\n");
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}

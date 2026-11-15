namespace SentinelAgente.Config;

/// <summary>O segredo não pôde ser lido ou gravado; a mensagem diz o que fazer.</summary>
public sealed class SecretFileException(string message) : Exception(message);

/// <summary>
/// Os dois segredos do agente moram em arquivo, e só o root os lê: o segredo de
/// instalação, que a instalação põe e o cadastro apaga, e o token da máquina,
/// que o cadastro grava. Arquivo que outro usuário lê é recusado, como o ssh faz
/// com a chave privada.
/// </summary>
public static class SecretFile
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const UnixFileMode GroupOrOthers =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    public static string Read(string path)
    {
        if (!File.Exists(path))
            throw new SecretFileException($"O arquivo \"{path}\" não existe.");

        var mode = File.GetUnixFileMode(path);
        if ((mode & GroupOrOthers) != 0)
            throw new SecretFileException(
                $"O arquivo \"{path}\" pode ser lido por outros usuários. Rode: sudo chmod 600 {path}");

        var secret = File.ReadAllText(path).Trim();
        if (secret.Length == 0)
            throw new SecretFileException($"O arquivo \"{path}\" está vazio.");

        return secret;
    }

    /// <summary>
    /// Grava o segredo com <c>chmod 600</c> desde o primeiro byte, num arquivo
    /// temporário que depois toma o lugar do definitivo: o agente que cai no meio
    /// não deixa um token pela metade nem apaga o que funcionava (o mesmo cuidado
    /// do Fleet com o node key, em <c>client/orbit_client.go</c>).
    /// </summary>
    public static void Write(string path, string secret)
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
                UnixCreateMode = OwnerOnly,
            }))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(secret);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}

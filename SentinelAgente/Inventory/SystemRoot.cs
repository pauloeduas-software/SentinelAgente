namespace SentinelAgente.Inventory;

/// <summary>
/// De onde saem o <c>/proc</c>, o <c>/sys</c> e o <c>/etc</c>: a raiz da
/// máquina, ou uma pasta do testdata nos testes.
/// </summary>
internal sealed class SystemRoot(string path)
{
    public string Combine(string relative) => Path.Combine(path, relative);

    public bool HasDirectory(string relative) => Directory.Exists(Combine(relative));

    /// <summary>O texto do arquivo, ou <c>null</c> quando ele não existe ou não abre.</summary>
    public string? ReadText(string relative)
    {
        try
        {
            return File.ReadAllText(Combine(relative));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>O valor de um arquivo de uma linha só, sem espaço nas pontas; vazio vira <c>null</c>.</summary>
    public string? ReadValue(string relative) =>
        ReadText(relative)?.Trim() is { Length: > 0 } value ? value : null;
}

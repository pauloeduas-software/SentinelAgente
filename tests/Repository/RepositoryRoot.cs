namespace SentinelAgente.Tests.Repository;

/// <summary>A raiz do repositório, achada subindo da pasta do teste até a solução.</summary>
internal static class RepositoryRoot
{
    public static string Path { get; } = Find();

    public static string Combine(string relative) => System.IO.Path.Combine(Path, relative);

    public static IEnumerable<string> Files(string folder, params string[] extensions) =>
        Directory.EnumerateFiles(Combine(folder), "*", SearchOption.AllDirectories)
            .Where(file => extensions.Contains(System.IO.Path.GetExtension(file)))
            .Where(file => !IsBuildOutput(file))
            .Select(file => System.IO.Path.GetRelativePath(Path, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);

    private static bool IsBuildOutput(string file) =>
        file.Split(System.IO.Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj");

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "SentinelAgente.slnx"))) return dir.FullName;
        }
        throw new InvalidOperationException("A raiz do repositório (SentinelAgente.slnx) não foi achada.");
    }
}

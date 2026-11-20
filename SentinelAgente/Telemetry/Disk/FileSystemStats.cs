namespace SentinelAgente.Telemetry.Disk;

/// <summary>O tamanho de um sistema de arquivos e o que está livre, contando a reserva do root.</summary>
internal readonly record struct FileSystemSpace(long TotalBytes, long FreeBytes);

/// <summary>O <c>statvfs</c> de um ponto de montagem; nos testes, um falso.</summary>
internal interface IFileSystemStats
{
    /// <summary>O espaço do ponto de montagem, ou <c>null</c> quando a leitura falha.</summary>
    FileSystemSpace? Read(string mountPoint);
}

/// <summary>
/// O <c>statvfs</c> pelo <see cref="DriveInfo"/>: o total são os blocos, e o
/// livre é o <c>f_bfree</c>, com a reserva do root, como o <c>df</c> conta o usado.
/// </summary>
internal sealed class DriveStats : IFileSystemStats
{
    public FileSystemSpace? Read(string mountPoint)
    {
        try
        {
            var drive = new DriveInfo(mountPoint);
            return new FileSystemSpace(drive.TotalSize, drive.TotalFreeSpace);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}

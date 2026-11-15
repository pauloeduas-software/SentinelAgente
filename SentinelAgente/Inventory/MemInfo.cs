namespace SentinelAgente.Inventory;

/// <summary>
/// A RAM total: o <c>MemTotal</c> do <c>/proc/meminfo</c> arredondado para cima
/// em GiB, em bytes. O <c>MemTotal</c> cru varia alguns kB com o kernel, e cada
/// variação viraria uma mudança de hardware (A14).
/// </summary>
/// <remarks>
/// Na VM com balão de memória (o <c>virtio_balloon</c> do Proxmox, a memória
/// dinâmica do Hyper-V), o <c>MemTotal</c> sobe e desce com o balão, e o
/// arredondamento não segura: cada GiB a mais ou a menos vira uma mudança.
/// </remarks>
internal static class MemInfo
{
    private const long GiB = 1024L * 1024 * 1024;

    public static long? Read(SystemRoot root)
    {
        foreach (var line in (root.ReadText("proc/meminfo") ?? "").Split('\n'))
        {
            if (!line.StartsWith("MemTotal:")) continue;

            var kilobytes = line["MemTotal:".Length..].Trim().Split(' ')[0];
            if (!long.TryParse(kilobytes, out var kb) || kb <= 0) return null;
            return (kb * 1024 + GiB - 1) / GiB * GiB;
        }
        return null;
    }
}

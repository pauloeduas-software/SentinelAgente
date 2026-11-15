using System.Text.RegularExpressions;
using SentinelAgente.Inventory;
using SentinelAgente.Protocol;

namespace SentinelAgente.Telemetry.Disk;

/// <summary>
/// Os discos um a um, pelo <c>/proc/mounts</c>: o nome é o ponto de montagem, o
/// total são os blocos e o usado é o que o <c>df</c> mostra (A16).
/// </summary>
/// <remarks>
/// Entra o que está num dispositivo local (<c>/dev/…</c>, ou um dataset do ZFS),
/// menos os tipos que o Telegraf ignora (<c>plugins/inputs/disk/sample.conf</c>).
/// Cada dispositivo vai uma vez, no primeiro ponto em que foi montado: o bind
/// mount e o subvolume do btrfs repetiriam o mesmo espaço. Compartilhamento de
/// rede (NFS, CIFS) não entra. O pendrive entra: a regra contra a oscilação é
/// do handshake (A14), e aqui os discos vão um a um.
/// </remarks>
internal sealed partial class DiskUsage(SystemRoot root, IFileSystemStats stats) : ITelemetryCollector
{
    private const double GiB = 1024.0 * 1024 * 1024;

    private static readonly HashSet<string> IgnoredTypes =
        ["tmpfs", "devtmpfs", "devfs", "iso9660", "overlay", "aufs", "squashfs"];

    public void Collect(TelemetrySample sample) => sample.Disks = Read();

    internal IReadOnlyList<DiskPayload> Read()
    {
        var disks = new List<DiskPayload>();
        var devices = new HashSet<string>();

        foreach (var line in (root.ReadText("proc/mounts") ?? "").Split('\n'))
        {
            var fields = line.Split(' ');
            if (fields.Length < 3) continue;

            var device = Unescape(fields[0]);
            var mountPoint = Unescape(fields[1]);
            var type = fields[2];

            if (!device.StartsWith("/dev/") && type != "zfs") continue;
            if (IgnoredTypes.Contains(type) || devices.Contains(device)) continue;
            if (stats.Read(mountPoint) is not { TotalBytes: > 0 } space) continue;

            devices.Add(device);
            disks.Add(new DiskPayload(mountPoint, Gb(space.TotalBytes), Gb(space.TotalBytes - space.FreeBytes)));
        }
        return disks;
    }

    private static double Gb(long bytes) => Math.Round(bytes / GiB, 2);

    /// <summary>O kernel escreve espaço, tab, quebra de linha e barra em octal (<c>\040</c>).</summary>
    private static string Unescape(string field) =>
        OctalEscape().Replace(field, match => ((char)Convert.ToInt32(match.Groups[1].Value, 8)).ToString());

    [GeneratedRegex(@"\\([0-7]{3})")]
    private static partial Regex OctalEscape();
}

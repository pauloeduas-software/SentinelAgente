using SentinelAgente.Protocol;
using SentinelAgente.Telemetry.Disk;

namespace SentinelAgente.Tests.Telemetry;

public sealed class DiskUsageTests
{
    private const long GiB = 1024L * 1024 * 1024;

    // Todo ponto de montagem responde: 100 GiB com 25 livres.
    private sealed class AnySpace : IFileSystemStats
    {
        public List<string> Asked { get; } = [];

        public FileSystemSpace? Read(string mountPoint)
        {
            Asked.Add(mountPoint);
            return new FileSystemSpace(100 * GiB, 25 * GiB);
        }
    }

    private static List<string> Names(string machine) =>
        new DiskUsage(Machines.Root(machine), new AnySpace()).Read().Select(disk => disk.Name).ToList();

    [Fact]
    public void Fica_so_o_disco_de_verdade_sem_snap_tmpfs_e_fuse()
    {
        Assert.Equal(["/", "/boot/efi"], Names("thinkpad"));
    }

    [Fact]
    public void Bind_mount_e_overlay_nao_entram_e_o_pendrive_sim()
    {
        Assert.Equal(["/", "/boot/efi", "/media/ana/PEN DRIVE"], Names("notebook-dell"));
    }

    [Fact]
    public void Subvolume_do_btrfs_vai_uma_vez_o_zfs_entra_e_o_nfs_nao()
    {
        Assert.Equal(["/", "/boot", "/dados", "/tank/vms"], Names("servidor-kvm"));
    }

    [Fact]
    public void O_cd_nao_entra()
    {
        Assert.Equal(["/"], Names("pc-montado"));
    }

    [Fact]
    public void O_usado_conta_a_reserva_do_root_como_o_df()
    {
        var disk = new DiskUsage(Machines.Root("pc-montado"), new AnySpace()).Read().Single();

        Assert.Equal(new DiskPayload("/", 100, 75), disk);
    }

    [Fact]
    public void Ponto_que_nao_responde_fica_de_fora()
    {
        var disks = new DiskUsage(Machines.Root("thinkpad"), new NoAnswer("/boot/efi")).Read();

        Assert.Equal(["/"], disks.Select(disk => disk.Name));
    }

    private sealed class NoAnswer(string silent) : IFileSystemStats
    {
        public FileSystemSpace? Read(string mountPoint) =>
            mountPoint == silent ? null : new FileSystemSpace(GiB, 0);
    }
}

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace SentinelAgente.Inventory;

/// <summary>
/// A memória das placas NVIDIA, que o driver fechado não põe no <c>/sys</c>: o
/// <c>nvidia-smi</c> que vem com ele, como o OCS Inventory faz
/// (<c>Backend/OS/Linux/Videos.pm</c>), e com o slot junto, para casar cada
/// placa com a do <c>/sys/bus/pci/devices</c> (A28).
/// </summary>
/// <remarks>
/// O executável é fixo e roda sem shell. Sem ele (o <c>nouveau</c>, ou o driver
/// fechado não instalado), a placa vai sem memória, como antes.
/// </remarks>
internal static class NvidiaSmi
{
    internal const string Program = "/usr/bin/nvidia-smi";

    /// <summary>O nvidia-smi costuma voltar em menos de um segundo; preso, a placa vai sem memória.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static readonly string[] Arguments =
        ["--query-gpu=pci.bus_id,memory.total", "--format=csv,noheader,nounits"];

    /// <summary>A memória em bytes, pelo slot do <c>/sys</c>; sem o nvidia-smi, vazio.</summary>
    public static IReadOnlyDictionary<string, long> Read() =>
        Run() is { } output ? Parse(output) : new Dictionary<string, long>();

    private static string? Run()
    {
        if (!File.Exists(Program)) return null;

        var start = new ProcessStartInfo(Program)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in Arguments) start.ArgumentList.Add(argument);
        start.Environment.Remove("NOTIFY_SOCKET");

        try
        {
            using var process = Process.Start(start);
            if (process is null) return null;

            // Os dois lidos junto com a espera: o cano cheio trava o processo.
            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(Timeout))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            return process.ExitCode == 0 ? output.GetAwaiter().GetResult() : null;
        }
        catch (Exception e) when (e is Win32Exception or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// "00000000:01:00.0, 8192" vira "0000:01:00.0" e 8 GiB: o nvidia-smi escreve o
    /// domínio com oito dígitos e a memória em MiB. A linha com <c>[N/A]</c> fica de fora.
    /// </summary>
    internal static Dictionary<string, long> Parse(string output)
    {
        var memory = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = line.Split(',', StringSplitOptions.TrimEntries);
            if (fields.Length != 2) continue;
            if (Slot(fields[0]) is not { } slot) continue;
            if (!long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var mebibytes) || mebibytes <= 0) continue;

            memory[slot] = mebibytes * 1024 * 1024;
        }
        return memory;
    }

    // "00000000:0A:00.0" → "0000:0a:00.0", a grafia do /sys.
    private static string? Slot(string busId)
    {
        var colon = busId.IndexOf(':');
        if (colon <= 0) return null;
        if (!int.TryParse(busId[..colon], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var domain)) return null;
        return $"{domain:x4}:{busId[(colon + 1)..].ToLowerInvariant()}";
    }
}

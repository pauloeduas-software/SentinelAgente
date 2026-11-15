using SentinelAgente.Protocol;
using SentinelAgente.Telemetry.Processes;

namespace SentinelAgente.Tests.Telemetry;

public sealed class TopProcessesTests
{
    private static IReadOnlyList<ProcessPayload> Read() => new TopProcesses(Machines.Root("thinkpad")).Read();

    [Fact]
    public void Vao_os_dez_de_mais_ram_do_maior_para_o_menor()
    {
        var processes = Read();

        Assert.Equal(
            [3121, 3388, 4502, 1890, 5120, 4630, 3502, 2950, 2210, 2876],
            processes.Select(process => process.Pid));
        Assert.Equal(new ProcessPayload(3121, "firefox", 1175.3), processes[0]);
    }

    [Fact]
    public void Mesmo_nome_nao_se_junta()
    {
        Assert.Equal(3, Read().Count(process => process.Name == "firefox"));
    }

    [Fact]
    public void Thread_de_kernel_nao_entra()
    {
        Assert.DoesNotContain(Read(), process => process.Name is "kthreadd" or "ksoftirqd/0" or "rcu_preempt");
    }
}

using SentinelAgente.Connection;

namespace SentinelAgente.Tests.Connection;

public sealed class EnrollmentTests
{
    [Theory]
    [InlineData("wss://sentinel.empresa.com/agent-hub", "https://sentinel.empresa.com/agent-hub/enroll")]
    [InlineData("wss://sentinel.empresa.com:8443/agent-hub/", "https://sentinel.empresa.com:8443/agent-hub/enroll")]
    [InlineData("ws://localhost:3001/agent-hub", "http://localhost:3001/agent-hub/enroll")]
    public void O_cadastro_fica_ao_lado_do_hub_com_a_mesma_criptografia(string hub, string enroll)
    {
        Assert.Equal(new Uri(enroll), Enrollment.EnrollUrl(new Uri(hub)));
    }
}

using Microsoft.Extensions.Options;
using SentinelAgente.Config;

namespace SentinelAgente.Connection;

public static class ConnectionRegistration
{
    /// <summary>
    /// Registra a conexão com o SentinelWeb e a seção <c>Server</c> da
    /// configuração, conferida na subida. Pede um <see cref="IHandshakeSource"/>
    /// e um <see cref="ICommandHandler"/> registrados por quem os implementa.
    /// </summary>
    public static IServiceCollection AddServerConnection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ServerOptions>()
            .Bind(configuration.GetSection(ServerOptions.Section))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ServerOptions>, ServerOptionsValidator>();

        services.AddSingleton(new ConnectionTimings());
        services.AddSingleton<AgentConnection>();
        services.AddSingleton<IServerLink>(provider => provider.GetRequiredService<AgentConnection>());
        services.AddHostedService(provider => provider.GetRequiredService<AgentConnection>());
        return services;
    }
}

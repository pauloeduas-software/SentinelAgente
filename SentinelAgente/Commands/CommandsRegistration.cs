using SentinelAgente.Connection;

namespace SentinelAgente.Commands;

public static class CommandsRegistration
{
    /// <summary>Registra a lista fechada como quem recebe os comandos de sistema; o <c>REFRESH</c> fica na conexão (A26).</summary>
    public static IServiceCollection AddCommands(this IServiceCollection services) =>
        services.AddSingleton<ICommandHandler>(provider => new PowerCommands(
            new ProcessRunner(),
            PowerCommands.DefaultTimeout,
            provider.GetRequiredService<ILogger<PowerCommands>>()));
}

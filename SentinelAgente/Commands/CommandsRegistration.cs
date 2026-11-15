using SentinelAgente.Connection;

namespace SentinelAgente.Commands;

public static class CommandsRegistration
{
    /// <summary>Registra a lista fechada de comandos como quem recebe o que o servidor manda.</summary>
    public static IServiceCollection AddCommands(this IServiceCollection services) =>
        services.AddSingleton<ICommandHandler>(provider => new PowerCommands(
            new ProcessRunner(),
            PowerCommands.DefaultTimeout,
            provider.GetRequiredService<ILogger<PowerCommands>>()));
}

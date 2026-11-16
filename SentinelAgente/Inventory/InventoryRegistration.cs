using SentinelAgente.Connection;

namespace SentinelAgente.Inventory;

public static class InventoryRegistration
{
    /// <summary>
    /// Registra a ficha da máquina como a apresentação que a conexão manda. A
    /// mesma instância guarda o HWID, para quem mais o manda.
    /// </summary>
    public static IServiceCollection AddInventory(this IServiceCollection services) =>
        services
            .AddSingleton(_ => new MachineInventory())
            .AddSingleton<IHandshakeSource>(provider => provider.GetRequiredService<MachineInventory>());
}

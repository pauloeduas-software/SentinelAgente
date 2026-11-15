using SentinelAgente.Connection;

namespace SentinelAgente.Inventory;

public static class InventoryRegistration
{
    /// <summary>Registra a ficha da máquina como a apresentação que a conexão manda.</summary>
    public static IServiceCollection AddInventory(this IServiceCollection services) =>
        services.AddSingleton<IHandshakeSource, MachineInventory>();
}

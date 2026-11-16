using SentinelAgente.Connection;
using SentinelAgente.Software;

namespace SentinelAgente.Inventory;

public static class InventoryRegistration
{
    /// <summary>
    /// Registra a ficha da máquina como a apresentação que a conexão manda. A
    /// mesma instância guarda o HWID, para quem mais o manda. Pede a lista de
    /// programas do <c>Software</c>.
    /// </summary>
    public static IServiceCollection AddInventory(this IServiceCollection services) =>
        services
            .AddSingleton(provider => new MachineInventory(provider.GetRequiredService<InstalledSoftware>()))
            .AddSingleton<IHandshakeSource>(provider => provider.GetRequiredService<MachineInventory>());
}

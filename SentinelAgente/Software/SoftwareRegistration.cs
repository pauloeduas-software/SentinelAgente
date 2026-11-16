namespace SentinelAgente.Software;

public static class SoftwareRegistration
{
    /// <summary>Registra a lista de programas, que o <c>Inventory</c> põe no handshake.</summary>
    public static IServiceCollection AddSoftware(this IServiceCollection services) =>
        services
            .AddSingleton<IPackageQuery, ProcessPackageQuery>()
            .AddSingleton<InstalledSoftware>();
}

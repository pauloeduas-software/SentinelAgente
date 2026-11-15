using SentinelAgente.Commands;
using SentinelAgente.Inventory;

// O PROCESSO: liga o host e a integração com o systemd (pronto, parada e log no
// journal). Cada pasta de função se registra aqui quando ganhar código; fora
// isso, este arquivo não tem regra nenhuma.

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSystemd();
builder.Services.AddInventory();
builder.Services.AddCommands();

builder.Build().Run();

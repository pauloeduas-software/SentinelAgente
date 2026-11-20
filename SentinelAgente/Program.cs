using SentinelAgente.Commands;
using SentinelAgente.Connection;
using SentinelAgente.Inventory;
using SentinelAgente.Telemetry;

// O PROCESSO: liga o host, a configuração e a integração com o systemd (pronto,
// parada e log no journal). A conexão e cada função se registram aqui (o
// Identity entra pelo Inventory); fora isso, este arquivo não tem regra nenhuma.

var builder = Host.CreateApplicationBuilder(args);

// O que a instalação escreve (A11). Opcional aqui: sem ele, quem confere é o
// ServerOptionsValidator, que diz no journal qual campo falta. Em
// desenvolvimento, a linha de comando faz o papel dele (--Server:Url=…).
builder.Configuration.AddJsonFile("/etc/sentinel-agent/agent.json", optional: true, reloadOnChange: false);
builder.Configuration.AddCommandLine(args);

builder.Services.AddSystemd();
builder.Services.AddServerConnection(builder.Configuration);
builder.Services.AddInventory();
builder.Services.AddTelemetry();
builder.Services.AddCommands();

builder.Build().Run();

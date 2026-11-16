using NerveTcp;

// Usage:
//   dotnet run -- server [--config config.json] [--host H] [--port P] [--passphrase S] [--interval SEC]
//   dotnet run -- client [SetA-Two] [--config config.json] [--host H] [--port P] [--passphrase S]

if (args.Length == 0 || (args[0] != "server" && args[0] != "client"))
{
    Console.WriteLine("Usage: NerveTcp <server|client> [request] [--config path] [--host H] [--port P] [--passphrase S] [--interval SEC]");
    return 1;
}

string mode = args[0];
string configPath = "config.json";
string? host = null, passphrase = null, request = null;
int? port = null;
double? interval = null;

for (int i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--config": configPath = args[++i]; break;
        case "--host": host = args[++i]; break;
        case "--port": port = int.Parse(args[++i]); break;
        case "--passphrase": passphrase = args[++i]; break;
        case "--interval": interval = double.Parse(args[++i]); break;
        default: request = args[i]; break;
    }
}

AppConfig cfg = AppConfig.Load(configPath);
if (host is not null) cfg.Host = host;
if (port is not null) cfg.Port = port.Value;
if (passphrase is not null) cfg.Passphrase = passphrase;
if (interval is not null) cfg.Interval = interval.Value;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

if (mode == "server")
    await TimeServer.RunAsync(cfg, cts.Token);
else
    await TimeClient.RunAsync(cfg, request, cts.Token);

return 0;

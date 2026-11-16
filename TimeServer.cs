using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace NerveTcp;

public static class TimeServer
{
    public const string TimeFormat = "dd-MM-yyyy HH:mm:ss";

    public static async Task RunAsync(AppConfig cfg, CancellationToken ct)
    {
        var channel = new SecureChannel(cfg.Passphrase, cfg.Salt);
        var ip = IPAddress.TryParse(cfg.Host, out var parsed) ? parsed : IPAddress.Any;
        var listener = new TcpListener(ip, cfg.Port);
        listener.Start();
        Console.WriteLine($"Server listening on {ip}:{cfg.Port} (Ctrl+C to stop)");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(ct);
                // One task per client => many clients can be served at once.
                _ = Task.Run(() => HandleClientAsync(client, cfg, channel, ct), ct);
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
        finally
        {
            listener.Stop();
            Console.WriteLine("Server stopped.");
        }
    }

    private static async Task HandleClientAsync(TcpClient client, AppConfig cfg, SecureChannel channel, CancellationToken ct)
    {
        var remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        Console.WriteLine($"[+] {remote} connected");

        try
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();

                string? request = await channel.ReceiveAsync(stream, ct);
                if (request is null) return;
                Console.WriteLine($"[{remote}] request: \"{request}\"");

                int? count = Lookup(cfg.Collection, request);
                if (count is null)
                {
                    await channel.SendAsync(stream, "EMPTY", ct);
                    return;
                }

                for (int i = 0; i < count.Value; i++)
                {
                    string now = DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture);
                    await channel.SendAsync(stream, now, ct);
                    if (i < count.Value - 1)
                        await Task.Delay(TimeSpan.FromSeconds(cfg.Interval), ct);
                }
            }
        }
        catch (CryptographicException)
        {
            Console.WriteLine($"[!] {remote} sent data that could not be decrypted (wrong passphrase?)");
        }
        catch (Exception ex) when (ex is IOException or SocketException or InvalidDataException)
        {
            Console.WriteLine($"[!] {remote} connection error: {ex.Message}");
        }
        catch (OperationCanceledException) { }
        finally
        {
            Console.WriteLine($"[-] {remote} disconnected");
        }
    }

    /// <summary>"SetA-Two" -> 2, or null if the set or key isn't in the collection.</summary>
    private static int? Lookup(Dictionary<string, List<Dictionary<string, int>>> collection, string request)
    {
        int dash = request.IndexOf('-');
        if (dash <= 0) return null;

        string setName = request[..dash].Trim();
        string key = request[(dash + 1)..].Trim();

        if (!collection.TryGetValue(setName, out var subset)) return null;

        foreach (var entry in subset)
            if (entry.TryGetValue(key, out int value))
                return value;

        return null;
    }
}

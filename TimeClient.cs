using System.Net.Sockets;
using System.Security.Cryptography;

namespace NerveTcp;

public static class TimeClient
{
    public static async Task RunAsync(AppConfig cfg, string? request, CancellationToken ct)
    {
        var channel = new SecureChannel(cfg.Passphrase, cfg.Salt);

        try
        {
            if (!string.IsNullOrWhiteSpace(request))
            {
                await SendRequestAsync(cfg, channel, request, ct);
                return;
            }

            while (!ct.IsCancellationRequested)
            {
                Console.Write("Enter string (e.g. SetA-Two, blank to quit): ");
                string? line = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(line)) break;
                await SendRequestAsync(cfg, channel, line, ct);
            }
        }
        catch (CryptographicException)
        {
            Console.WriteLine("Could not decrypt server response (passphrase mismatch?)");
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
        }
        catch (OperationCanceledException) { }
    }

    private static async Task SendRequestAsync(AppConfig cfg, SecureChannel channel, string request, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(cfg.Host, cfg.Port, ct);
        NetworkStream stream = client.GetStream();

        await channel.SendAsync(stream, request, ct);

        // The server closes the connection after its last message, so ReceiveAsync returns null at the end.
        while (await channel.ReceiveAsync(stream, ct) is { } message)
            Console.WriteLine(message);
    }
}

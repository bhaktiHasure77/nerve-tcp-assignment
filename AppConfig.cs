using System.Text.Json;

namespace NerveTcp;

public class AppConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5000;
    public string Passphrase { get; set; } = "change-me";
    public string Salt { get; set; } = "nerve-assignment-salt";
    public double Interval { get; set; } = 1.0;
    public Dictionary<string, List<Dictionary<string, int>>> Collection { get; set; } = new();

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            var alt = Path.Combine(AppContext.BaseDirectory, path);
            if (File.Exists(alt)) path = alt;
            else throw new FileNotFoundException($"Config file not found: {path}");
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), options)
               ?? throw new InvalidDataException("Config file is empty or invalid.");
    }
}

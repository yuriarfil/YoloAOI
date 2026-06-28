using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using YoloAOIApp.Models;

namespace YoloAOIApp.Services;

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public RayConfig? LoadConfig(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
                return null;

            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<RayConfig>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public bool SaveConfig(string configPath, RayConfig config)
    {
        try
        {
            var directory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(configPath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

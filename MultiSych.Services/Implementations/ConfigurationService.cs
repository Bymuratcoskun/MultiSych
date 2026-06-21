using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;

namespace MultiSych.Services.Implementations;

public partial class ConfigurationService : IConfigurationService
{
    private static readonly string EnvPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public Task SaveSettingAsync(string key, string value)
    {
        return SaveSettingsAsync(new Dictionary<string, string> { { key, value } });
    }

    public async Task SaveSettingsAsync(Dictionary<string, string> settings)
    {
        await FileLock.WaitAsync();
        try
        {
            var lines = File.Exists(EnvPath) ? (await File.ReadAllLinesAsync(EnvPath)).ToList() : new List<string>();

            foreach (var (key, value) in settings)
            {
                var index = lines.FindIndex(l => l.StartsWith(key + "="));
                if (index >= 0)
                    lines[index] = $"{key}={value}";
                else
                    lines.Add($"{key}={value}");
            }

            await File.WriteAllLinesAsync(EnvPath, lines);
        }
        finally
        {
            FileLock.Release();
        }
    }
}
public partial class ConfigurationService : IConfigurationServiceExtended
{
    public string GetString(string key, string defaultValue = "")
    {
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(envPath))
            return defaultValue;

        try
        {
            var lines = File.ReadAllLines(envPath);
            var line = lines.FirstOrDefault(l => l.StartsWith(key + "="));
            if (line == null)
                return defaultValue;

            var parts = line.Split('=', 2);
            return parts.Length == 2 ? parts[1] : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task<string> GetStringAsync(string key, string defaultValue = "")
    {
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(envPath))
            return defaultValue;

        try
        {
            var lines = await File.ReadAllLinesAsync(envPath);
            var line = lines.FirstOrDefault(l => l.StartsWith(key + "="));
            if (line == null)
                return defaultValue;

            var parts = line.Split('=', 2);
            return parts.Length == 2 ? parts[1] : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        var stringValue = GetString(key);
        return int.TryParse(stringValue, out var result) ? result : defaultValue;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        var stringValue = GetString(key);
        return bool.TryParse(stringValue, out var result) ? result : defaultValue;
    }
}

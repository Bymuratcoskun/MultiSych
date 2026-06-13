using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MultiSych.Services.Interfaces;

namespace MultiSych.Services.Implementations;

public class ConfigurationService : IConfigurationService
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
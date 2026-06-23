using System.Collections.Generic;
using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces;

public interface IConfigurationService
{
    Task SaveSettingAsync(string key, string value);
    Task SaveSettingsAsync(Dictionary<string, string> settings);
}
public interface IConfigurationServiceExtended : IConfigurationService
{
    string GetString(string key, string defaultValue = "");
    Task<string> GetStringAsync(string key, string defaultValue = "");
    int GetInt(string key, int defaultValue = 0);
    bool GetBool(string key, bool defaultValue = false);
}

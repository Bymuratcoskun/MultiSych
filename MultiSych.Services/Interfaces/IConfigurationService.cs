using System.Collections.Generic;
using System.Threading.Tasks;

namespace MultiSych.Services.Interfaces;

public interface IConfigurationService
{
    Task SaveSettingAsync(string key, string value);
    Task SaveSettingsAsync(Dictionary<string, string> settings);
}
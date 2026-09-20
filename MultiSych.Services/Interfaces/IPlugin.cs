using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace MultiSych.Services.Interfaces;

public interface IPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Description { get; }
    string Author { get; }

    /// <summary>Plugin DI kaydı için. Servisleri buraya ekleyin.</summary>
    void ConfigureServices(IServiceCollection services);

    Task InitializeAsync(IServiceProvider services, CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
}

public interface IPluginLoader
{
    IPlugin[] LoadedPlugins { get; }
    Task LoadPluginsFromDirectoryAsync(string directory, CancellationToken ct = default);
    Task UnloadPluginAsync(string pluginId, CancellationToken ct = default);
}

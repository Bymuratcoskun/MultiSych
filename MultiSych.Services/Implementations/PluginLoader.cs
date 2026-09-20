using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MultiSych.Services.Interfaces;
using Serilog;

namespace MultiSych.Services.Implementations;

public sealed class PluginLoader : IPluginLoader, IAsyncDisposable
{
    private readonly ILogger _logger = Log.ForContext<PluginLoader>();
    private readonly IServiceProvider _appServices;
    private readonly ConcurrentDictionary<string, LoadedPlugin> _plugins = new();

    public IPlugin[] LoadedPlugins => _plugins.Values.Select(p => p.Instance).ToArray();

    public PluginLoader(IServiceProvider appServices)
    {
        _appServices = appServices;
    }

    public async Task LoadPluginsFromDirectoryAsync(string directory, CancellationToken ct = default)
    {
        if (!Directory.Exists(directory))
        {
            _logger.Information("Plugin directory not found: {Dir}", directory);
            return;
        }

        var dlls = Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly);
        foreach (var dll in dlls)
        {
            ct.ThrowIfCancellationRequested();
            await TryLoadPluginAssemblyAsync(dll, ct);
        }
    }

    public async Task UnloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        if (!_plugins.TryRemove(pluginId, out var loaded)) return;
        try
        {
            await loaded.Instance.ShutdownAsync(ct);
            loaded.Context.Unload();
            _logger.Information("Plugin unloaded: {Id}", pluginId);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error shutting down plugin {Id}", pluginId);
        }
    }

    private async Task TryLoadPluginAssemblyAsync(string dllPath, CancellationToken ct)
    {
        try
        {
            var context = new PluginAssemblyContext(dllPath);
            var assembly = context.LoadFromAssemblyPath(dllPath);

            var pluginTypes = assembly.GetExportedTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .ToList();

            if (pluginTypes.Count == 0)
            {
                _logger.Debug("No IPlugin implementations found in {Dll}", Path.GetFileName(dllPath));
                context.Unload();
                return;
            }

            foreach (var pluginType in pluginTypes)
            {
                ct.ThrowIfCancellationRequested();

                var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;

                if (_plugins.ContainsKey(plugin.Id))
                {
                    _logger.Warning("Plugin {Id} already loaded, skipping duplicate from {Dll}", plugin.Id, dllPath);
                    continue;
                }

                // Plugin kendi servislerini kaydetmek istiyorsa uydu DI container
                var pluginServices = new ServiceCollection();
                plugin.ConfigureServices(pluginServices);

                await plugin.InitializeAsync(_appServices, ct);
                _plugins[plugin.Id] = new LoadedPlugin(plugin, context);
                _logger.Information("Plugin loaded: {Name} v{Version} by {Author}", plugin.Name, plugin.Version, plugin.Author);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load plugin assembly {Dll}", Path.GetFileName(dllPath));
        }
    }

    public async ValueTask DisposeAsync()
    {
        var ids = _plugins.Keys.ToList();
        foreach (var id in ids)
            await UnloadPluginAsync(id);
    }

    private sealed record LoadedPlugin(IPlugin Instance, PluginAssemblyContext Context);

    private sealed class PluginAssemblyContext(string mainAssemblyPath)
        : AssemblyLoadContext(Path.GetFileNameWithoutExtension(mainAssemblyPath), isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(mainAssemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var resolved = _resolver.ResolveAssemblyToPath(assemblyName);
            return resolved != null ? LoadFromAssemblyPath(resolved) : null;
        }
    }
}

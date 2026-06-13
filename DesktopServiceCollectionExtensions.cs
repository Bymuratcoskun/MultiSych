using Microsoft.Extensions.DependencyInjection;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Configuration;

public static class DesktopServiceCollectionExtensions
{
    public static IServiceCollection AddMultiSychViewModels(this IServiceCollection services)
    {
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<AccountsViewModel>();
        services.AddSingleton<SyncViewModel>();
        services.AddSingleton<AIOverviewViewModel>();
        services.AddSingleton<DocumentAnalyzerViewModel>();
        services.AddSingleton<ErrorReportViewModel>();
        services.AddSingleton<SettingsViewModel>();
        
        return services;
    }
}

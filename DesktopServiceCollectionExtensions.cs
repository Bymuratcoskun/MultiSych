using Microsoft.Extensions.DependencyInjection;
using MultiSych.Desktop.ViewModels;

namespace MultiSych.Desktop.Configuration;

public static class DesktopServiceCollectionExtensions
{
    public static IServiceCollection AddMultiSychViewModels(this IServiceCollection services)
    {
        // ViewModels: Transient - her binding'de yenisi oluştur
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<AccountsViewModel>();
        services.AddTransient<AddAccountViewModel>();
        services.AddTransient<SyncViewModel>();
        services.AddTransient<FileExplorerViewModel>();
        services.AddTransient<AIOverviewViewModel>();
        services.AddTransient<DocumentAnalyzerViewModel>();
        services.AddTransient<ErrorReportViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<AIChatViewModel>();
        
        return services;
    }
}

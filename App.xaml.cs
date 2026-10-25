using System.Windows;
using AIGamingOptimizer.Models;
using AIGamingOptimizer.Services;
using AIGamingOptimizer.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AIGamingOptimizer;

public partial class App : Application
{
    private IHost? _host;

    public static void Ui(Action action)
    {
        Current.Dispatcher.Invoke(action);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.SetBasePath(AppContext.BaseDirectory);
                cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                cfg.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
                cfg.AddEnvironmentVariables();
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<AiOptions>(ctx.Configuration.GetSection("Ai"));
                services.AddHttpClient<AiAssistantService>(client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(90);
                });
                services.AddSingleton<GpuMetricsService>();
                services.AddSingleton<HardwareInfoService>();
                services.AddSingleton<PowerPlanService>();
                services.AddSingleton<ProcessOptimizerService>();
                services.AddSingleton<CleanupService>();
                services.AddSingleton<StartupService>();
                services.AddSingleton<SystemMetricsService>();
                services.AddSingleton<SystemToolService>();
                services.AddSingleton<OverviewViewModel>();
                services.AddSingleton<GameBoostViewModel>();
                services.AddSingleton<CleanerViewModel>();
                services.AddSingleton<StartupViewModel>();
                services.AddSingleton<AssistantViewModel>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}

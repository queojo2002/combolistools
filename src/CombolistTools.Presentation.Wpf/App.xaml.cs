using CombolistTools.Application;
using CombolistTools.Infrastructure;
using CombolistTools.Presentation.Wpf.Services;
using CombolistTools.Presentation.Wpf.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace CombolistTools.Presentation.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder(e.Args)
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddApplication();
                services.AddInfrastructure(ctx.Configuration);
                services.AddSingleton<IUiLogSink, UiLogSink>();
                services.AddSingleton<IJobExecutionService, JobExecutionService>();
                services.AddSingleton<IPathPickerService, PathPickerService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.DataContext = _host.Services.GetRequiredService<MainViewModel>();
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


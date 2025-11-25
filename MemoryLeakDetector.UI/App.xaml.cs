using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MemoryLeakDetector.Core.Options;
using MemoryLeakDetector.UI.Services.Data;
using MemoryLeakDetector.UI.Services.Monitoring;
using MemoryLeakDetector.UI.ViewModels;
using System.Windows;

namespace MemoryLeakDetector.UI
{
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();
        }

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            services.Configure<MonitoringPipeOptions>(context.Configuration.GetSection("MonitoringPipe"));
            services.AddSingleton<IMonitoringResultSubscriber, NamedPipeMonitoringResultSubscriber>();
            services.AddSingleton<StreamProcessDataProvider>();
            services.AddSingleton<IProcessDataProvider>(provider => provider.GetRequiredService<StreamProcessDataProvider>());
            services.AddHostedService<MonitoringResultListener>();

            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<ProcessesViewModel>();
            services.AddSingleton<ShellViewModel>();

            services.AddSingleton<MainWindow>(provider => new MainWindow
            {
                DataContext = provider.GetRequiredService<ShellViewModel>()
            });
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();

            base.OnExit(e);
        }
    }
}

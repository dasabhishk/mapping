using System.Windows;
using CMMT.Services;
using CMMT.UI;
using CMMT.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace CMMT
{
    public partial class App : Application
    {
        private IHost? _host;

        public IServiceProvider Services
        {
            get
            {
                if (_host?.Services == null)
                {
                    var message = "Services not initialized. Application may not have started properly.";
                    LoggingService.LogInfo("Service not initialized. Application may not have started properly");
                    throw new InvalidOperationException(message);
                }
                return _host.Services;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(cfg =>
                {
                    cfg.AddJsonFile("appsettings.json", optional: true);
                    cfg.AddJsonFile("schemaconfig.json", optional: true);
                })
                .UseSerilog((ctx, lc) => lc
                    .MinimumLevel.Override("Quartz", Serilog.Events.LogEventLevel.Warning)
                    .MinimumLevel.Override("Quartz.Impl", Serilog.Events.LogEventLevel.Warning)
                    .MinimumLevel.Override("Quartz.Impl", Serilog.Events.LogEventLevel.Warning)
                    .WriteTo.File("logs\\cmmt-.log", rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true, fileSizeLimitBytes: 1073741824))


                .ConfigureServices((ctx, services) =>
                {

                    // Core Migration Services
                    services.AddSingleton<DatabaseService>();
                    services.AddSingleton<ImportManager>();
                    services.AddSingleton<SchedulerService>();
                    services.AddSingleton<MigrationManager>();

                    // CSV & Mapping Services
                    services.AddSingleton<ICsvParserService, CsvParserService>();
                    services.AddSingleton<ISchemaLoaderService, SchemaLoaderService>();
                    services.AddSingleton<ITransformationViewService, TransformationViewService>();
                    services.AddSingleton<IMappingService>(sp =>
                        new MappingService(sp.GetRequiredService<ITransformationViewService>()));

                    // ViewModels 
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<CsvMappingTypeViewModel>();
                    services.AddTransient<ColumnMappingViewModel>();
                    services.AddTransient<ProcessCSVViewModel>();
                    services.AddTransient<MigrationViewModel>();

                    // Views 
                    services.AddSingleton<MainWindow>();
                    services.AddTransient<MappingView>();
                    services.AddTransient<StagingPage>();
                    services.AddTransient<ProcessCSV>();
                    services.AddTransient<TargetDatabasePage>();
                    services.AddTransient<Migration>();
                    services.AddTransient<ReportingPage>();
                    services.AddTransient<SettingsPage>();
                })
                .Build();

                _host.Start();

                // Initialize scheduler service in background after host is started
                _ = Task.Run(async () => await InitializeSchedulerServiceAsync());

                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
                mainWindow.DataContext = mainViewModel;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Critical error during application startup: {ex.Message}\n\nThe application will now exit.", ex, showMsgBox: true);
                Environment.Exit(1);
            }
        }

        private async Task InitializeSchedulerServiceAsync()
        {
            try
            {
                LoggingService.LogInfo("App: Starting background scheduler initialization...");

                var schedulerService = _host?.Services.GetService<SchedulerService>();
                if (schedulerService == null)
                {
                    LoggingService.LogError("App: SchedulerService not found in DI container", null);
                    return;
                }

                bool initialized = await schedulerService.InitializeAsync();
                if (initialized)
                {
                    LoggingService.LogInfo("App: Scheduler service initialized successfully");
                }
                else
                {
                    LoggingService.LogError("App: Failed to initialize scheduler service", null);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"App: Error during scheduler initialization: {ex.Message}", ex);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Gracefully shutdown scheduler if initialized
                var schedulerService = _host?.Services.GetService<SchedulerService>();
                if (schedulerService?.IsInitialized == true)
                {
                    LoggingService.LogInfo("App: Shutting down scheduler service...");
                    // Use Task.Run to avoid blocking the UI thread during shutdown
                    Task.Run(async () => await schedulerService.StopAsync()).Wait(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"App: Error during scheduler shutdown: {ex.Message}", ex);
            }
            finally
            {
                _host?.Dispose();
                base.OnExit(e);
            }
        }
    }
}
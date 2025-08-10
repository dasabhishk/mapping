using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CMMT.Services;
using CMMT.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CMMT.UI
{
    public partial class MainWindow : Window
    {
   

        public MainWindow()
        {
            InitializeComponent();
     

            Loaded += async (s, e) =>
            {
                if (DataContext is MainViewModel viewModel)
                {
                    try
                    {
                        LoggingService.LogInfo("Initializing MainViewModel...");
                        await viewModel.InitializeCommand.ExecuteAsync(null);
                        LoggingService.LogInfo("Initialization complete. Navigating to TargetDB view.");
                        await NavigateToPage("TargetDB");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError("Error during initialization", ex);
                    }
                }
            };
        }

        private async void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pageName)
            {
                LoggingService.LogInfo($"Navigation button clicked: {pageName}");
                await NavigateToPage(pageName);
                HighlightActiveButton(btn);
            }
        }

        public async Task NavigateToPage(string pageName)
        {
            try
            {
                if (Application.Current is not App app)
                {
                    LoggingService.LogInfo("Application not properly initialized");
                    return;
                }

                Page? page = pageName switch
                {
                    "TargetDB" => app.Services.GetService<TargetDatabasePage>(),
                    "Mapping" => app.Services.GetService<MappingView>(),
                    "StagingPage" => app.Services.GetService<StagingPage>(),
                    "ProcessCSV" => app.Services.GetService<ProcessCSV>(),
                    "Migration" => app.Services.GetService<Migration>(),
                    "Report" => app.Services?.GetService<ReportingPage>(),
                    "Settings" => app.Services?.GetService<SettingsPage>(),
                    _ => null
                };

                if (page == null)
                {
                    LoggingService.LogWarning($"Navigation failed unknown page: {pageName}");
                    return;
                }

                //  DataContext for MappingView 
                if (page is MappingView)
                {
                    page.DataContext = DataContext;
                    await Task.Delay(10); 
                }
                MainFrame.Navigate(page);
                LoggingService.LogInfo($"Navigation to {pageName} successful.");
            }
            catch (InvalidOperationException ex)
            {
                LoggingService.LogError("Service resolution error during navigation", ex);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Unexpected error during navigation", ex);
            }
        }

        private void HighlightActiveButton(Button activeButton)
        {
            try
            {
                if (activeButton.Parent is Panel panel)
                {
                    foreach (var child in panel.Children)
                    {
                        if (child is Button btn)
                        {
                            btn.Background = Brushes.Transparent;
                        }
                    }
                    activeButton.Background = new SolidColorBrush(Color.FromRgb(62, 75, 94));
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"HighlightActiveButton failed: {ex.Message}");
                LoggingService.LogError("Error highlighting button",ex);
            }
        }
    }
}
using System.Windows;
using CMMT.Services;
using CMMT.Models;
using CMMT.UI;
using CMMT.Helpers;

namespace CMMT.UI
{
    public class TargetDatabasePage : StagingPage
    {
        public TargetDatabasePage()
        {
            InitializeComponent();

            // Hide the Create button
            if (btnCreateDatabase != null)
                btnCreateDatabase.Visibility = Visibility.Collapsed;

            // Update labels
            if (lblDatabaseList != null)
                lblDatabaseList.Text = "Target Database List";

        }

        protected override async void LoadStagingDBConfig()
        {
            try
            {
                var config = await ConfigFileService.LoadInitialConfig();
                if (config?.TargetDatabase == null) return;

                SetUiFromConfig(config.TargetDatabase);
                LoggingService.LogInfo("Loaded target database config", false);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to load target database config", ex, showMsgBox: true);
            }
        }


        protected override async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedDatabase = cmbDatabaseName.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selectedDatabase))
            {
                LoggingService.LogWarning("Please select a database.", true);
                return;
            }

            try
            {
                DatabaseConfig config = new DatabaseConfig
                {
                    TargetDatabase = new DbConnectionInfo
                          {
                             Server = _currentServerInstance,
                             Authentication = _currentAuthType,
                             User = _currentAuthType == "Sql" ? _currentUsername : "",
                             Database = selectedDatabase,
                             EncryptedPassword = _currentAuthType == "Sql" ? SecureStringHelper.Encrypt(_currentPassword) : "",
                             EncryptedConnectionString = SecureStringHelper.Encrypt(_latestConnectionString + $"Database={selectedDatabase}")
                          }
                };
                await ConfigFileService.SaveDatabaseConfig(config);
                LoggingService.LogInfo("Config Saved Succesfully.", true);

            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to save target config", ex, true);
            }
        }

        protected override async Task RefreshDatabaseList()
        {
            try
            {
                var databases = await DatabaseService.LoadTargetDatabases(_latestConnectionString);
                cmbDatabaseName.ItemsSource = databases;
                btnSaveConfig.IsEnabled = false;

                if (!string.IsNullOrWhiteSpace(_targetDatabaseName) && databases.Contains(_targetDatabaseName))
                {
                    cmbDatabaseName.SelectedItem = _targetDatabaseName;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to load databases", ex, true);
            }
        }

    }
}

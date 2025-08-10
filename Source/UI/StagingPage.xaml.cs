using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using CMMT.dao;
using CMMT.Helpers;
using CMMT.Models;
using CMMT.Services;

namespace CMMT.UI
{
    public partial class StagingPage : Page
    {
        protected string _currentServerInstance = "";
        protected string _currentAuthType = "Windows";
        protected string _currentUsername = "";
        protected string _currentPassword = "";
        protected string _latestConnectionString = "";
        protected string _targetDatabaseName = "";

        public StagingPage()
        {
            InitializeComponent();
            btnCreateDatabase.IsEnabled = false;
            btnSaveConfig.IsEnabled = false;
            cmbDatabaseName.SelectionChanged += DatabaseNameComboBox_SelectionChanged;

            LoadStagingDBConfig();
        }

        protected virtual async void LoadStagingDBConfig()
        {
            try
            {
                var config = await ConfigFileService.LoadInitialConfig();
                if (config?.StagingDatabase == null) return;
                SetUiFromConfig(config.StagingDatabase);
                LoggingService.LogInfo("loaded staging database config", false);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error loading staging database config", ex, true);
            }
        }

        protected void SetUiFromConfig(DbConnectionInfo db)
        {
            if (!string.IsNullOrEmpty(db.Server))
            {
                string[] serverParts = db.Server.Split('\\');
                if (serverParts.Length == 2)
                {
                    txtServerPrefix.Text = serverParts[0];
                    txtServerName.Text = serverParts[1];
                }
                else
                {
                    txtServerPrefix.Text = db.Server;
                }
            }
            _currentServerInstance = db.Server;
            _currentAuthType = db.Authentication ?? "Windows";
            _currentUsername = db.User ?? "";
            _currentPassword = db.EncryptedPassword ?? "";
            _targetDatabaseName = db.Database ?? "";

            cmbAuthType.SelectedIndex = _currentAuthType == "Sql" ? 1 : 0;
            txtUsername.Text = _currentUsername;
            txtPassword.Password = _currentPassword;

        }

        private void AuthTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (txtUsername == null || txtPassword == null)
                return;

            var selectedItem = cmbAuthType.SelectedItem as ComboBoxItem;
            bool isSqlAuth = selectedItem?.Content.ToString() == "SQL Server Authentication";

            txtUsername.IsEnabled = isSqlAuth;
            txtPassword.IsEnabled = isSqlAuth;

            txtUsername.Background = isSqlAuth ? Brushes.White : Brushes.LightGray;
            txtPassword.Background = isSqlAuth ? Brushes.White : Brushes.LightGray;

            asteriskUsername.Visibility = isSqlAuth ? Visibility.Visible : Visibility.Collapsed;
            asteriskPassword.Visibility = isSqlAuth ? Visibility.Visible : Visibility.Collapsed;

            ResetDatabaseUI();

        }

        private void DatabaseNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnSaveConfig.IsEnabled = cmbDatabaseName.SelectedItem != null;
        }

        private void CaptureServerCredentials()
        {
            _currentServerInstance = $"{txtServerPrefix.Text.Trim()}\\{txtServerName.Text.Trim()}";
            _currentAuthType = ((ComboBoxItem)cmbAuthType.SelectedItem)?.Content?.ToString() == "SQL Server Authentication" ? "Sql" : "Windows";
            _currentUsername = (txtUsername?.Text ?? string.Empty).Trim();
            _currentPassword = txtPassword.Password;
        }

        private string BuildCurrentConnectionString()
        {
            return DBHandler.GetConnstring(_currentServerInstance, _currentAuthType, _currentUsername, _currentPassword);
        }

        protected virtual async Task RefreshDatabaseList()
        {
            try
            {
                var databases = await DatabaseService.LoadDatabases(_latestConnectionString);
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

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            btnTestConnection.IsEnabled = false;

            try
            {
                bool isSqlAuth = ((ComboBoxItem)cmbAuthType.SelectedItem)?.Content.ToString() == "SQL Server Authentication";

                bool isValid = true;

                // Validate server name
                bool isServerPrefixEmpty = string.IsNullOrWhiteSpace(txtServerPrefix.Text);
                bool isServerNameEmpty = string.IsNullOrWhiteSpace(txtServerName.Text);
                if (isServerPrefixEmpty || isServerNameEmpty) isValid = false;

                // Validate SQL auth fields
                if (isSqlAuth)
                {
                    bool isUsernameEmpty = string.IsNullOrWhiteSpace(txtUsername.Text);
                    bool isPasswordEmpty = string.IsNullOrWhiteSpace(txtPassword.Password);
                    if (isUsernameEmpty || isPasswordEmpty) isValid = false;
                }

                if (!isValid)
                {
                    LoggingService.LogWarning("Please fill all required fields.", true);
                    return;
                }

                // Proceed
                CaptureServerCredentials();
                _latestConnectionString = BuildCurrentConnectionString();
                DBLayer oDB = new DBLayer(_latestConnectionString);

                bool success = oDB.Connect(false);
                if (!success)
                    throw new Exception("Could not connect to the server.");

                cmbDatabaseName.IsEnabled = true;
                btnCreateDatabase.IsEnabled = true;
                LoggingService.LogInfo("Connection Established", true);

                await RefreshDatabaseList();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Connection Failed to Establish.", ex, true);
                btnCreateDatabase.IsEnabled = false;
            }
            finally
            {
                btnTestConnection.IsEnabled = true;
            }
        }

        private async Task<bool> EnsureStagingDatabaseReadyAsync()
        {
            DatabaseConfig dbConfig = await ConfigFileHelper.LoadAsync<DatabaseConfig>(ConfigFileService.ConfigPath);
            var decryptedConnStr = SecureStringHelper.Decrypt(dbConfig.StagingDatabase.EncryptedConnectionString);
            using var dbLayer = new DBLayer(decryptedConnStr);
            var stagingDbService = new DatabaseService(dbLayer);
            return await stagingDbService.EnsureStagingDbReadyAsync(dbConfig);
        }

        protected virtual async void SaveButton_Click(object sender, RoutedEventArgs e)
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
                    StagingDatabase = new DbConnectionInfo
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
                bool dbReady = await EnsureStagingDatabaseReadyAsync();
                if (!dbReady)
                {
                    throw new Exception("An unexpected error occurred while preparing the staging database.");

                }
                LoggingService.LogInfo("The staging database is configured and ready for use.", true);

            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to save staging config", ex, true);
            }
        }

        private void CreateDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            CaptureServerCredentials();

            var createDbWindow = new CreateDatabase(
                _currentServerInstance,
                _currentAuthType,
                _currentUsername,
                _currentPassword,
                async (newDbName) =>
                {
                    _targetDatabaseName = newDbName;
                    _latestConnectionString = BuildCurrentConnectionString();

                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await RefreshDatabaseList();
                        if (cmbDatabaseName.ItemsSource is IEnumerable<string> databases && databases.Contains(newDbName))
                        {
                            cmbDatabaseName.SelectedItem = newDbName;
                            btnSaveConfig.IsEnabled = true;
                        }
                    });
                });

            createDbWindow.Owner = Window.GetWindow(this);
            createDbWindow.ShowDialog();
        }

        private void Server_TextChanged(object sender, TextChangedEventArgs e) => ResetDatabaseUI();

        private void ClearConnectionFields_Click(object sender, RoutedEventArgs e)
        {
            txtServerPrefix.Text = string.Empty;
            txtServerName.Text = string.Empty;
            txtUsername.Text = string.Empty;
            txtPassword.Password = string.Empty;
            cmbAuthType.SelectedIndex = 0;
            btnCreateDatabase.IsEnabled = false;
            ResetDatabaseUI();
        }

        private void ResetDatabaseUI()
        {
            cmbDatabaseName.ItemsSource = null;
            cmbDatabaseName.IsEnabled = false;
            btnCreateDatabase.IsEnabled = false;
            btnSaveConfig.IsEnabled = false;
        }

    }
}
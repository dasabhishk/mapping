using System.Windows;
using System.Windows.Controls;
using CMMT.dao;
using CMMT.Services;

namespace CMMT.UI
{
    public partial class CreateDatabase : Window
    {
        private readonly string _serverInstance;
        private readonly string _authType;
        private readonly string _username;
        private readonly string _password;
        public Func<string, Task>? OnDatabaseCreated { get; set; }

        public CreateDatabase(string serverInstance, string authType, string username, string password,
            Func<string, Task>? onDatabaseCreated)
        {
            InitializeComponent();
            _serverInstance = serverInstance;
            _authType = authType;
            _username = username;
            _password = password;
            OnDatabaseCreated = onDatabaseCreated;

            btnCreate.IsEnabled = false;
            txtNewDatabase.TextChanged += NewDatabaseTextBox_TextChanged;
        }

        private void NewDatabaseTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string rawText = txtNewDatabase.Text;
            // Filter out invalid characters: only allow letters, digits, and underscores
            string filteredText = new string(rawText.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

            if (rawText != filteredText)
            {
                int caret = txtNewDatabase.CaretIndex;
                txtNewDatabase.Text = filteredText;
                txtNewDatabase.CaretIndex = Math.Max(0, caret - 1);
            }

            btnCreate.IsEnabled = !string.IsNullOrWhiteSpace(txtNewDatabase.Text);
        }


        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            string newDbName = txtNewDatabase.Text.Trim();
            if (string.IsNullOrWhiteSpace(newDbName))
            {
                LoggingService.LogWarning("Please enter a database name.", true);
                return;
            }
            else if (newDbName.StartsWith('_') || newDbName.EndsWith('_'))
            {
                LoggingService.LogWarning("Database name cannot start or end with an underscore.", true);
                return;
            }
            await HandleCreateDatabase(newDbName);
        }

        public async Task HandleCreateDatabase(string userInputDbName)
        {
            string prefix = "staging_migration";
            string fullDbName = userInputDbName.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                                userInputDbName.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase)
                ? userInputDbName
                : $"{prefix}_{userInputDbName}";

            string connStr = DBHandler.GetConnstring(_serverInstance, _authType, _username, _password);

            LoggingService.LogInfo($"Attempting to create database '{fullDbName}' on server '{_serverInstance}'", false);
            try
            {
                bool exists = await DatabaseService.DatabaseExists(connStr, fullDbName);
                if (exists)
                {
                    LoggingService.LogWarning("Database with this name already exists.", true);
                    return;
                }

                DatabaseService.CreateDatabase(connStr, fullDbName);
                LoggingService.LogInfo($"Database '{fullDbName}' created successfully.", true);

                if (OnDatabaseCreated != null)
                    await OnDatabaseCreated.Invoke(fullDbName);

                this.Close();
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error while creating database '{fullDbName}'", ex, true);
            }
        }


        private void ClearButton_Click(object sender, RoutedEventArgs e) => txtNewDatabase.Text = string.Empty;

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
    }

}
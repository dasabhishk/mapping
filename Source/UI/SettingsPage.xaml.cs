using System.Windows;
using System.Windows.Controls;
using CMMT.Services;
using CMMT.Helpers;
using CMMT.Models;
using CMMT.dao;
using System.IO;
using System.Text.Json;
using System.Data;

namespace CMMT.UI
{
    public partial class SettingsPage : Page
    {
        private string? _latestConnectionString;
        private string? _targetDatabaseName;
        private string? _initialDatabaseName;
        private static SqlProcedureConfig? _sqlProcedureConfig;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadConnectionString();
            await RefreshDatabaseList();
        }


        private static SqlProcedureConfig GetSqlProcedureConfig()
        {
            try
            {
                if (_sqlProcedureConfig != null)
                    return _sqlProcedureConfig;

                string configPath = ConfigFileHelper.GetConfigFilePath("Configuration", "SQLProcedure.json");
                string json = File.ReadAllText(configPath);
                _sqlProcedureConfig = JsonSerializer.Deserialize<SqlProcedureConfig>(json);
                if (_sqlProcedureConfig == null)
                    return new SqlProcedureConfig { Procedures = new Dictionary<string, List<string>>() };
                return _sqlProcedureConfig;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error in GetSqlProcedureConfig", ex, true);
                return new SqlProcedureConfig { Procedures = new Dictionary<string, List<string>>() }; 
            }
        }

        private async Task LoadConnectionString()
        {
            try
            {
                var dbConfig = await ConfigFileHelper.LoadAsync<DatabaseConfig>(ConfigFileService.ConfigPath);
                if (dbConfig?.StagingDatabase?.EncryptedConnectionString == null || dbConfig.StagingDatabase.Database == null)
                    throw new InvalidOperationException("Missing encrypted connection string or database details.");
                _latestConnectionString = SecureStringHelper.Decrypt(dbConfig.StagingDatabase.EncryptedConnectionString);
                _initialDatabaseName = dbConfig.StagingDatabase.Database;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to load connection string - Check if Staging Database is configured", ex, true);
                cmbDatabaseName.IsEnabled = false;
            }
        }


        protected async Task RefreshDatabaseList()
        {
            try
            {
                cmbDatabaseName.SelectionChanged -= cmbDatabaseName_SelectionChanged;
                cmbDatabaseName.Items.Clear();

                if (string.IsNullOrWhiteSpace(_latestConnectionString))
                {
                    cmbDatabaseName.Items.Add("Please Select a database");
                    cmbDatabaseName.SelectedIndex = 0;
                    cmbDatabaseName.IsEnabled = false;
                    btnResetMigration.IsEnabled = false;
                    return;
                }
                var databases = await DatabaseService.LoadDatabases(_latestConnectionString);
                foreach (var db in databases)
                    cmbDatabaseName.Items.Add(db);

                int initialDbIndex = _initialDatabaseName != null ? databases.IndexOf(_initialDatabaseName) : -1;
                if (initialDbIndex >= 0)
                    cmbDatabaseName.SelectedIndex = initialDbIndex;
                else if (cmbDatabaseName.Items.Count > 0)
                    cmbDatabaseName.SelectedIndex = 0;

                btnResetMigration.IsEnabled = true;
                cmbDatabaseName.IsEnabled = true;
                cmbDatabaseName.SelectionChanged += cmbDatabaseName_SelectionChanged;
                _targetDatabaseName = _initialDatabaseName;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to load databases", ex, true);
            }
        }

        private void cmbDatabaseName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbDatabaseName.SelectedIndex < 0)
            {
                _targetDatabaseName = null;
                btnResetMigration.IsEnabled = false;
                return;
            }
            string? selectedDb = cmbDatabaseName.SelectedItem?.ToString();

            if (!string.IsNullOrWhiteSpace(selectedDb) && selectedDb != _initialDatabaseName)
            {
                var result = MessageBox.Show(
                    $"The selected database - {selectedDb} doesn't match with the configured staging database - {_initialDatabaseName}, Do you still want to continue?",
                    "Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    _ = RefreshDatabaseList();
                    return;
                }
            }

            if (cmbDatabaseName.SelectedIndex >= 0)
            {
                _targetDatabaseName = selectedDb;
                btnResetMigration.IsEnabled = true;
            }
            else
            {
                _targetDatabaseName = null;
                btnResetMigration.IsEnabled = false;
            }
        }
        private static string BuildConnectionStringWithDatabase(string baseConnectionString, string databaseName)
        {
            var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(baseConnectionString);
            builder.InitialCatalog = databaseName;
            return builder.ConnectionString;
        }

        private void btnResetMigration_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_latestConnectionString) || string.IsNullOrWhiteSpace(_targetDatabaseName))
                return;

            try
            {
                string connectionStringWithDb = BuildConnectionStringWithDatabase(_latestConnectionString, _targetDatabaseName);

                using var dbLayer = new DBLayer(connectionStringWithDb);
                dbLayer.Connect(false);

                var migratedRows = GetMigratedRowsCount(dbLayer);
                int studyCount = migratedRows.StudyCount;
                int seriesCount = migratedRows.SeriesCount;

                if (studyCount == 0 && seriesCount == 0)
                {
                    LoggingService.LogInfo("No migrated rows found in the selected database.", true);
                    return;
                }

                var result = MessageBox.Show(
                    "Are you sure you want to reset migrated rows? This will make them eligible for reprocessing.",
                    "Confirm Reset",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.OK)
                {
                    var startTime = DateTime.Now;

                    ResetMigratedRows(dbLayer);

                    var elapsed = DateTime.Now - startTime;

                    string message = "";
                    if (studyCount > 0)
                        message += $"Total {studyCount} rows have been reset in Patient Study table.\n";
                    if (seriesCount > 0)
                        message += $"Total {seriesCount} rows have been reset in Patient Series table.\n";
                    message += $"Error Table has been truncated.\nCompleted in {elapsed.TotalSeconds:F2} seconds.";
                    LoggingService.LogInfo(message.TrimEnd('\n'), true);
                    LogResetOperationDetails(connectionStringWithDb, studyCount, seriesCount, elapsed);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error during reset migrated rows", ex, true);
            }
        }

        private static MigratedRowsCount GetMigratedRowsCount(DBLayer dbLayer)
        {
            try
            {
                // Use the stored procedure name from the config
                var procConfig = GetSqlProcedureConfig();
                string procName = "cmmt.sp_GetMigratedRowsCount";

                // Call the stored procedure and read the result
                DataTable dt;
                dbLayer.Execute_SP_DataTable(procName, new DBParameters(), out dt);

                if (dt.Rows.Count > 0)
                {
                    int studyCount = Convert.ToInt32(dt.Rows[0]["StudyCount"]);
                    int seriesCount = Convert.ToInt32(dt.Rows[0]["SeriesCount"]);
                    return new MigratedRowsCount(studyCount, seriesCount);
                }
                return new MigratedRowsCount(0, 0);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error while fetching status column count in metadata tables", ex);
                return new MigratedRowsCount(0, 0);
            }
        }


        private static void ResetMigratedRows(DBLayer dbLayer)
        {
            try
            {
                // Use the stored procedure name from the config
                var procConfig = GetSqlProcedureConfig();
                string procName = "cmmt.sp_ResetMigratedRows";

                dbLayer.Execute_SP(procName);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to reset migrated rows", ex, true);
            }
        }


        private static void LogResetOperationDetails(string connectionStringWithDb, int studyCount, int seriesCount, TimeSpan elapsed)
        {
            var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionStringWithDb);
            string server = builder.DataSource;
            string database = builder.InitialCatalog;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string logMsg = $"Reset operation completed at {timestamp} on server '{server}' and database '{database}'. " +
                            $"Rows reset: Patient Study = {studyCount}, Patient Series = {seriesCount}. " +
                            $"Elapsed: {elapsed.TotalSeconds:F2} seconds.";

            LoggingService.LogInfo(logMsg);
        }
    }

}

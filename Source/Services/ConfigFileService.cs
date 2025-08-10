using System.IO;
using System.Text.Json;
using CMMT.Helpers;
using CMMT.Models;

namespace CMMT.Services
{
    public class ConfigFileService
    {
        public static string ConfigPath => ConfigFileHelper.GetConfigFilePath("Configuration", "dbconfig.json");

        public static async Task<DatabaseConfig?> LoadInitialConfig()
        {
            if (!File.Exists(ConfigPath)) return null;

            string json = await File.ReadAllTextAsync(ConfigPath);
            var config = JsonSerializer.Deserialize<DatabaseConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            });

            if (config == null) return null;

            return config;
        }

        /// <summary>
        /// Updates the respective Database section while preserving the rest of the config.
        /// </summary>
        public static async Task<bool> SaveDatabaseConfig(DatabaseConfig configToSave)
        {
            try
            {
                DatabaseConfig? fullConfig = await ConfigFileHelper.LoadAsync<DatabaseConfig>(ConfigPath);
                if (fullConfig == null)
                    fullConfig = new DatabaseConfig();

                if (configToSave.StagingDatabase != null)
                    fullConfig.StagingDatabase = configToSave.StagingDatabase;

                if (configToSave.TargetDatabase != null)
                    fullConfig.TargetDatabase = configToSave.TargetDatabase;

                if (configToSave.StudyMigrationProcess != null)
                    fullConfig.StudyMigrationProcess.TotalNoOfRecordsToMigrate = configToSave.StudyMigrationProcess.TotalNoOfRecordsToMigrate;

                if (configToSave.SeriesMigrationProcess != null)
                    fullConfig.SeriesMigrationProcess.TotalNoOfRecordsToMigrate = configToSave.SeriesMigrationProcess.TotalNoOfRecordsToMigrate;

                var options = new JsonSerializerOptions { WriteIndented = true };
                string updatedJson = JsonSerializer.Serialize(fullConfig, options);
                await File.WriteAllTextAsync(ConfigPath, updatedJson);
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error while saving the config", ex, true);
                return false;
            }
        }

    }
}
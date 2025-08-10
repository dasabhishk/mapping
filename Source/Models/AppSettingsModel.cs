using CMMT.DataStructures;
using CMMT.Helpers;
using CMMT.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CMMT.Models
{
    public sealed class AppSettingsModel
    {
        private static readonly AppSettingsModel appSettingsModel = new AppSettingsModel();

        /// <summary>
        /// Gets the singleton instance of AppSettingsModel.
        /// </summary>
        public static AppSettingsModel GetInstance
        {
            get
            {
                if (appSettingsModel == null)
                {
                    return new AppSettingsModel();
                }
                return appSettingsModel;
            }
        }

        /// <summary>
        /// Provides access to the application settings loaded from AppSettings.json file.
        /// </summary>
        public AppSettings Settings { get; private set; }


        private AppSettingsModel()
        {

        }

        /// <summary>
        /// Loads the application settings from the AppSettings.json file.
        /// </summary>
        /// <returns></returns>
        public void LoadAppSettings()
        {
            if (Settings == null)
            {
                // App settings required to load in sync as it has references in over all application cannot be loaded in async for the app start up.
                var path = ConfigFileHelper.GetConfigFilePath("Configuration", "AppSettings.json");
                Settings = JsonHelper.LoadSynchronously<AppSettings>(path);
            }
        }

        /// <summary>
        /// Resets the application settings to their default values.    
        /// </summary>
        /// <remarks>This method initializes the <see cref="Settings"/> property with default values if it
        /// is currently null.  The default settings include predefined values for properties. Additionally, the settings
        /// are saved to a configuration file named  "AppSettings.json" in the "Configuration" directory.</remarks>
        public void ResetAppSettings()
        {
            if (Settings == null)
            {
                Settings = GetDefaultAppSettings();
                using var _ = ConfigFileHelper.SaveAsync(Settings, ConfigFileHelper.GetConfigFilePath("Configuration", "AppSettings.json"));
            }
        }

        /// <summary>
        /// Retrieves the default application settings.
        /// </summary>
        /// <remarks>This method returns a preconfigured <see cref="AppSettings"/> object with default
        /// values  for various application settings, These defaults are suitable for typical use cases but can be customized  as needed.</remarks>
        /// <returns>An <see cref="AppSettings"/> object containing the default configuration values.</returns>
        public AppSettings GetDefaultAppSettings()
        {
            return new AppSettings
            {
                MaxSampleRows = 4,
                MaxParallel = 2,
                PreviewSampleCount = 3,
                CsvTypes = new CsvTypes
                {
                    PatientStudy = "Patient Study",
                    SeriesInstance = "Series Instance"
                },
                NoMappingOptional = "-- No Mapping (Optional) --",
                StudyDataProcedure = "[dbo].[HIS_RegisterPACSStudyData]",
                CsvDelimitter = "|",
                TimeOutCount = 3600
            };
        }

        /// <summary>
        /// Corrects and saves the application settings based on the load status and invalid settings.
        /// </summary>
        /// <param name="loadStatus"> states the status of the settings load</param>
        /// <param name="invalidSettings">gives the list of invalid settings</param>
        /// <returns></returns>
        public async Task CorrectAndSaveAppSettings(bool loadStatus, List<PropertyInfo> invalidSettings)
        {
            if (!loadStatus)
            {
                appSettingsModel.Settings = appSettingsModel.GetDefaultAppSettings();
            }
            else if (invalidSettings.Count > 0)
            {
                var defaultSettings = appSettingsModel.GetDefaultAppSettings();

                foreach (var property in invalidSettings)
                {
                    var defaultValue = property.GetValue(defaultSettings);
                    property.SetValue(appSettingsModel.Settings, defaultValue);
                }
            }
            else
            {
                LoggingService.LogInfo("The Application settings has been loaded successfully from AppSettings config file.");
                return;
            }
            LoggingService.LogWarning("The Application settings has been loaded with some invalid values. Default values have been applied for the invalid settings.");
            await ConfigFileHelper.SaveAsync(appSettingsModel.Settings, ConfigFileHelper.GetConfigFilePath("Configuration", "AppSettings.json"));
        }
    }
}

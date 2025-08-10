using System;
using CMMT.dao;
using CMMT.Helpers;
using CMMT.Models;

namespace CMMT.Services
{
    public class IscvVersionValidator
    {
        private readonly DatabaseConfig _dbConfig;

        public IscvVersionValidator(DatabaseConfig dbConfig)
        {
            _dbConfig = dbConfig;
        }

        public async Task<bool> IsTargetDbVersionSupportedAsync()
        {
            try
            {
                var supportedVersions = _dbConfig.SupportedTargetDbVersions;
                var encryptedConnStr = _dbConfig.TargetDatabase.EncryptedConnectionString;
                var targetDbConnStr = SecureStringHelper.Decrypt(encryptedConnStr);

                string dbVersion = await GetCurrentDbVersionAsync(new DBLayer(targetDbConnStr));

                if (string.IsNullOrWhiteSpace(dbVersion))
                {
                    LoggingService.LogWarning("No version details found in [_XA_INSTALL_HISTORY]");
                }

                bool isSupported = supportedVersions.Contains(dbVersion);
                if (!isSupported)
                {
                    LoggingService.LogWarning($"Target DB version '{dbVersion}' is not supported. Supported versions: {string.Join(", ", supportedVersions)}");
                }
                return isSupported;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error during ISCV version validation: {ex.Message}", ex);
                return false;
            }
        }

        private async Task<string> GetCurrentDbVersionAsync(DBLayer dbLayer)
        {
            dbLayer.Connect(false);

            const string sql = "SELECT TOP 1 [MajorVersion] FROM [dbo].[_XA_INSTALL_HISTORY] ORDER BY [installId] DESC";
            var result = dbLayer.ExecuteScalar_Query(sql);
            return result?.ToString() ?? string.Empty;
        }
    }
}

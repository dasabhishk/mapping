using System.Data;
using CMMT.dao;
using CMMT.Helpers; // Assuming LoggingService is here

namespace CMMT.Services
{
    public class BatchStatusUpdater
    {
        private readonly string _stagingDbConnectionString;

        public BatchStatusUpdater(string stagingDbConnectionString)
        {
            _stagingDbConnectionString = stagingDbConnectionString;
        }

        public void UpdateBatchStatus(List<object> ids, string tableName, string status)
        {
            if (ids == null || ids.Count == 0)
                return;

            if (string.IsNullOrWhiteSpace(tableName))
            {
                LoggingService.LogError(
                    $"Attempted to update batch status for invalid or untrusted table name: '{tableName}'. Operation aborted.",
                    null,
                    true
                );
                return;
            }
            try
            {
                var idParams = new List<string>();
                using var parameters = new DBParameters();
                for (int i = 0; i < ids.Count; i++)
                {
                    string paramName = $"@Id{i}";
                    idParams.Add(paramName);
                    parameters.Add(paramName, ids[i], ParameterDirection.Input, SqlDbType.Int);
                }
                parameters.Add("@Status", status, ParameterDirection.Input, SqlDbType.NVarChar, 50);
                string sql = $"UPDATE {tableName} SET Status = @Status WHERE Id IN ({string.Join(",", idParams)})";
                using var stagingDbLayer = new DBLayer(_stagingDbConnectionString);
                stagingDbLayer.Connect(false);
                stagingDbLayer.ExecuteQuery_WithParams<int>(sql, parameters);
                stagingDbLayer.Disconnect();
            }
            catch (Exception ex)
            {
                LoggingService.LogError(
                    $"Failed to update batch status for table '{tableName}' with status '{status}'. Exception: {ex.Message}",
                    ex);
                throw;
            }
        }
    }
}
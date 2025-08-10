using System.Data;
using CMMT.Models;
using Microsoft.Data.SqlClient;

namespace CMMT.Services
{
    public class BulkCopyService
    {
        public async static Task<(int SuccessCount, int ErrorCount)> SaveDataToStaging(DataTable batch, MappingConfig mappingConfig, string decryptedConnStr, string tablename)
        {
            var mapping = mappingConfig.Mappings.First(m => m.TableName == tablename);
            try
            {
                using var bulkCopy = new SqlBulkCopy(decryptedConnStr)
                {
                    EnableStreaming = true,
                    DestinationTableName = tablename switch
                    {
                        "cmmt_PatientStudyMetaData" => "cmmt.cmmt_PatientStudyMetaData",
                        "cmmt_PatientStudySeriesData" => "cmmt.cmmt_PatientStudySeriesData",
                        _ => throw new ArgumentException("Invalid tablename")
                    }
                };

                // Prepare SqlBulkCopy mapping
                foreach (var map in mapping.ColumnMappings)
                {
                    bulkCopy.ColumnMappings.Add(map.DbColumn, map.DbColumn);
                }
                bulkCopy.ColumnMappings.Add("FileName", "FileName");
                bulkCopy.ColumnMappings.Add("RowNumber", "RowNumber");
                bulkCopy.ColumnMappings.Add("Source", "Source");

                await Task.Run(() => bulkCopy.WriteToServer(batch));

                LoggingService.LogInfo($"Successfully transformed and loaded batch of records into the staging DB.");

                return (batch.Rows.Count, 0); // all succeeded
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error occurred while inserting a batch of records into staging table.", ex, false);
                
                return (0, batch.Rows.Count); // all failed
            }
        }
        public async static Task SaveAuditLogToDB(string decryptedConnStr, DataTable auditLogTable)
        {
            try
            {
                using var bulkCopyAudit = new SqlBulkCopy(decryptedConnStr)
                {
                    EnableStreaming = true,
                    DestinationTableName = "cmmt.cmmt_AuditLog"
                };
                bulkCopyAudit.ColumnMappings.Add("FileName", "FileName");
                bulkCopyAudit.ColumnMappings.Add("CompletedAt", "CompletedAt");
                bulkCopyAudit.ColumnMappings.Add("TotalRowsCount", "TotalRowsCount");
                bulkCopyAudit.ColumnMappings.Add("SucessRowsCount", "SucessRowsCount");
                bulkCopyAudit.ColumnMappings.Add("ErrorRowsCount", "ErrorRowsCount");

                await bulkCopyAudit.WriteToServerAsync(auditLogTable);
                LoggingService.LogInfo("Audit logs successfully written to DB.");
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Failed to write audit logs to DB.", ex, true);
            }
        }

    }
}

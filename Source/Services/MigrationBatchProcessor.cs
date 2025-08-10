using System.Collections.Concurrent;
using System.Data.SqlClient;
using CMMT.dao;
using CMMT.Helpers;
using CMMT.Models;

namespace CMMT.Services
{
    public class MigrationBatchProcessor
    {
        private readonly string _targetDbConnectionString;
        private readonly string _stagingDbConnectionString;
       

        public MigrationBatchProcessor(string targetDbConnectionString, string stagingDbConnectionString)
        {
            _targetDbConnectionString = targetDbConnectionString;
            _stagingDbConnectionString = stagingDbConnectionString;
        }
        private void LogErrorToStagingDb(string fileName, int rowNumber, string errorMessage)
        {
            using (var logDbLayer = new DBLayer(_stagingDbConnectionString))
            {
                logDbLayer.Connect(false);
                var errorLogger = new ErrorLogger(logDbLayer);
                errorLogger.LogError(fileName, rowNumber, errorMessage);
            }
        }

        public async Task<(List<object> processedIds, List<object> failedIds)> ProcessPatienStudyMetaDataBatchParallelAsync(
        List<ProcedureParameters> batch, int maxDegreeOfParallelism, CancellationToken cancellationToken = default)
        {
            var processedIds = new ConcurrentBag<object>();
            var failedIds = new ConcurrentBag<object>();

            int _maxDegreeOfParallelism = maxDegreeOfParallelism;
            var appSettings = AppSettingsModel.GetInstance;
            await Task.Run(() =>
            {
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };

                Parallel.ForEach(batch, parallelOptions, item =>
                {
                    bool success = true;
                    try
                    {
                        object returnValue;

                        using (var targetDbLayer = new DBLayer(_targetDbConnectionString))
                        {
                            targetDbLayer.Connect(false);
                            int result = targetDbLayer.Execute_SP(appSettings.Settings.StudyDataProcedure, item.Parameters, out returnValue);
                        }
                        int code = (returnValue == null || returnValue == DBNull.Value) ? -1 : Convert.ToInt32(returnValue);

                        var procResult = new MessageService(code, "StudyDataMapping");

                        if (code != 0)
                        {
                            success = false;
                            string errorMessage = $"Procedure failed for record id={item.Id}  with the return code as {procResult.Code} - {procResult.Message}";
                            LogErrorToStagingDb(item.FileName, item.RowNumber, errorMessage);
                        }
                    }
                    catch (SqlException ex)
                    {
                        success = false;
                        string errorMessage = $"SQL Exception while processing the record id={item.Id} @Error: {ex.Message}";
                        LogErrorToStagingDb(item.FileName, item.RowNumber, errorMessage);
                    }
                    catch (Exception ex)
                    {
                        success = false;
                        string errorMessage = $"Exception while processing the record id={item.Id} @Error: {ex.Message}";
                        LogErrorToStagingDb(item.FileName, item.RowNumber, errorMessage);
                    }
                    if (success)
                    {
                        processedIds.Add(item.Id);
                    }
                    else
                    {
                        failedIds.Add(item.Id);
                    }
                    
                });
            });

            return (new List<object>(processedIds), new List<object>(failedIds));
        }
    }
}
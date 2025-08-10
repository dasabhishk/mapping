using System.Data;
using CMMT.dao;
using CMMT.Models;
using CMMT.Helpers;

namespace CMMT.Services
{
    public class StudyDataMigration : DataMigration
    {
        public StudyDataMigration(DatabaseConfig dbConfig, string CsvType) : base(dbConfig, CsvType)
        {
            Initialize(_csvType);
            LoggingService.LogInfo(
                                   $"Initialized StudyData migration parameters: " +
                                   $"StagingDb='{_stagingDbConnectionString}', " +
                                   $"TargetDb='{_targetDbConnectionString}', " +
                                   $"MaxParallelism={_maxParallelism}, " +
                                   $"DbFetchBatchSize={_dbFetchBatchSize}, " +
                                   $"ProcessingBatchSize={_processingBatchSize}, " +
                                   $"Table={_fullTableName}");
        }

        public override async Task<bool> ProcessDataInBatchesAsync(CancellationToken cancellationToken = default)
        {
            _cancellationToken = cancellationToken;
            int _totalProcessedRecords = 0;
            int _totalBatchesProcessed = 0;
            try
            {
                bool hasMoreRows = true;
                while (hasMoreRows)
                {
                    // Batch Check cancellation before fetching new batch
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        LoggingService.LogInfo($"Migration cancellation requested - no new batches started. Completed {_totalBatchesProcessed} batches successfully.");
                        return true; // Return success for graceful partial completion
                    }

                    LoggingService.LogInfo($"Starting batch {_totalBatchesProcessed + 1} - fetching staging data...");
                    DataTable stagingData = FetchNextStagingBatch();

                    if (stagingData == null || stagingData.Rows.Count == 0)
                    {
                        hasMoreRows = false;
                        break;
                    }

                    var rows = stagingData.AsEnumerable().ToList();
                    // If a limit is set and this batch would exceed it, trim the batch
                    if (_recordsToProcess > 0 && _totalProcessedRecords + rows.Count > _recordsToProcess)
                    {
                        rows = rows.Take(_recordsToProcess - _totalProcessedRecords).ToList();
                        hasMoreRows = false; 
                    }

                    foreach (var batch in GetBatches(rows, _processingBatchSize))
                    {
                        LoggingService.LogInfo($"Processing batch with {batch.Count} records...");
                        
                        // COMPLETE BATCH PROCESSING - Never interrupt during batch processing
                        var (processedIds, failedIds) = await _migrationBatchProcessor
                            .ProcessPatienStudyMetaDataBatchParallelAsync(batch, _maxParallelism, _cancellationToken)
                            .ConfigureAwait(false);

                        // COMPLETE ALL STATUS UPDATES for current batch
                        if (processedIds.Count > 0)
                            _batchStatusUpdater.UpdateBatchStatus(processedIds, _fullTableName, "P");
                        if (failedIds.Count > 0)
                            _batchStatusUpdater.UpdateBatchStatus(failedIds, _fullTableName, "E");

                        _totalProcessedRecords += batch.Count;
                        LoggingService.LogInfo($"Batch completed - processed: {processedIds.Count}, failed: {failedIds.Count}, total records processed: {_totalProcessedRecords}");

                        // POST-BATCH CHECK: Check cancellation after complete batch status update
                        if (_cancellationToken.IsCancellationRequested)
                        {
                            LoggingService.LogInfo($"Migration cancellation processed after batch completion. Successfully completed batches with {_totalProcessedRecords} total records.");
                            return true; // Return success for graceful partial completion
                        }

                        // If a limit is set and we've reached it, exit
                        if (_recordsToProcess > 0 && _totalProcessedRecords >= _recordsToProcess)
                        {
                            hasMoreRows = false;
                            break;
                        }
                    }
                    
                    _totalBatchesProcessed++;
                }
                return true;
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error during the Patient Study MetaData migration process. Details: {ex.Message}";
                LoggingService.LogError(errorMessage, ex,true);
                return false;
            }
        }

        public override DBParameters BuildParameters(DataRow row)
        {
            return ParameterBuilder.BuildParametersStudyMetaData(row);
        }
    }
}
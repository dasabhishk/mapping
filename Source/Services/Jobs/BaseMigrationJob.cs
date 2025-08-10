
using Quartz;
using CMMT.Models;
using CMMT.Helpers;

namespace CMMT.Services.Jobs
{
    public abstract class BaseMigrationJob : IJob
    {
        protected bool _isInterrupted = false;

        public virtual async Task Execute(IJobExecutionContext context)
        {
            var jobKey = context.JobDetail.Key;
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            
            LoggingService.LogInfo($"[{correlationId}] Starting {GetType().Name} execution - JobKey: {jobKey}, Thread: {Environment.CurrentManagedThreadId}");
            
            var startTime = DateTime.UtcNow;
            
            try
            {
                // Log cancellation token status
                LoggingService.LogInfo($"[{correlationId}] Cancellation token status - IsCancellationRequested: {context.CancellationToken.IsCancellationRequested}");
                
                // Extract job data from context with detailed logging
                LoggingService.LogInfo($"[{correlationId}] Extracting job data from context...");
                var jobData = ExtractJobData(context);
                LoggingService.LogInfo($"[{correlationId}] Job data extracted successfully - JobId: {jobData.JobId}, CsvType: {jobData.CsvType}");

                // Validate job data with detailed logging
                LoggingService.LogInfo($"[{correlationId}] Validating job data...");
                if (!ValidateJobData(jobData))
                {
                    LoggingService.LogError($"[{correlationId}] Job data validation failed for JobId: {jobData.JobId}", null);
                    return;
                }
                LoggingService.LogInfo($"[{correlationId}] Job data validation passed");

                // Execute the migration with cancellation token from context
                LoggingService.LogInfo($"[{correlationId}] Starting migration execution...");
                await ExecuteMigrationAsync(jobData, context.CancellationToken);

                var executionTime = DateTime.UtcNow - startTime;
                
                if (!context.CancellationToken.IsCancellationRequested)
                {
                    LoggingService.LogInfo($"[{correlationId}] Successfully completed {GetType().Name} with JobId: {jobData.JobId} in {executionTime.TotalSeconds:F2}s");
                }
                else
                {
                    LoggingService.LogInfo($"[{correlationId}] Migration was cancelled during execution after {executionTime.TotalSeconds:F2}s");
                }
            }
            catch (OperationCanceledException)
            {
                var executionTime = DateTime.UtcNow - startTime;
                LoggingService.LogInfo($"[{correlationId}] Migration job was cancelled: {jobKey} after {executionTime.TotalSeconds:F2}s");
            }
            catch (Exception ex)
            {
                var executionTime = DateTime.UtcNow - startTime;
                LoggingService.LogError($"[{correlationId}] Error executing migration job after {executionTime.TotalSeconds:F2}s: {ex.Message}", ex);
                throw;
            }
        }

        protected virtual MigrationJobData ExtractJobData(IJobExecutionContext context)
        {
            var dataMap = context.JobDetail.JobDataMap;
            LoggingService.LogInfo($"Extracting job data from JobDataMap with {dataMap.Count} entries");

            try
            {
                // Extract JobId with validation
                LoggingService.LogInfo("Extracting JobId...");
                var jobId = dataMap.GetString("JobId");
                if (string.IsNullOrEmpty(jobId))
                {
                    throw new InvalidOperationException("JobId is null or empty in JobDataMap");
                }
                LoggingService.LogInfo($"JobId extracted: {jobId}");

                // Extract CsvType with validation
                LoggingService.LogInfo("Extracting CsvType...");
                var csvType = dataMap.GetString("CsvType");
                if (string.IsNullOrEmpty(csvType))
                {
                    throw new InvalidOperationException("CsvType is null or empty in JobDataMap");
                }
                LoggingService.LogInfo($"CsvType extracted: {csvType}");

                // Extract DatabaseConfig with validation
                LoggingService.LogInfo("Extracting DatabaseConfig...");
                var databaseConfig = dataMap.Get("DatabaseConfig");
                if (databaseConfig == null)
                {
                    throw new InvalidOperationException("DatabaseConfig is null in JobDataMap");
                }
                if (!(databaseConfig is DatabaseConfig dbConfig))
                {
                    throw new InvalidOperationException($"DatabaseConfig is not of expected type. Actual type: {databaseConfig.GetType()}");
                }
                LoggingService.LogInfo($"DatabaseConfig extracted successfully - ConnectionString length: {dbConfig.StagingDatabase?.EncryptedConnectionString?.Length ?? 0}");

                // Extract MigrationOptionType with validation
                LoggingService.LogInfo("Extracting MigrationOptionType...");
                var migrationOptionType = dataMap.Get("MigrationOptionType");
                if (migrationOptionType == null)
                {
                    throw new InvalidOperationException("MigrationOptionType is null in JobDataMap");
                }
                if (!(migrationOptionType is MigrationOptionType optionType))
                {
                    throw new InvalidOperationException($"MigrationOptionType is not of expected type. Actual type: {migrationOptionType.GetType()}");
                }
                LoggingService.LogInfo($"MigrationOptionType extracted: {optionType}");

                // Extract MigrationSettings with validation
                LoggingService.LogInfo("Extracting MigrationSettings...");
                var migrationSettings = dataMap.Get("MigrationSettings");
                if (migrationSettings == null)
                {
                    throw new InvalidOperationException("MigrationSettings is null in JobDataMap");
                }
                if (!(migrationSettings is MigrationSettings settings))
                {
                    throw new InvalidOperationException($"MigrationSettings is not of expected type. Actual type: {migrationSettings.GetType()}");
                }
                LoggingService.LogInfo($"MigrationSettings extracted - MaxParallelism: {settings.MaxParallelism}, BatchSize: {settings.ProcessingBatchSize}");

                // Extract optional scheduled times
                DateTime? scheduledStartTime = null;
                DateTime? scheduledEndTime = null;
                
                var startTimeObj = dataMap.Get("ScheduledStartTime");
                if (startTimeObj is DateTime startTime)
                {
                    scheduledStartTime = startTime;
                    LoggingService.LogInfo($"ScheduledStartTime extracted: {startTime}");
                }

                var endTimeObj = dataMap.Get("ScheduledEndTime");
                if (endTimeObj is DateTime endTime)
                {
                    scheduledEndTime = endTime;
                    LoggingService.LogInfo($"ScheduledEndTime extracted: {endTime}");
                }

                return new MigrationJobData
                {
                    JobId = jobId,
                    CsvType = csvType,
                    DatabaseConfig = dbConfig,
                    MigrationOptionType = optionType,
                    ScheduledStartTime = scheduledStartTime,
                    ScheduledEndTime = scheduledEndTime,
                    MigrationSettings = settings
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to extract job data: {ex.Message}", ex);
                throw;
            }
        }

        protected virtual bool ValidateJobData(MigrationJobData jobData)
        {
            LoggingService.LogInfo($"Validating job data for JobId: {jobData.JobId}");

            if (string.IsNullOrEmpty(jobData.CsvType))
            {
                LoggingService.LogError("Validation failed: CsvType is required for migration job", null);
                return false;
            }

            if (jobData.DatabaseConfig == null)
            {
                LoggingService.LogError("Validation failed: DatabaseConfig is required for migration job", null);
                return false;
            }

            // Validate DatabaseConfig completeness
            if (jobData.DatabaseConfig.StagingDatabase == null)
            {
                LoggingService.LogError("Validation failed: StagingDatabase configuration is missing", null);
                return false;
            }

            if (string.IsNullOrEmpty(jobData.DatabaseConfig.StagingDatabase.EncryptedConnectionString))
            {
                LoggingService.LogError("Validation failed: StagingDatabase connection string is missing", null);
                return false;
            }

            // Validate MigrationSettings
            if (jobData.MigrationSettings == null)
            {
                LoggingService.LogError("Validation failed: MigrationSettings is required for migration job", null);
                return false;
            }

            if (jobData.MigrationSettings.MaxParallelism <= 0)
            {
                LoggingService.LogError($"Validation failed: Invalid MaxParallelism value: {jobData.MigrationSettings.MaxParallelism}", null);
                return false;
            }

            if (jobData.MigrationOptionType == MigrationOptionType.Scheduled)
            {
                if (!jobData.ScheduledStartTime.HasValue || !jobData.ScheduledEndTime.HasValue)
                {
                    LoggingService.LogError("Validation failed: Scheduled migration requires both start and end times", null);
                    return false;
                }

                if (jobData.ScheduledEndTime <= jobData.ScheduledStartTime)
                {
                    LoggingService.LogError($"Validation failed: Scheduled end time ({jobData.ScheduledEndTime}) must be after start time ({jobData.ScheduledStartTime})", null);
                    return false;
                }

                var duration = jobData.ScheduledEndTime.Value - jobData.ScheduledStartTime.Value;
                LoggingService.LogInfo($"Scheduled migration window: {duration.TotalHours:F2} hours");
            }

            LoggingService.LogInfo("Job data validation completed successfully");
            return true;
        }

        protected abstract Task ExecuteMigrationAsync(MigrationJobData jobData, CancellationToken cancellationToken);
    }
}

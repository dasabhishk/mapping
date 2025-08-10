using CMMT.Helpers;
using CMMT.Models;
using CMMT.ViewModels;

namespace CMMT.Services
{
    public class MigrationManager
    {
        private readonly SchedulerService _schedulerService;
        private readonly Dictionary<string, string> _activeJobIds;
        private readonly AppSettingsModel appSettingsModel;

        public MigrationManager(SchedulerService schedulerService)
        {
            _schedulerService = schedulerService;
            _activeJobIds = new Dictionary<string, string>();
            appSettingsModel = AppSettingsModel.GetInstance;
        }

        public async Task<string> StartMigrationAsync(
            MigrationOptionType migrationOptionType,
            DateTime? startTime = null,
            DateTime? endTime = null,
            string csvType = null)
        {
            try
            {
                LoggingService.LogInfo($"MigrationManager: Starting {migrationOptionType} migration...");

                // Validate scheduler readiness with retry
                if (!await EnsureSchedulerReadyAsync())
                {
                    LoggingService.LogError("MigrationManager: Scheduler is not ready for migration operations", null);
                    return null;
                }

                // Validate input parameters
                if (migrationOptionType == MigrationOptionType.Scheduled)
                {
                    if (!startTime.HasValue || !endTime.HasValue)
                    {
                        LoggingService.LogError("MigrationManager: Scheduled migration requires both start and end times", null);
                        return null;
                    }
                    if (endTime <= startTime)
                    {
                        LoggingService.LogError("MigrationManager: End time must be after start time for scheduled migration", null);
                        return null;
                    }
                }

                // Load database configuration
                var dbConfig = await ConfigFileHelper.LoadAsync<DatabaseConfig>(ConfigFileService.ConfigPath);
                if (dbConfig == null)
                {
                    LoggingService.LogError("Failed to load database configuration", null);
                    return null;
                }

                // Use default CSV type if not specified
                csvType ??= appSettingsModel.Settings.CsvTypes.PatientStudy;

                // Create migration settings from database config
                var migrationSettings = CreateMigrationSettings(dbConfig, csvType);

                // Create job data
                var jobData = new MigrationJobData
                {
                    CsvType = csvType,
                    DatabaseConfig = dbConfig,
                    MigrationOptionType = migrationOptionType,
                    ScheduledStartTime = migrationOptionType == MigrationOptionType.Scheduled ? startTime : null,
                    ScheduledEndTime = migrationOptionType == MigrationOptionType.Scheduled ? endTime : null,
                    MigrationSettings = migrationSettings
                };

                string jobId;

                if (migrationOptionType == MigrationOptionType.Instant)
                {
                    LoggingService.LogInfo("Scheduling instant migration job...");
                    jobId = await _schedulerService.ScheduleInstantMigrationAsync(jobData);
                }
                else // Scheduled
                {
                    if (!startTime.HasValue || !endTime.HasValue)
                    {
                        LoggingService.LogError("Scheduled migration requires both start and end times", null);
                        return null;
                    }

                    LoggingService.LogInfo($"Scheduling migration job for execution at: {startTime} (Window: {startTime} - {endTime})");
                    jobId = await _schedulerService.ScheduleMigrationAsync(jobData, startTime.Value);
                }

                // Track the job
                _activeJobIds[jobId] = csvType;

                LoggingService.LogInfo($"Successfully scheduled {migrationOptionType} migration with JobId: {jobId}");
                return jobId;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to start migration: {ex.Message}", ex);
                return null;
            }
        }

        public async Task<bool> StopMigrationAsync(string jobId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(jobId))
                {
                    // Cancel all active jobs
                    var activeJobs = await _schedulerService.GetActiveJobIdsAsync();
                    bool allCancelled = true;

                    foreach (var activeJobId in activeJobs)
                    {
                        bool cancelled = await _schedulerService.CancelMigrationJobAsync(activeJobId);
                        if (cancelled)
                        {
                            _activeJobIds.Remove(activeJobId);
                        }
                        allCancelled &= cancelled;
                    }

                    LoggingService.LogInfo($"Attempted to cancel {activeJobs.Count} active migration jobs");
                    return allCancelled;
                }
                else
                {
                    // Cancel specific job
                    bool cancelled = await _schedulerService.CancelMigrationJobAsync(jobId);
                    if (cancelled)
                    {
                        _activeJobIds.Remove(jobId);
                        LoggingService.LogInfo($"Successfully cancelled migration job: {jobId}");
                    }
                    return cancelled;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to stop migration: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<JobStatus> GetMigrationStatusAsync(string jobId)
        {
            try
            {
                return await _schedulerService.GetJobStatusAsync(jobId);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to get migration status for job {jobId}: {ex.Message}", ex);
                return JobStatus.Failed;
            }
        }

        public async Task<List<string>> GetActiveMigrationJobsAsync()
        {
            return await _schedulerService.GetActiveJobIdsAsync();
        }

        private MigrationSettings CreateMigrationSettings(DatabaseConfig dbConfig, string csvType)
        {
            var migrationConfig = csvType switch
            {
                var t when t == appSettingsModel.Settings.CsvTypes.PatientStudy => dbConfig.StudyMigrationProcess,
                _ => dbConfig.SeriesMigrationProcess
            };

            return new MigrationSettings
            {
                MaxParallelism = migrationConfig.MaxParallelism,
                DbFetchBatchSize = migrationConfig.DataFetchBatchSize,
                ProcessingBatchSize = migrationConfig.RowsPerBatch,
                RecordsToProcess = migrationConfig.TotalNoOfRecordsToMigrate
            };
        }

        private async Task<bool> EnsureSchedulerReadyAsync()
        {
            const int maxRetries = 3;
            const int delayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                LoggingService.LogInfo($"MigrationManager: Checking scheduler readiness (attempt {attempt}/{maxRetries})");
                LoggingService.LogInfo($"MigrationManager: Current state - IsInitialized: {_schedulerService.IsInitialized}, IsReady: {_schedulerService.IsReady}");

                if (!_schedulerService.IsInitialized)
                {
                    LoggingService.LogWarning($"MigrationManager: Scheduler not initialized on attempt {attempt}");

                    if (attempt < maxRetries)
                    {
                        LoggingService.LogInfo($"MigrationManager: Waiting {delayMs}ms before retry...");
                        await Task.Delay(delayMs);
                        continue;
                    }
                    LoggingService.LogError("MigrationManager: Scheduler initialization failed after all retries", null);
                    return false;
                }

                if (!_schedulerService.IsReady)
                {
                    LoggingService.LogWarning($"MigrationManager: Scheduler not ready on attempt {attempt} - scheduler may not be started");

                    if (attempt < maxRetries)
                    {
                        LoggingService.LogInfo($"MigrationManager: Waiting {delayMs}ms before retry...");
                        await Task.Delay(delayMs);
                        continue;
                    }
                    LoggingService.LogError("MigrationManager: Scheduler failed to become ready after all retries", null);
                    return false;
                }

                // Final verification that scheduler can actually accept jobs
                try
                {
                    // This will call ValidateSchedulerReady() which now has enhanced checks
                    var activeJobs = await _schedulerService.GetActiveJobIdsAsync();
                    LoggingService.LogInfo($"MigrationManager: Scheduler verification successful - {activeJobs.Count} active jobs");
                    LoggingService.LogInfo("MigrationManager: Scheduler is ready for operations");
                    return true;
                }
                catch (Exception ex)
                {
                    LoggingService.LogWarning($"MigrationManager: Scheduler verification failed on attempt {attempt}: {ex.Message}");

                    if (attempt < maxRetries)
                    {
                        LoggingService.LogInfo($"MigrationManager: Waiting {delayMs}ms before retry...");
                        await Task.Delay(delayMs);
                        continue;
                    }
                    LoggingService.LogError($"MigrationManager: Scheduler verification failed after all retries: {ex.Message}", ex);
                    return false;
                }
            }

            return false;
        }

        // Legacy method for backward compatibility - now wraps job-based approach
        public void StopMigration()
        {
            LoggingService.LogInfo("MigrationManager: Legacy stop migration called - delegating to async job cancellation");
            _ = Task.Run(async () => await StopMigrationAsync());
        }
    }
}

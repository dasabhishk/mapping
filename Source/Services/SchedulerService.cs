using CMMT.Models;
using CMMT.Helpers;
using CMMT.Services.Jobs;
using Quartz;
using Quartz.Impl;
using System.Collections.Concurrent;

namespace CMMT.Services
{
    public class SchedulerService
    {
        private IScheduler? _scheduler;
        private readonly ConcurrentDictionary<string, JobKey> _activeJobs;
        private volatile bool _isInitialized = false;
        private volatile bool _isInitializing = false;
        private readonly object _initializationLock = new object();

        public bool IsInitialized => _isInitialized;
        public bool IsReady => _isInitialized && _scheduler?.IsStarted == true;

        public SchedulerService()
        {
            _activeJobs = new ConcurrentDictionary<string, JobKey>();
            LoggingService.LogInfo("SchedulerService: Constructor completed - async initialization required");
        }

        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
            {
                LoggingService.LogInfo("SchedulerService: Already initialized, skipping");
                return true;
            }

            lock (_initializationLock)
            {
                if (_isInitializing)
                {
                    LoggingService.LogInfo("SchedulerService: Initialization already in progress");
                    return false;
                }
                _isInitializing = true;
            }

            try
            {
                LoggingService.LogInfo("SchedulerService: Starting async initialization...");

                LoggingService.LogInfo("SchedulerService: Getting default scheduler");

                _scheduler = await StdSchedulerFactory.GetDefaultScheduler().ConfigureAwait(false);
                LoggingService.LogInfo("SchedulerService: Default scheduler obtained successfully");

                if (_scheduler == null)
                {
                    LoggingService.LogError("SchedulerService: Failed to obtain scheduler instance", null);
                    return false;
                }

                LoggingService.LogInfo($"SchedulerService: Scheduler created - Name: {_scheduler.SchedulerName}, InstanceId: {_scheduler.SchedulerInstanceId}");
                LoggingService.LogInfo($"SchedulerService: Scheduler state before start - IsStarted: {_scheduler.IsStarted}, IsShutdown: {_scheduler.IsShutdown}");

                // Start the scheduler to make it ready for operations
                await _scheduler.Start().ConfigureAwait(false);
                LoggingService.LogInfo($"SchedulerService: Scheduler started - IsStarted: {_scheduler.IsStarted}, IsShutdown: {_scheduler.IsShutdown}");

                _isInitialized = true;
                LoggingService.LogInfo($"SchedulerService: Initialization completed - IsInitialized: {_isInitialized}, IsReady: {IsReady}");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"SchedulerService: Initialization failed - {ex.Message}", ex);
                _scheduler = null;
                return false;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void ValidateSchedulerReady()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("SchedulerService is not initialized. Call InitializeAsync() first.");
            }

            if (_scheduler == null)
            {
                throw new InvalidOperationException("SchedulerService scheduler instance is null. Initialization may have failed.");
            }

            if (_scheduler.IsStarted != true)
            {
                throw new InvalidOperationException("SchedulerService scheduler is not started. Cannot perform scheduling operations.");
            }
        }

        public async Task StartAsync(SchedulerConfig config)
        {
            ValidateSchedulerReady();

            LoggingService.LogInfo($"SchedulerService: Configuring scheduler with {config.Jobs.Count} job configurations");

            foreach (var jobConfig in config.Jobs)
            {
                if (!jobConfig.Enabled)
                {
                    LoggingService.LogInfo($"SchedulerService: Skipping disabled job: {jobConfig.Name}");
                    continue;
                }

                LoggingService.LogInfo($"SchedulerService: Configuring job: {jobConfig.Name} with cron: {jobConfig.CronExpression}");

                var job = JobBuilder.Create<ImportJob>()
                    .WithIdentity(jobConfig.Name)
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithCronSchedule(jobConfig.CronExpression)
                    .Build();

                await _scheduler.ScheduleJob(job, trigger);
                LoggingService.LogInfo($"SchedulerService: Successfully scheduled job: {jobConfig.Name}");
            }

            LoggingService.LogInfo("SchedulerService: Job configuration completed - scheduler already running");
        }

        public async Task<string> ScheduleInstantMigrationAsync(MigrationJobData jobData)
        {
            ValidateSchedulerReady();

            // Immediate post-validation verification
            if (!IsReady)
            {
                throw new InvalidOperationException("SchedulerService state changed during validation - scheduler no longer ready");
            }

            try
            {
                LoggingService.LogInfo($"SchedulerService: Scheduling instant migration job with JobId: {jobData.JobId}");

                var job = JobBuilder.Create<InstantMigrationJob>()
                    .WithIdentity($"InstantMigration_{jobData.JobId}", "MigrationJobs")
                    .UsingJobData(CreateJobDataMap(jobData))
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity($"InstantTrigger_{jobData.JobId}", "MigrationJobs")
                    .StartNow()
                    .Build();

                await _scheduler.ScheduleJob(job, trigger);

                // Track the job (thread-safe)
                _activeJobs.TryAdd(jobData.JobId, job.Key);

                LoggingService.LogInfo($"SchedulerService: Successfully scheduled instant migration job: {jobData.JobId}");
                return jobData.JobId;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"SchedulerService: Failed to schedule instant migration job: {ex.Message}", ex);
                throw;
            }
        }

        public async Task<string> ScheduleMigrationAsync(MigrationJobData jobData, DateTime scheduleTime)
        {
            ValidateSchedulerReady();

            // Immediate post-validation verification
            if (!IsReady)
            {
                throw new InvalidOperationException("SchedulerService state changed during validation - scheduler no longer ready");
            }

            try
            {
                LoggingService.LogInfo($"SchedulerService: Scheduling migration job with JobId: {jobData.JobId} for execution at: {scheduleTime}");

                var job = JobBuilder.Create<ScheduledMigrationJob>()
                    .WithIdentity($"ScheduledMigration_{jobData.JobId}", "MigrationJobs")
                    .UsingJobData(CreateJobDataMap(jobData))
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity($"ScheduledTrigger_{jobData.JobId}", "MigrationJobs")
                    .StartAt(scheduleTime)
                    .Build();

                await _scheduler.ScheduleJob(job, trigger);

                // Track the job (thread-safe)
                _activeJobs.TryAdd(jobData.JobId, job.Key);

                LoggingService.LogInfo($"SchedulerService: Successfully scheduled migration job: {jobData.JobId} for {scheduleTime}");
                return jobData.JobId;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"SchedulerService: Failed to schedule migration job: {ex.Message}", ex);
                throw;
            }
        }

        public async Task<bool> CancelMigrationJobAsync(string jobId)
        {
            ValidateSchedulerReady();

            // Immediate post-validation verification
            if (!IsReady)
            {
                LoggingService.LogWarning("SchedulerService state changed during validation - scheduler no longer ready for cancellation");
                return false;
            }

            try
            {
                if (!_activeJobs.TryGetValue(jobId, out var jobKey))
                {
                    LoggingService.LogWarning($"SchedulerService: Job with ID {jobId} not found in active jobs.");
                    return false;
                }

                // Interrupt the job if it's currently running
                await _scheduler.Interrupt(jobKey);

                // Delete the job
                bool deleted = await _scheduler.DeleteJob(jobKey);

                if (deleted)
                {
                    _activeJobs.TryRemove(jobId, out _);
                    LoggingService.LogInfo($"SchedulerService: Successfully cancelled migration job: {jobId}");
                }

                return deleted;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"SchedulerService: Failed to cancel migration job {jobId}: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<JobStatus> GetJobStatusAsync(string jobId)
        {
            if (!IsReady)
            {
                LoggingService.LogWarning("SchedulerService: Cannot get job status - scheduler not ready");
                return JobStatus.Failed;
            }

            try
            {
                if (!_activeJobs.TryGetValue(jobId, out var jobKey))
                {
                    return JobStatus.Cancelled;
                }

                var jobDetail = await _scheduler.GetJobDetail(jobKey);
                if (jobDetail == null)
                {
                    return JobStatus.Cancelled;
                }

                var triggers = await _scheduler.GetTriggersOfJob(jobKey);
                if (!triggers.Any())
                {
                    return JobStatus.Completed;
                }

                var trigger = triggers.First();
                var triggerState = await _scheduler.GetTriggerState(trigger.Key);

                return triggerState switch
                {
                    TriggerState.Normal => JobStatus.Scheduled,
                    TriggerState.Complete => JobStatus.Completed,
                    TriggerState.Error => JobStatus.Failed,
                    TriggerState.Blocked => JobStatus.Running,
                    TriggerState.Paused => JobStatus.Scheduled,
                    _ => JobStatus.Scheduled
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"SchedulerService: Failed to get job status for {jobId}: {ex.Message}", ex);
                return JobStatus.Failed;
            }
        }

        public async Task<List<string>> GetActiveJobIdsAsync()
        {
            return _activeJobs.Keys.ToList();
        }

        private JobDataMap CreateJobDataMap(MigrationJobData jobData)
        {
            LoggingService.LogInfo($"SchedulerService: Creating job data map for JobId: {jobData.JobId}");

            var dataMap = new JobDataMap
            {
                ["JobId"] = jobData.JobId,
                ["CsvType"] = jobData.CsvType,
                ["DatabaseConfig"] = jobData.DatabaseConfig,
                ["MigrationOptionType"] = jobData.MigrationOptionType,
                ["MigrationSettings"] = jobData.MigrationSettings
            };

            // Only add scheduled times for scheduled migrations
            if (jobData.MigrationOptionType == MigrationOptionType.Scheduled)
            {
                if (jobData.ScheduledStartTime.HasValue)
                {
                    dataMap["ScheduledStartTime"] = jobData.ScheduledStartTime.Value;
                    LoggingService.LogInfo($"SchedulerService: Added ScheduledStartTime: {jobData.ScheduledStartTime.Value}");
                }

                if (jobData.ScheduledEndTime.HasValue)
                {
                    dataMap["ScheduledEndTime"] = jobData.ScheduledEndTime.Value;
                    LoggingService.LogInfo($"SchedulerService: Added ScheduledEndTime: {jobData.ScheduledEndTime.Value}");
                }
            }
            else
            {
                LoggingService.LogInfo($"SchedulerService: Instant migration - no scheduled times required");
            }

            return dataMap;
        }

        public async Task StopAsync()
        {
            if (_scheduler?.IsStarted == true)
            {
                LoggingService.LogInfo("SchedulerService: Shutting down scheduler...");
                await _scheduler.Shutdown();
                LoggingService.LogInfo("SchedulerService: Scheduler shutdown completed");
            }
        }
    }

    // Keep existing ImportJob for backward compatibility
    public class ImportJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            // Call your import logic here
            return Task.CompletedTask;
        }
    }
}

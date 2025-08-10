
using Quartz;
using CMMT.Models;
using CMMT.Helpers;

namespace CMMT.Services.Jobs
{
    public class ScheduledMigrationJob : BaseMigrationJob
    {
        protected override async Task ExecuteMigrationAsync(MigrationJobData jobData, CancellationToken cancellationToken)
        {
            LoggingService.LogInfo($"Executing scheduled migration for CSV type: {jobData.CsvType} from {jobData.ScheduledStartTime} to {jobData.ScheduledEndTime}");

            try
            {
                // Check for cancellation at the start
                cancellationToken.ThrowIfCancellationRequested();

                // Check if we're within the scheduled execution window
                DateTime now = DateTime.Now;
                if (now < jobData.ScheduledStartTime || now > jobData.ScheduledEndTime)
                {
                    LoggingService.LogWarning($"Current time {now} is outside scheduled execution window ({jobData.ScheduledStartTime} - {jobData.ScheduledEndTime}). Skipping execution.");
                    return;
                }

                // Validate target database version
                var validator = new IscvVersionValidator(jobData.DatabaseConfig);
                if (!await validator.IsTargetDbVersionSupportedAsync())
                {
                    LoggingService.LogError("Target database version is not supported for migration.", null);
                    return;
                }

                // Check for cancellation before creating migration service
                cancellationToken.ThrowIfCancellationRequested();

                // Create migration service using factory pattern
                DataMigration migrationService = DataMigration.CreateMigration(jobData.DatabaseConfig, jobData.CsvType);
                
                // Check for cancellation before starting processing
                cancellationToken.ThrowIfCancellationRequested();

                // Execute the migration within the scheduled window
                bool success = await ExecuteMigrationWithTimeWindow(migrationService, jobData, cancellationToken);
                
                if (success && !cancellationToken.IsCancellationRequested)
                {
                    LoggingService.LogInfo($"Scheduled migration completed successfully for CSV type: {jobData.CsvType}");
                }
                else if (cancellationToken.IsCancellationRequested)
                {
                    LoggingService.LogInfo($"Scheduled migration was cancelled for CSV type: {jobData.CsvType}");
                }
                else
                {
                    LoggingService.LogError($"Scheduled migration failed for CSV type: {jobData.CsvType}", null);
                }
            }
            catch (OperationCanceledException)
            {
                LoggingService.LogInfo($"Scheduled migration was cancelled for CSV type: {jobData.CsvType}");
                throw;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error during scheduled migration for CSV type {jobData.CsvType}: {ex.Message}", ex);
                throw;
            }
        }

        private async Task<bool> ExecuteMigrationWithTimeWindow(DataMigration migrationService, MigrationJobData jobData, CancellationToken cancellationToken)
        {
            // Create a combined cancellation token that also respects the end time
            using var timeoutCts = new CancellationTokenSource();
            var endTime = jobData.ScheduledEndTime!.Value;
            var remainingTime = endTime - DateTime.Now;
            
            if (remainingTime > TimeSpan.Zero)
            {
                timeoutCts.CancelAfter(remainingTime);
            }
            else
            {
                LoggingService.LogWarning("Scheduled end time has already passed. Skipping migration execution.");
                return false;
            }

            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                timeoutCts.Token
            );

            try
            {
                return await migrationService.ProcessDataInBatchesAsync(combinedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                LoggingService.LogInfo($"Scheduled migration stopped due to end time reached: {endTime}");
                return false;
            }
        }
    }
}

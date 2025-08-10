using Quartz;
using CMMT.Models;
using CMMT.Helpers;

namespace CMMT.Services.Jobs
{
    public class InstantMigrationJob : BaseMigrationJob
    {
        protected override async Task ExecuteMigrationAsync(MigrationJobData jobData, CancellationToken cancellationToken)
        {
            LoggingService.LogInfo($"Executing instant migration for CSV type: {jobData.CsvType}");

            try
            {
                // Check for cancellation at the start
                cancellationToken.ThrowIfCancellationRequested();

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

                // Execute the migration with cancellation token
                bool success = await migrationService.ProcessDataInBatchesAsync(cancellationToken);
                
                if (success && !cancellationToken.IsCancellationRequested)
                {
                    LoggingService.LogInfo($"Instant migration completed successfully for CSV type: {jobData.CsvType}");
                }
                else if (cancellationToken.IsCancellationRequested)
                {
                    LoggingService.LogInfo($"Instant migration was cancelled for CSV type: {jobData.CsvType}");
                }
                else
                {
                    LoggingService.LogError($"Instant migration failed for CSV type: {jobData.CsvType}", null);
                }
            }
            catch (OperationCanceledException)
            {
                LoggingService.LogInfo($"Instant migration was cancelled for CSV type: {jobData.CsvType}");
                throw;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error during instant migration for CSV type {jobData.CsvType}: {ex.Message}", ex);
                throw;
            }
        }
    }
}

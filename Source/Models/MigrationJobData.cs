
namespace CMMT.Models
{
    public class MigrationJobData
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public string CsvType { get; set; }
        public DatabaseConfig DatabaseConfig { get; set; }
        public MigrationOptionType MigrationOptionType { get; set; }
        public DateTime? ScheduledStartTime { get; set; }
        public DateTime? ScheduledEndTime { get; set; }
        public MigrationSettings MigrationSettings { get; set; }
        public string CreatedBy { get; set; } = Environment.UserName;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public enum MigrationOptionType
    {
        Instant,
        Scheduled
    }

    public class MigrationSettings
    {
        public int MaxParallelism { get; set; }
        public int DbFetchBatchSize { get; set; }
        public int ProcessingBatchSize { get; set; }
        public int RecordsToProcess { get; set; }
    }

    public enum JobStatus
    {
        Scheduled,
        Running,
        Completed,
        Failed,
        Cancelled
    }
}

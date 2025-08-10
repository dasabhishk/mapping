using System.Collections.Generic;

namespace CMMT.Models
{
    public class SchedulerConfig
    {
        public List<JobConfig> Jobs { get; set; }
    }

    public class JobConfig
    {
        public string Name { get; set; }
        public string CronExpression { get; set; }
        public int BatchSize { get; set; }
        public bool Enabled { get; set; }
    }
}

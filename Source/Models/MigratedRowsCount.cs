namespace CMMT.Models
{
    public class MigratedRowsCount(int studyCount, int seriesCount)
    {
        public int StudyCount { get; set; } = studyCount;
        public int SeriesCount { get; set; } = seriesCount;
    }
}

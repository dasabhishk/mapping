namespace CMMT.Models
{
    public class ColumnMapping
    {
        public string CsvColumn { get; set; } = string.Empty;
        public string DbColumn { get; set; } = string.Empty;
        public bool IsDerivedColumn { get; set; }
        public string? SourceColumnName { get; set; }
        public string? TransformationType { get; set; }
        public Dictionary<string, object>? TransformationParameters { get; set; }

        public static ColumnMapping Create(string csvColumnName, string dbColumnName)
        {
            return new ColumnMapping
            {
                CsvColumn = csvColumnName,
                DbColumn = dbColumnName,
                IsDerivedColumn = false,
                SourceColumnName = null,
                TransformationType = null,
                TransformationParameters = null
            };
        }

        public static ColumnMapping CreateForDerivedColumn(
            string derivedColumnName,
            string dbColumnName,
            string sourceColumnName,
            string transformationType,
            Dictionary<string, object>? transformationParameters)
        {
            return new ColumnMapping
            {
                CsvColumn = derivedColumnName,
                DbColumn = dbColumnName,
                IsDerivedColumn = true,
                SourceColumnName = sourceColumnName,
                TransformationType = transformationType,
                TransformationParameters = transformationParameters
            };
        }
    }
}
using CMMT.Models.Transformations;

namespace CMMT.Models
{
    /// <summary>
    /// Represents a virtual column derived from another CSV column through transformation
    /// </summary>
    public class DerivedColumn : CsvColumn
    {
        /// <summary>
        /// The name of the source column this derived column is based on
        /// </summary>
        public string SourceColumnName { get; set; } = string.Empty;

        /// <summary>
        /// The type of transformation applied
        /// </summary>
        public TransformationType TransformationType { get; set; }

        /// <summary>
        /// Parameters for the transformation
        /// </summary>
        public Dictionary<string, object> TransformationParameters { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Indicates this is a virtual column that doesn't exist in the original CSV
        /// </summary>
        public override bool IsVirtual => true;

        /// <summary>
        /// Creates a derived column from a source column
        /// </summary>

        /// <returns>A derived column</returns>
        public static DerivedColumn Create(
            CsvColumn sourceColumn,
            string newName,
            TransformationType transformationType,
            Dictionary<string, object> parameters)
        {
            var derivedColumn = new DerivedColumn
            {
                Name = newName,
                SourceColumnName = sourceColumn.Name,
                TransformationType = transformationType,
                TransformationParameters = parameters ?? new Dictionary<string, object>(),
                Index = sourceColumn.Index,
                InferredType = sourceColumn.InferredType
            };

            return derivedColumn;
        }
    }
}
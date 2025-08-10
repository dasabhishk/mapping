namespace CMMT.Models
{
    /// <summary>
    /// Represents a column from a CSV file
    /// </summary>
    public class CsvColumn
    {
        /// <summary>
        /// Name of the column
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Index of the column in the CSV file (zero-based)
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Inferred data type based on values
        /// </summary>
        public string InferredType { get; set; } = "string";

        /// <summary>
        /// Sample values from this column
        /// </summary>
        public List<string> SampleValues { get; set; } = new List<string>();

        /// <summary>
        /// Indicates if this is a virtual column that doesn't exist in the original CSV
        /// </summary>
        public virtual bool IsVirtual => false;
    }
}
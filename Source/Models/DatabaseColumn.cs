namespace CMMT.Models
{
    /// <summary>
    /// Represents a column in the database schema
    /// </summary>
    public class DatabaseColumn
    {
        /// <summary>
        /// Name of the column
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Data type of the column (e.g., NVARCHAR(50), INT, DATETIME2)
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// Whether the column is required for database operations
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Whether mapping is required for this column in the UI
        /// </summary>
        public bool IsMappingRequired { get; set; }

        /// <summary>
        /// Whether this column should be visible in the mapping UI
        /// </summary>
        public bool IsVisibleForMapping { get; set; } = true;

        /// <summary>
        /// Whether the column is nullable in the database
        /// </summary>
        public bool IsNullable { get; set; } = true;

        /// <summary>
        /// Whether the column is an identity column
        /// </summary>
        public bool IsIdentity { get; set; }

        /// <summary>
        /// Default value for the column
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// Maximum length for string columns
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Whether this column allows transformations
        /// </summary>
        public bool CanTransform { get; set; }
    }
}
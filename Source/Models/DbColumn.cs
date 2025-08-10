namespace CMMT.Models
{
    /// <summary>
    /// Represents a column from a database schema
    /// </summary>
    public class DbColumn
    {
        /// <summary>
        /// Name of the column
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Data type of the column
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// Whether the column is required
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Maximum length for string columns
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Validation pattern (regex) for the column
        /// </summary>
        public string? ValidationPattern { get; set; }

        /// <summary>
        /// Description of the column
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Implicit conversion from DbColumn to DatabaseColumn
        /// </summary>
        public static implicit operator DatabaseColumn(DbColumn dbColumn)
        {
            return new DatabaseColumn
            {
                Name = dbColumn.Name,
                DataType = dbColumn.DataType
            };
        }

        /// <summary>
        /// Implicit conversion from DatabaseColumn to DbColumn
        /// </summary>
        public static implicit operator DbColumn(DatabaseColumn databaseColumn)
        {
            return new DbColumn
            {
                Name = databaseColumn.Name,
                DataType = databaseColumn.DataType,
                IsRequired = false // Default value as DatabaseColumn doesn't have this
            };
        }
    }
}
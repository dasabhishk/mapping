namespace CMMT.Models
{
    public class TableSchema
    {
        /// <summary>
        /// Database schema name
        /// </summary>
        public string Schema { get; set; } = string.Empty;

        /// <summary>
        /// Name of the table
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Type of CSV file that maps to this table
        /// </summary>
        public string CsvType { get; set; } = string.Empty;

        /// <summary>
        /// Columns in this table
        /// </summary>
        public List<DatabaseColumn> Columns { get; set; } = new List<DatabaseColumn>();

        /// <summary>
        /// Primary key constraint information
        /// </summary>
        public PrimaryKeyConstraint? PrimaryKey { get; set; }
    }

    /// <summary>
    /// Represents primary key constraint information
    /// </summary>
    public class PrimaryKeyConstraint
    {
        /// <summary>
        /// Name of the primary key constraint
        /// </summary>
        public string ConstraintName { get; set; } = string.Empty;

        /// <summary>
        /// Column names that make up the primary key
        /// </summary>
        public List<string> ColumnNames { get; set; } = new List<string>();
    }
}

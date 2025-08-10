using System.Collections.Generic;

namespace CMMT.Models
{
    /// <summary>
    /// Represents the result of a mapping operation for a single CSV type to DB table
    /// </summary>
    public class MappingResult
    {
        /// <summary>
        /// Name of the database table
        /// </summary>
        public string TableName { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of CSV file that maps to this table
        /// </summary>
        public string CsvType { get; set; } = string.Empty;
        
        /// <summary>
        /// Column mappings from CSV to database
        /// </summary>
        public List<ColumnMapping> ColumnMappings { get; set; } = new List<ColumnMapping>();
    }
    
    /// <summary>
    /// Represents multiple mapping results for different CSV types
    /// </summary>
    public class MultiMappingResult
    {
        /// <summary>
        /// Collection of mapping results for different CSV types
        /// </summary>
        public List<MappingResult> Mappings { get; set; } = new List<MappingResult>();
    }
}
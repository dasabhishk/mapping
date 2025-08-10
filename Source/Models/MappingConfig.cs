namespace CMMT.Models
{
    public class MappingConfig
    {
        public List<TableMapping> Mappings { get; set; }
    }

    public class TableMapping
    {
        public string TableName { get; set; }
        public string CsvType { get; set; }
        public List<ColumnMapping> ColumnMappings { get; set; }
    }
}
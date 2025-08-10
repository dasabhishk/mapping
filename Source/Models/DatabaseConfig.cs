using System.Collections.Generic;

namespace CMMT.Models
{
    public class DatabaseConfig
    {
        public DbConnectionInfo StagingDatabase { get; set; }
        public DbConnectionInfo TargetDatabase { get; set; }
        public List<string> SupportedTargetDbVersions { get; set; }
        public MigrationConfig StudyMigrationProcess { get; set; }
        public MigrationConfig SeriesMigrationProcess { get; set; }
        public List<TableDefinition> Tables { get; set; }
    }

    public class DbConnectionInfo
    {
        public string Server { get; set; }
        public string Database { get; set; }
        public string Authentication { get; set; } // "Windows" or "Sql"
        public string User { get; set; }
        public string EncryptedPassword { get; set; }
        public string EncryptedConnectionString { get; set; }
    }
    public class MigrationConfig
    {
        public int DataFetchBatchSize { get; set; }
        public int RowsPerBatch { get; set; }
        public int MaxParallelism { get; set; }
        public int TotalNoOfRecordsToMigrate { get; set; }
    }

    public class TableDefinition
    {
        public string Schema { get; set; }
        public string TableName { get; set; }
        public string CsvType { get; set; }
        public List<ColumnDefinition> Columns { get; set; }
        public PrimaryKeyDefinition? PrimaryKey { get; set; }
        public List<ForeignKeyDefinition> ForeignKeys { get; set; }
    }

    public class ColumnDefinition
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public bool IsNullable { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsRequired { get; set; }
        public bool IsMappingRequired { get; set; }
        public bool IsVisibleForMapping { get; set; }
        public bool CanTransform { get; set; }
        public string? DefaultValue { get; set; }
    }

    public class PrimaryKeyDefinition
    {
        public string ConstraintName { get; set; }
        public List<string> ColumnNames { get; set; }
    }

    public class ForeignKeyDefinition
    {
        public string ConstraintName { get; set; }
        public string ReferencedTableName { get; set; }
        public string ReferencedColumnName { get; set; }
        public List<string> ColumnNames { get; set; }
    }
}

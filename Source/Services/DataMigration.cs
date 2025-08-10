using System.Data;
using CMMT.dao;
using CMMT.Models;
using CMMT.Helpers;
using System.Diagnostics.Eventing.Reader;
using System.Data.Entity.Core.Mapping;

namespace CMMT.Services
{
    public abstract class DataMigration : IMigrationService
    {
        protected CancellationToken _cancellationToken;
        protected string _stagingDbConnectionString;
        protected string _targetDbConnectionString;
        protected BatchStatusUpdater _batchStatusUpdater;
        protected MigrationBatchProcessor _migrationBatchProcessor;
        protected int _maxParallelism;
        protected int _dbFetchBatchSize;
        protected int _processingBatchSize;
        protected int _recordsToProcess;
        protected readonly DatabaseConfig _dbConfig;
        protected TableDefinition _tableDefinition;
        protected string _fullTableName;
        protected string _csvType;
        protected DataMigration(DatabaseConfig dbConfig,string CsvType)
        {
            _dbConfig = dbConfig;
            _csvType = CsvType;
        }
        public static DataMigration CreateMigration(DatabaseConfig dbConfig, string csvType)
        {
            var appsettings = AppSettingsModel.GetInstance;
            return csvType switch
            {
                var t when t == appsettings.Settings.CsvTypes.PatientStudy => new StudyDataMigration(dbConfig, csvType),
                var t when t == appsettings.Settings.CsvTypes.SeriesInstance => new SeriesDataMigration(dbConfig, csvType),
                _ => throw new Exception($"Unsupported CsvType: {csvType}")
            };
        }
        public virtual void Initialize(string csvType)
        {
            try
            {
                _stagingDbConnectionString = SecureStringHelper.Decrypt(_dbConfig.StagingDatabase.EncryptedConnectionString);
                _targetDbConnectionString = SecureStringHelper.Decrypt(_dbConfig.TargetDatabase.EncryptedConnectionString);
                var appSettings = AppSettingsModel.GetInstance;
                MigrationConfig _migrationConfig = csvType switch
                {
                    var t when t == appSettings.Settings.CsvTypes.PatientStudy => _dbConfig.StudyMigrationProcess,
                    var t when t == appSettings.Settings.CsvTypes.SeriesInstance => _dbConfig.SeriesMigrationProcess,
                    _ => throw new Exception($"Unsupported CsvType: {csvType}")
                };
                _maxParallelism = _migrationConfig.MaxParallelism;
                _dbFetchBatchSize = _migrationConfig.DataFetchBatchSize;
                _processingBatchSize = _migrationConfig.RowsPerBatch;
                _recordsToProcess = _migrationConfig.TotalNoOfRecordsToMigrate;
                _tableDefinition = _dbConfig.Tables.FirstOrDefault(t => t.CsvType == csvType);
                _fullTableName = $"{_tableDefinition.Schema}.{_tableDefinition.TableName}";
                _batchStatusUpdater = new BatchStatusUpdater(_stagingDbConnectionString);
                _migrationBatchProcessor = new MigrationBatchProcessor(_targetDbConnectionString, _stagingDbConnectionString);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error initializing DataMigration for CSV type '{csvType}': {ex.Message}", ex);
                throw;
            }
        }
        public virtual DataTable FetchNextStagingBatch()
        {

            using var stagingDbLayer = new DBLayer(_stagingDbConnectionString);
            stagingDbLayer.Connect(false);

            DataTable stagingData;
            string sql = $@"
                            SELECT *
                            FROM {_fullTableName}
                            WHERE status = 'V'
                            ORDER BY id
                            OFFSET 0 ROWS FETCH NEXT {_dbFetchBatchSize} ROWS ONLY";
            try
            {
                stagingDbLayer.Execute_Query_DataTable(sql, out stagingData);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error fetching data from staging database Exception: {ex.Message}", ex);
                throw;
            }
            return stagingData;
        }
        public virtual IEnumerable<List<ProcedureParameters>> GetBatches(List<DataRow> rows, int batchSize)
        {
            var _parameterList = rows.Select(row => new ProcedureParameters
            {
                Parameters = BuildParameters(row),
                FileName = row.Table.Columns.Contains("filename") && !row.IsNull("filename") ? row.Field<string>("filename") ?? string.Empty : string.Empty,
                RowNumber = row.Table.Columns.Contains("RowNumber") && !row.IsNull("RowNumber") ? row.Field<int>("RowNumber") : 0,
                Id = row.Table.Columns.Contains("Id") && !row.IsNull("Id") ? row["Id"] : null
            }).ToList();
            for (int i = 0; i < _parameterList.Count; i += batchSize)
            {
                yield return _parameterList.GetRange(i, Math.Min(batchSize, _parameterList.Count - i));
            }

        }
        public abstract Task<bool> ProcessDataInBatchesAsync(CancellationToken cancellationToken = default);
        public abstract DBParameters BuildParameters(DataRow row);
    }
}
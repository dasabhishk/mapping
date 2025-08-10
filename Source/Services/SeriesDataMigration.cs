using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CMMT.dao;
using CMMT.Models;

namespace CMMT.Services
{
    public class SeriesDataMigration : DataMigration
    {
        public SeriesDataMigration(DatabaseConfig dbConfig, string CsvType) : base(dbConfig, CsvType)
        {
            Initialize(_csvType);
            LoggingService.LogInfo(
                                   $"Initialized StudyData migration parameters: " +
                                   $"StagingDb='{_stagingDbConnectionString}', " +
                                   $"TargetDb='{_targetDbConnectionString}', " +
                                   $"MaxParallelism={_maxParallelism}, " +
                                   $"DbFetchBatchSize={_dbFetchBatchSize}, " +
                                   $"ProcessingBatchSize={_processingBatchSize}, " +
                                   $"Table={_fullTableName}");
        }

        public override async Task<bool> ProcessDataInBatchesAsync(CancellationToken cancellationToken = default)
        {
            return true;
        }
        public override DBParameters BuildParameters(DataRow row)
        {
            return null;
        }
    }
}

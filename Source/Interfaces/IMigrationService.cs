using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using CMMT.dao;

namespace CMMT.Services
{
    public interface IMigrationService
    {
        public void Initialize(string csvType);
        public Task<bool> ProcessDataInBatchesAsync(CancellationToken cancellationToken = default);

        public DBParameters BuildParameters(DataRow row);

        public DataTable FetchNextStagingBatch();
        public IEnumerable<List<ProcedureParameters>> GetBatches(List<DataRow> rows, int batchSize);
    }
}
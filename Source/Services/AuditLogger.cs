using System.Threading.Tasks;
using CMMT.dao;

namespace CMMT.Services
{
    public class AuditLogger
    {
        private readonly DBLayer _dbLayer;
        private readonly string _connStr;

        public AuditLogger(DBLayer dbLayer, string connStr)
        {
            _dbLayer = dbLayer;
            _connStr = connStr;
        }

        public async Task LogImportAsync(string fileName, int recordsImported, string user)
        {
            string sql = "INSERT INTO AuditLog (EventType, FileName, Records, UserName, Timestamp) VALUES (@type, @file, @count, @user, GETDATE())";
            //var parameters = new[]
            //{
            //    new { Name = "@type", Value = "Import" },
            //    new { Name = "@file", Value = fileName },
            //    new { Name = "@count", Value = recordsImported },
            //    new { Name = "@user", Value = user }
            //};
            //await _dbLayer.Execute_SP(_connStr, sql, parameters);
        }
    }
}

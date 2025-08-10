using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMMT.Models
{
    public class MigrationReportRow
    {
        public string FileName { get; init; } = "";
        public int TotalRows { get; init; }
        public int ValidRows { get; init; }
        public int InvalidRows { get; init; }
        public int MigratedRows { get; init; }
        public int ErrorRows { get; init; }
        public DataTable? InvalidRowsTable { get; init; }
        public DataTable? ErrorRowsTable { get; init; }
    }
}

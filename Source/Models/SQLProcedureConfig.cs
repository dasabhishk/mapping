using System.Collections.Generic;

namespace CMMT.Models
{
    public class SqlProcedureConfig
    {
        public Dictionary<string, List<string>> Procedures { get; set; }
        public Dictionary<string, List<string>> Queries { get; set; }
    }
}

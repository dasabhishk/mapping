using System.IO;
using CMMT.Models;

namespace CMMT.Helpers
{
    public static class DBScriptsHelper
    {
        private static readonly SqlProcedureConfig _config;

        static DBScriptsHelper()
        {
            var json = File.ReadAllText(ConfigFileHelper.GetConfigFilePath("Configuration", "SQLProcedure.json"));
            _config = System.Text.Json.JsonSerializer.Deserialize<SqlProcedureConfig>(json)
                ?? throw new InvalidOperationException("Failed to parse SQLProcedure.json");
        }
        public static string GetQuery(string queryName)
        {
            if (_config.Queries.TryGetValue(queryName, out var queries))
            {
                return queries.FirstOrDefault() ?? string.Empty;
            }
            throw new KeyNotFoundException($"Query '{queryName}' not found in configuration.");
        }

        public static string GetProcedures(string procedureName)
        {
            if (_config.Procedures.TryGetValue(procedureName, out var procedures))
            {
                return procedures.FirstOrDefault() ?? string.Empty;
            }
            throw new KeyNotFoundException($"Procedure '{procedureName}' not found in configuration.");
        }
    }
}

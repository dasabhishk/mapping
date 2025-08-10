using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using CMMT.dao;
using CMMT.Helpers;
using CMMT.Models;
using Quartz.Util;
using Serilog;

namespace CMMT.Services
{
    public class DatabaseService
    {
        private readonly DBLayer _dbLayer;
        SqlProcedureConfig?  SQLConfig;
        private string pathForStoredProcedure;

        public DatabaseService(DBLayer dbLayer)
        {
            _dbLayer = dbLayer;
        }

        public async Task<bool> EnsureStagingDbReadyAsync(DatabaseConfig config)
        {
            if (config == null)
            {
                LoggingService.LogWarning("StagingDbService: Database configuration is null. Please check dbconfig.json. Operation aborted.", false);
                return false;
            }

            try
            {
                LoggingService.LogInfo("StagingDbService: Attempting to connect to the database...", false);
                if (!_dbLayer.Connect(false))
                {
                    LoggingService.LogWarning($"Unable to connect to Staging database. Verify connection settings. {config.StagingDatabase.EncryptedConnectionString}", false);
                    return false;
                }
                LoggingService.LogInfo("StagingDbService: Connected to database successfully.", false);

                foreach (var table in config.Tables)
                {
                    try
                    {
                        if (!SchemaExists(table.Schema))
                        {
                            var ddl = $"CREATE SCHEMA [{table.Schema}]";
                            LoggingService.LogInfo($"StagingDbService: Creating schema: {ddl}", false);
                            _dbLayer.Execute_Query(ddl);
                        }

                        if (!TableExists(table.Schema, table.TableName) && table.TableName!= "cmmt_StudyType")
                        {
                            var ddl = GenerateCreateTableScript(table);
                            LoggingService.LogInfo($"StagingDbService: Creating table: {ddl}", false);
                            _dbLayer.Execute_Query(ddl);
                        }
                    }
                    catch (SqlException ex)
                    {
                        LoggingService.LogError($"Database error while preparing table {table.Schema}.{table.TableName}.", ex, false);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError("Unexpected error while preparing staging database.", ex, false);
                        return false;
                    }
                }
                //Once after table creation is completed, creation of stored procedure implementation is done
                // reload config so we get the database name
                string dbName = config.StagingDatabase.Database;
                try
                {
                    // Accessing the path to json file and parsing it
                    pathForStoredProcedure = ConfigFileHelper.GetConfigFilePath("Configuration", "SQLProcedure.json");
                    string json = File.ReadAllText(pathForStoredProcedure);
                    SQLConfig = JsonSerializer.Deserialize<SqlProcedureConfig>(json);
                    _dbLayer.Execute_Query($"USE [{dbName}];");   //Run all the sql command in this DB

                    if (SQLConfig == null || SQLConfig.Procedures == null || SQLConfig.Procedures.Count == 0)
                    {
                        LoggingService.LogError("No procedures found in SQLProcedure.json", null, false);
                        return false;
                    }

                    await ProcedureCreationFromJson(_dbLayer, pathForStoredProcedure, SQLConfig, false);  //Deploying procedures to the database
                    LoggingService.LogInfo("Stored-procedures deployed successfully");

                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Error reading SQLProcedure.json", ex, true);
                    return false;
                }
            }
            catch (SqlException ex)
            {
                LoggingService.LogError("Database error while preparing staging database", ex, false);
                return false;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Unexpected error while preparing staging database", ex, false);
                return false;
            }
            finally
            {
                _dbLayer.Disconnect();
                Log.Information("StagingDbService: Database connection closed.");
            }

            LoggingService.LogInfo("StagingDbService: Staging database is ready.", false);
            return true;
        }

        private bool SchemaExists(string schemaName)
        {
            var query = $"SELECT name FROM sys.schemas WHERE name = '{schemaName}'";
            var result = _dbLayer.ExecuteScalar_Query(query);
            return !string.IsNullOrWhiteSpace(result as string);
        }

        private bool TableExists(string schema, string tableName)
        {
            var fullTableName = $"[{schema}].[{tableName}]";
            var query = $"SELECT name FROM sys.objects WHERE object_id = OBJECT_ID('{fullTableName}') AND type = 'U'";
            var result = _dbLayer.ExecuteScalar_Query(query);
            return !string.IsNullOrWhiteSpace(result as string);
        }

        private string GenerateCreateTableScript(TableDefinition table)
        {
            var columns = string.Join(", ",
                table.Columns
                .Where(col => col.IsRequired)
                .Select(col =>
                    $"[{col.Name}] {col.DataType}" +
                    (col.IsNullable ? "" : " NOT NULL") +
                    (col.IsIdentity ? " IDENTITY(1,1)" : "") +
                    (col.DefaultValue != null ? $" DEFAULT {col.DefaultValue}" : "")
                ));

            string pkConstraint = string.Empty;
            if (table.PrimaryKey != null && table.PrimaryKey.ColumnNames.Count > 0)
            {
                var pkColumns = string.Join(", ", table.PrimaryKey.ColumnNames.Select(c => $"[{c}]"));
                var pkName = !string.IsNullOrWhiteSpace(table.PrimaryKey.ConstraintName)
                    ? table.PrimaryKey.ConstraintName
                    : $"PK_{table.TableName}";
                pkConstraint = $",\n    CONSTRAINT [{pkName}] PRIMARY KEY ({pkColumns})";
            }

            string fkConstraints = string.Empty;
            if (table.ForeignKeys != null && table.ForeignKeys.Count > 0)
            {
                fkConstraints = string.Join(",\n    ",
                    table.ForeignKeys.Select(fk =>
                    {
                        var fkColumns = string.Join(", ", fk.ColumnNames.Select(c => $"[{c}]"));
                        var fkName = !string.IsNullOrWhiteSpace(fk.ConstraintName)
                            ? fk.ConstraintName
                            : $"FK_{table.TableName}_{string.Join("_", fk.ColumnNames)}";
                        return $"CONSTRAINT [{fkName}] FOREIGN KEY ({fkColumns}) REFERENCES [{table.Schema}].[{fk.ReferencedTableName}]([{fk.ReferencedColumnName}])";
                    })
                );
                if (!string.IsNullOrEmpty(fkConstraints))
                    fkConstraints = ",\n    " + fkConstraints;
            }

            return $"CREATE TABLE [{table.Schema}].[{table.TableName}] (\n    {columns}{pkConstraint}{fkConstraints}\n);";
        }

        public static async Task<bool> DatabaseExists(string connectionString, string dbName)
        {
            DBLayer oDB = new(connectionString);
            string query = "SELECT database_id FROM sys.databases WHERE name = @DatabaseName";
            var parameters = new DBParameters();
            parameters.Add("@DatabaseName", dbName, ParameterDirection.Input, SqlDbType.NVarChar, 128);
            int result = 0;

            try
            {
                oDB.Connect(false);
                result = await oDB.ExecuteQuery_WithParamsAsync<int>(query, parameters);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error while checking if database exists.", ex);
            }
            finally
            {
                if (oDB != null)
                {
                    oDB.Disconnect();
                    oDB.Dispose();
                }
            }
            return result > 0;
        }

        public static void CreateDatabase(string connectionString, string dbName)
        {
            DBLayer oDB = new(connectionString);

            try
            {
                oDB.Connect(false);
                string query = $"CREATE DATABASE [{dbName}]";
                oDB.Execute_Query(query);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to create database [{dbName}].", ex);
                throw;
            }
            finally
            {
                oDB.Disconnect();
                oDB.Dispose();
            }
        }

        public static async Task<List<string>> LoadDatabases(string connectionString)
        {
            var databases = new List<string>();
            DBLayer oDB = new(connectionString);

            try
            {
                oDB.Connect(false);
                string query = "SELECT name FROM sys.databases WHERE name LIKE 'staging_migration%' AND database_id > 4 ORDER BY create_date DESC";

                using SqlDataReader reader = await oDB.ExecuteReader_QueryAsync(query);
                while (await reader.ReadAsync())
                {
                    databases.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error while loading databases.", ex, true);
            }
            finally
            {
                if (oDB != null)
                {
                    oDB.Disconnect();
                    oDB.Dispose();
                }
            }
            return databases;
        }

        public static async Task<List<string>> LoadTargetDatabases(string connectionString)
        {
            var databases = new List<string>();
            DBLayer oDB = new(connectionString);

            try
            {
                oDB.Connect(false);

                string query =
                    "SELECT name FROM sys.databases " +
                    "WHERE database_id > 4 ORDER BY name";

                using SqlDataReader reader = await oDB.ExecuteReader_QueryAsync(query);

                while (await reader.ReadAsync())
                {
                    databases.Add(reader.GetString(0));
                }

                reader.Close(); // optional due to using
                oDB.Disconnect();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error occurred while loading databases.", ex, true);
            }
            finally
            {
                if (oDB != null)
                {
                    oDB.Disconnect();
                    oDB.Dispose();
                }
            }

            return databases;
        }
        internal static async Task ProcedureCreationFromJson(
            DBLayer db,
            string jsonFilePath,
            SqlProcedureConfig? SQLConfig,
            bool isValidationServiceCall)
        {
            try
            {
                if (!File.Exists(jsonFilePath))
                    throw new FileNotFoundException("Procedure JSON not found", jsonFilePath);

                var procedures = new Dictionary<string, List<string>>();

                // Load & deserialize JSON
                if (isValidationServiceCall)
                {
                  procedures = SQLConfig.Procedures.Take(6).ToDictionary(
                  kvp => kvp.Key,
                  kvp => kvp.Value);
                }
              else
                {
                    procedures = SQLConfig.Procedures;
                }
                var compareString = StringComparer.OrdinalIgnoreCase;

                // Deploy all procedures
                foreach (var proc in procedures)
                {
                    var procName = proc.Key;
                    var lines = proc.Value;            // array of lines
                    var script = string.Join(Environment.NewLine, lines);
                    ExecuteSql(db, procName, script);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error occurred while creating procedures.", ex, true);
                throw new Exception("Error when creating procedures");
            }
        }


        /* Registers stored procedure in the connected database by trimming white spaces,
        GO statements, skipping empty chunks and stripping trailing semicolons */
        static void ExecuteSql(DBLayer db, string procedureName, string sql)

        {
            LoggingService.LogInfo($"Deploying {procedureName}", false);

            var procedures = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Select(ch => ch.Trim())
                .Where(ch => !string.IsNullOrWhiteSpace(ch))
                .Select(ch => ch.TrimEnd(';').Trim());
            foreach (var procedure in procedures)
                db.Execute_Query(procedure);
        }

        public static async Task<List<string>> LoadDistinctInstituteNamesAsync(string connectionString)
        {
            var institutes = new List<string>();
            var decryptedConnStr = SecureStringHelper.Decrypt(connectionString);
            var dbLayer = new DBLayer(decryptedConnStr);

            try
            {
                dbLayer.Connect(false);
                string query = DBScriptsHelper.GetQuery("LoadDistinctInstitutionNames");

                using SqlDataReader reader = await dbLayer.ExecuteReader_QueryAsync(query);

                while (await reader.ReadAsync())
                {
                    string? name = reader["InstitutionName"] as string;
                    if (!string.IsNullOrWhiteSpace(name))
                        institutes.Add(name.Trim());
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error occurred while loading distinct institute names.", ex, true);
            }
            finally
            {
                dbLayer?.Disconnect();
                dbLayer?.Dispose();
            }

            return institutes;
        }

        public static async Task<List<string>> LoadDistinctStudyTypesAsync(string connectionString)
        {
            var studyTypes = new List<string>();
            var decryptedConnStr = SecureStringHelper.Decrypt(connectionString);
            var dbLayer = new DBLayer(decryptedConnStr);

            try
            {
                dbLayer.Connect(false);
                string query = DBScriptsHelper.GetQuery("LoadDistinctStudyTypeNames");

                using SqlDataReader reader = await dbLayer.ExecuteReader_QueryAsync(query);

                while (await reader.ReadAsync())
                {
                    string? name = reader["StudyTypeName"] as string;
                    if (!string.IsNullOrWhiteSpace(name))
                        studyTypes.Add(name.Trim());
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error occurred while loading distinct study type names.", ex, true);
            }
            finally
            {
                dbLayer?.Disconnect();
                dbLayer?.Dispose();
            }

            return studyTypes;
        }
        public static List<ArchiveType> LoadArchiveTypes(string connectionString)
        {
            var archiveTypes = new List<ArchiveType>();
            var decryptedConnStr = SecureStringHelper.Decrypt(connectionString);
            var dbLayer = new DBLayer(decryptedConnStr);

            try
            {
                dbLayer.Connect(false);

                string query = "SELECT sl.storageLocDBKey, sl.accessProtocol, sl.hostName, sl.rootPath, sap.identifier " +
                    "FROM Xcelera.dbo._STORAGE_LOC AS sl " +
                    "INNER JOIN Xcelera.dbo._STORAGE_ACCESS_PROTOCOL AS sap " +
                    "ON sl.AccessProtocol = sap.AccessProtocol " +
                    "WHERE sl.StorageType = 1 AND sl.AccessType = 1 AND sl.ServerStatus = 1";

                SqlDataReader reader = null;
                dbLayer.ExecuteReader_Query(query, out reader);

                while (reader.Read())
                {

                    string Identifier = reader.GetString("hostName") + ";" + reader.GetString("rootPath") + ";" + reader.GetString("identifier");
                    archiveTypes.Add(new ArchiveType { StorageLocDBKey = reader.GetInt32("storageLocDBKey"), Identifier = Identifier });
                }

            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error occurred while loading archive types.", ex, true);
            }
            finally
            {
                if (dbLayer != null)
                {
                    dbLayer.Disconnect();
                    dbLayer.Dispose();
                }
            }

            return archiveTypes;
        }

        /// <summary>
        /// Gets the list of processed files from the database.
        /// </summary>
        /// <param name="fileNames"></param>
        /// <returns>file names</returns>
        public List<string> GetProcessedFiles(List<string> fileNames)
        {
            var processedFiles = new List<string>();
            var fileNamesParam = string.Join(",", fileNames); // comma-separated string
            var parameters = new DBParameters();
            parameters.Add("@FileNames", fileNamesParam, ParameterDirection.Input, SqlDbType.NVarChar);
            _dbLayer.Connect(false);
            _dbLayer.Execute_SP_Reader("cmmt.GetProcessedFileNames", parameters, out var reader);
            try
            {
                while (reader.Read())
                {
                    string processedFile = reader.GetString(reader.GetOrdinal("FileName"));
                    processedFiles.Add(processedFile);
                }
            }
            finally
            {
                reader.Close();
                _dbLayer.Disconnect();
            }
            return processedFiles;
        }
    }
}    


// CMMT.Services/SchemaLoaderService.cs

using CMMT.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace CMMT.Services
{
    /// <summary>
    /// Service for loading database schema from JSON file
    /// </summary>
    public class SchemaLoaderService : ISchemaLoaderService
    {
        /// <summary>
        /// Loads database schema from the default dbconfig.json file in Configuration folder
        /// </summary>
        /// <returns>The database schema with tables and columns</returns>
        public async Task<DatabaseSchema> LoadSchemaAsync()
        {
            var config = await LoadDatabaseConfigAsync(); // Use the public method to load config

            if (config == null)
            {
                LoggingService.LogError("Failed to load DatabaseConfig. Cannot generate DatabaseSchema.",null);
                return null; // Or throw an appropriate exception
            }

            // Convert DatabaseConfig to DatabaseSchema
            var schema = ConvertToSchema(config);

            LoggingService.LogInfo($"Schema loaded successfully: {schema.Tables?.Count ?? 0} tables found");

            return schema;
        }

        /// <summary>
        /// Loads raw DatabaseConfig from the default dbconfig.json file in Configuration folder
        /// </summary>
        /// <returns>The raw DatabaseConfig object</returns>
        public async Task<DatabaseConfig> LoadDatabaseConfigAsync()
        {
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string currentDirectory = Directory.GetCurrentDirectory();

            var pathsToTry = new List<string>
            {
                Path.Combine(appDirectory, "Configuration", "dbconfig.json"),
                Path.Combine(currentDirectory, "Configuration", "dbconfig.json"),
                Path.Combine("Configuration", "dbconfig.json"),
                Path.Combine(appDirectory, "..", "..", "..", "Configuration", "dbconfig.json"),
                Path.Combine(currentDirectory, "..", "..", "..", "Configuration", "dbconfig.json")
            };

            LoggingService.LogInfo($"Attempting to load dbconfig.json for raw config.");

            foreach (var path in pathsToTry)
            {
                var fullPath = Path.GetFullPath(path);
                LoggingService.LogInfo($"Trying path: {fullPath}");

                if (File.Exists(fullPath))
                {
                    LoggingService.LogInfo($"Found config file at: {fullPath}");
                    return await LoadDatabaseConfigAsync(fullPath);
                }
            }

            LoggingService.LogWarning("dbconfig.json not found. Checked paths:");
            foreach (var path in pathsToTry)
            {
                LoggingService.LogInfo($"  Path: {Path.GetFullPath(path)}");
            }

            throw new FileNotFoundException($"Config file 'dbconfig.json' not found in Configuration folder. Tried {pathsToTry.Count} different locations.");
        }


        /// <summary>
        /// Loads database schema from a JSON file 
        /// </summary>
        /// <param name="filePath">Path to the schema JSON file</param>
        /// <returns>The database schema with tables and columns</returns>
        public async Task<DatabaseSchema> LoadSchemaAsync(string filePath)
        {
            // Now calls the public LoadDatabaseConfigAsync(string filePath)
            var databaseConfig = await LoadDatabaseConfigAsync(filePath);

            if (databaseConfig == null)
            {
                throw new InvalidOperationException($"Failed to load DatabaseConfig from {filePath}. Cannot create schema.");
            }

            // Convert DatabaseConfig to DatabaseSchema
            var schema = ConvertToSchema(databaseConfig);

            LoggingService.LogInfo($"Schema loaded successfully from {filePath}: {schema.Tables?.Count ?? 0} tables found");

            return schema;
        }

        /// <summary>
        /// Loads raw DatabaseConfig from a specified JSON file path. (Now a PUBLIC method)
        /// </summary>
        /// <param name="filePath">Path to the config JSON file</param>
        /// <returns>The DatabaseConfig object</returns>
        public async Task<DatabaseConfig> LoadDatabaseConfigAsync(string filePath) // <--- Changed to public
        {
            LoggingService.LogInfo($"Attempting to load DatabaseConfig from: {filePath}");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"The specified config file was not found at: {Path.GetFullPath(filePath)}", filePath);
            }

            try
            {
                string jsonContent = await File.ReadAllTextAsync(filePath);

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    throw new InvalidOperationException("Config file is empty.");
                }

                var databaseConfig = JsonConvert.DeserializeObject<DatabaseConfig>(jsonContent);

                if (databaseConfig == null)
                {
                    LoggingService.LogInfo("Deserialization of DatabaseConfig returned null");
                    throw new InvalidOperationException("Failed to deserialize the config file to a valid DatabaseConfig object.");
                }

                LoggingService.LogInfo($"Loaded Staging Database: {databaseConfig.StagingDatabase?.Database}");
                LoggingService.LogInfo($"Loaded Target Database: {databaseConfig.TargetDatabase?.Database}");


                return databaseConfig;
            }
            catch (JsonException jsonEx)
            {
                LoggingService.LogError($"JSON parsing error in config file at path '{filePath}': {jsonEx.Message}", jsonEx);
                throw new Exception($"JSON parsing error in config file at path '{filePath}': {jsonEx.Message}", jsonEx);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error loading config file: {ex.Message}", ex);
                throw new Exception($"Error loading config file: {ex.Message}", ex);
            }
        }


        private DatabaseSchema ConvertToSchema(DatabaseConfig databaseConfig)
        {
            var schema = new DatabaseSchema
            {
                DatabaseName = databaseConfig.StagingDatabase?.Database ?? "Unknown",
                Tables = databaseConfig.Tables?.Select(tableConfig => new TableSchema
                {
                    Schema = tableConfig.Schema,
                    TableName = tableConfig.TableName,
                    CsvType = tableConfig.CsvType,
                    Columns = tableConfig.Columns?.Where(columnConfig => columnConfig.IsVisibleForMapping == true).Select(columnConfig => new DatabaseColumn
                    {
                        Name = columnConfig.Name,
                        DataType = columnConfig.DataType,
                        IsRequired = columnConfig.IsRequired,
                        IsMappingRequired = columnConfig.IsMappingRequired,
                        IsVisibleForMapping = columnConfig.IsVisibleForMapping,
                        IsNullable = columnConfig.IsNullable,
                        IsIdentity = columnConfig.IsIdentity,
                        DefaultValue = columnConfig.DefaultValue,
                        CanTransform = columnConfig.CanTransform
                    }).ToList()
                }).ToList()
            };

            return schema;
        }
    }
}
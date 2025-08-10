using CMMT.Models;

namespace CMMT.Services
{
    /// <summary>
    /// Service interface for loading database schema
    /// </summary>
    public interface ISchemaLoaderService
    {
        /// <summary>
        /// Loads database schema from the default dbconfig.json file in Configuration folder
        /// </summary>
        /// <returns>The database schema with tables and columns</returns>
        Task<DatabaseSchema> LoadSchemaAsync();
        Task<DatabaseConfig> LoadDatabaseConfigAsync();

        /// <summary>
        /// Loads database schema from a JSON file
        /// </summary>
        /// <param name="filePath">Path to the schema JSON file</param>
        /// <returns>The database schema with tables and columns</returns>
        Task<DatabaseSchema> LoadSchemaAsync(string filePath);
    }
}
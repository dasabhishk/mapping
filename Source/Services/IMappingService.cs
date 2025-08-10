using CMMT.Models;
using CMMT.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CMMT.Services
{
    /// <summary>
    /// Service interface for managing CSV to database mappings
    /// </summary>
    public interface IMappingService
    {
        /// <summary>
        /// Attempts to automatically match CSV columns to database columns
        /// based on name similarity
        /// </summary>
        /// <param name="csvColumns">Collection of CSV columns</param>
        /// <param name="dbColumns">List of database columns</param>
        /// <returns>Dictionary mapping database column names to matched CSV column names</returns>
        Dictionary<string, string> AutoMatchColumns(ObservableCollection<CsvColumn> csvColumns, List<DatabaseColumn> dbColumns);
        
        /// <summary>
        /// Validates a mapping between CSV columns and database columns
        /// </summary>
        /// <param name="mappingViewModels">List of column mapping view models</param>
        /// <param name="csvColumns">Collection of CSV columns</param>
        /// <param name="dbColumns">List of database columns</param>
        /// <returns>Dictionary of validation errors (db column name -> error message)</returns>
        Dictionary<string, string> ValidateMappings(
            List<ColumnMappingViewModel> mappingViewModels, 
            ObservableCollection<CsvColumn> csvColumns, 
            List<DatabaseColumn> dbColumns);
        
        /// <summary>
        /// Loads mappings from a JSON file
        /// </summary>
        /// <param name="filePath">Path to the mappings file</param>
        /// <returns>MultiMappingResult containing all mappings</returns>
        Task<MultiMappingResult> LoadMappingsAsync(string filePath);
        
        /// <summary>
        /// Saves mappings to a JSON file
        /// </summary>
        /// <param name="mappings">MultiMappingResult containing all mappings</param>
        /// <param name="filePath">Path to the mappings file</param>
        /// <returns>True if saving was successful</returns>
        Task<bool> SaveMultiMappingsAsync(MultiMappingResult mappings, string filePath);
        
        /// <summary>
        /// Creates a derived column by applying a transformation to a source column
        /// </summary>
        /// <param name="sourceColumn">The source column</param>
        /// <param name="newColumnName">Name for the derived column</param>
        /// <param name="transformationType">Type of transformation to apply</param>
        /// <param name="parameters">Transformation parameters</param>
        /// <returns>The derived column</returns>
        DerivedColumn CreateDerivedColumn(
            CsvColumn sourceColumn, 
            string newColumnName, 
            Models.Transformations.TransformationType transformationType, 
            Dictionary<string, object> parameters);
        
        /// <summary>
        /// Recreates a derived column from a saved mapping
        /// </summary>
        /// <param name="sourceColumn">The source column</param>
        /// <param name="derivedColumnName">The name for the derived column</param>
        /// <param name="transformationType">Type of transformation as a string</param>
        /// <param name="transformationParametersJson">JSON string of parameters</param>
        /// <returns>The derived column or null if recreation fails</returns>
        DerivedColumn? RecreateTransformedColumn(
            CsvColumn sourceColumn,
            string derivedColumnName,
            string transformationType,
            string transformationParametersJson);
    }
}
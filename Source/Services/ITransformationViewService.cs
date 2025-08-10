using CMMT.Models;
using CMMT.Models.Transformations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CMMT.Services
{
    /// <summary>
    /// Service interface for managing column transformations
    /// </summary>
    public interface ITransformationViewService
    {
        /// <summary>
        /// Gets a transformation implementation by type
        /// </summary>
        /// <param name="type">The transformation type</param>
        /// <returns>The transformation implementation</returns>
        ITransformation GetTransformation(TransformationType type);
        
        /// <summary>
        /// Creates a transformation instance with default parameters
        /// </summary>
        /// <param name="type">The transformation type</param>
        /// <param name="sourceColumn">The source column name</param>
        /// <returns>The transformation implementation</returns>
        ITransformation CreateTransformation(TransformationType type, string sourceColumn);
        
        /// <summary>
        /// Creates a derived column by applying a transformation to a source column
        /// </summary>
        /// <param name="sourceColumn">The source column</param>
        /// <param name="newColumnName">Name for the derived column</param>
        /// <param name="transformationType">Type of transformation to apply</param>
        /// <param name="parameters">Transformation parameters</param>
        /// <returns>The derived column with sample values transformed</returns>
        DerivedColumn CreateDerivedColumn(
            CsvColumn sourceColumn, 
            string newColumnName, 
            TransformationType transformationType, 
            Dictionary<string, object> parameters);
        
        /// <summary>
        /// Applies a transformation to sample values
        /// </summary>
        /// <param name="sampleValues">Values to transform</param>
        /// <param name="transformationType">Type of transformation</param>
        /// <param name="parameters">Transformation parameters</param>
        /// <returns>Transformed values</returns>
        List<string> TransformSamples(
            List<string> sampleValues, 
            TransformationType transformationType, 
            Dictionary<string, object> parameters);
        
        /// <summary>
        /// Validates transformation parameters
        /// </summary>
        /// <param name="transformationType">Type of transformation</param>
        /// <param name="parameters">Parameters to validate</param>
        /// <param name="error">Error message if validation fails</param>
        /// <returns>True if parameters are valid</returns>
        bool ValidateTransformationParameters(
            TransformationType transformationType, 
            Dictionary<string, object> parameters, 
            out string error);
        
        /// <summary>
        /// Gets a description of a transformation with the given parameters
        /// </summary>
        /// <param name="transformationType">Type of transformation</param>
        /// <param name="parameters">Transformation parameters</param>
        /// <returns>A human-readable description</returns>
        string GetTransformationDescription(
            TransformationType transformationType, 
            Dictionary<string, object> parameters);
    }
}
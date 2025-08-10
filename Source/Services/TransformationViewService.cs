using CMMT.Models;
using CMMT.Models.Transformations;

namespace CMMT.Services
{
    /// <summary>
    /// Service for managing column transformations
    /// </summary>
    public class TransformationViewService : ITransformationViewService
    {
        private readonly Dictionary<TransformationType, ITransformation> _transformations;

        /// <summary>
        /// Constructor - initializes all available transformations
        /// </summary>
        public TransformationViewService()
        {
            LoggingService.LogInfo("Initializing TransformationViewService with available transformations ");
            _transformations = new Dictionary<TransformationType, ITransformation>
            {
                { TransformationType.SplitByIndexToken, new SplitTextTransformation(TransformationType.SplitByIndexToken) },

                // Date transformations
                { TransformationType.DateFormat, new DateFormatTransformation() },
                
                // Category transformations
                { TransformationType.CategoryMapping, new CategoryMappingTransformation() },

                //ValueMappingTransformation
                { TransformationType.ValueMapping, new ValueMappingTransformation() }

            };
        }

        /// <summary>
        /// Gets a transformation implementation by type
        /// </summary>
        public ITransformation GetTransformation(TransformationType type)
        {
            LoggingService.LogInfo($"Requesting transformation of type '{type}'.");
            if (_transformations.TryGetValue(type, out var transformation))
            {
                return transformation;
            }
            LoggingService.LogWarning($"Transformation of type '{type}' is not supported.");
            throw new ArgumentException($"Transformation of type {type} is not supported.");
        }

        /// <summary>
        /// Creates a transformation instance with default parameters
        /// </summary>
        public ITransformation CreateTransformation(TransformationType type, string sourceColumn)
        {
            LoggingService.LogInfo($"Creating transformation of type '{type}' for source column '{sourceColumn}'.");
            switch (type)
            {
                case TransformationType.SplitByIndexToken:
                    return new SplitTextTransformation(TransformationType.SplitByIndexToken);
                case TransformationType.DateFormat:
                    return new DateFormatTransformation();
                case TransformationType.CategoryMapping:
                    return new CategoryMappingTransformation();
                case TransformationType.ValueMapping:
                    return new ValueMappingTransformation();
                default:
                    LoggingService.LogWarning($"Transformation of type '{type}' is not supported.");
                    throw new ArgumentException($"Transformation of type {type} is not supported.");
            }
        }

        /// <summary>
        /// Creates a derived column by applying a transformation to a source column
        /// </summary>
        public DerivedColumn CreateDerivedColumn(
            CsvColumn sourceColumn,
            string newColumnName,
            TransformationType transformationType,
            Dictionary<string, object> parameters)
        {
            LoggingService.LogInfo($"Creating derived column '{newColumnName}' from source '{sourceColumn.Name}' using transformation '{transformationType}'.");

            // Validate parameters
            if (!ValidateTransformationParameters(transformationType, parameters, out string error))
            {
                LoggingService.LogWarning($"Invalid transformation parameters for '{transformationType}': {error}");
                throw new ArgumentException($"Invalid transformation parameters: {error}");
            }

            var derivedColumn = DerivedColumn.Create(
                sourceColumn,
                newColumnName,
                transformationType,
                parameters);

            var transformation = GetTransformation(transformationType);
            var transformedSamples = transformation.TransformSamples(sourceColumn.SampleValues, parameters);
            derivedColumn.SampleValues = transformedSamples;

            derivedColumn.InferredType = InferColumnType(transformedSamples);

            LoggingService.LogInfo($"Derived column '{newColumnName}' created successfully.");
            return derivedColumn;
        }

        /// <summary>
        /// Applies a transformation to sample values
        /// </summary>
        public List<string> TransformSamples(
            List<string> sampleValues,
            TransformationType transformationType,
            Dictionary<string, object> parameters)
        {
            LoggingService.LogInfo($"Applying transformation '{transformationType}' to sample values.");
            var transformation = GetTransformation(transformationType);
            return transformation.TransformSamples(sampleValues, parameters);
        }

        /// <summary>
        /// Validates transformation parameters
        /// </summary>
        public bool ValidateTransformationParameters(
            TransformationType transformationType,
            Dictionary<string, object> parameters,
            out string error)
        {
            var transformation = GetTransformation(transformationType);
            bool isValid = transformation.ValidateParameters(parameters, out error);
            LoggingService.LogInfo($"Validation of parameters for transformation '{transformationType}': {(isValid ? "Success" : "Failure")}. Error: {error}");
            return isValid;
        }

        /// <summary>
        /// Gets a description of a transformation with the given parameters
        /// </summary>
        public string GetTransformationDescription(
            TransformationType transformationType,
            Dictionary<string, object> parameters)
        {
            LoggingService.LogInfo($"Getting description for transformation '{transformationType}' with parameters: {string.Join(", ", parameters.Select(kv => $"{kv.Key}={kv.Value}"))}");
            var transformation = GetTransformation(transformationType);
            return transformation.GetDescription(parameters);
        }

        /// <summary>
        /// Infers the data type from sample values after transformation
        /// </summary>
        private string InferColumnType(List<string> sampleValues)
        {
            bool allIntegers = true;
            bool allDecimals = true;
            bool allDates = true;

            foreach (var value in sampleValues)
            {
                if (string.IsNullOrEmpty(value))
                    continue;

                if (allIntegers && !int.TryParse(value, out _))
                    allIntegers = false;

                if (allDecimals && !decimal.TryParse(value, out _))
                    allDecimals = false;

                if (allDates && !DateTime.TryParse(value, out _))
                    allDates = false;

                if (!allIntegers && !allDecimals && !allDates)
                    break;
            }

            if (allIntegers)
                return "int";
            if (allDecimals)
                return "decimal";
            if (allDates)
                return "datetime";

            return "string";
        }
    }
}
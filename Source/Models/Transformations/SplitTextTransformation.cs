using CMMT.Services;

namespace CMMT.Models.Transformations
{
    /// <summary>
    /// Transformation that splits text and extracts specific tokens
    /// </summary>
    public class SplitTextTransformation : TransformationBase
    {
        /// <summary>
        /// The type of split operation
        /// </summary>
        private readonly TransformationType _splitType;

        /// <summary>
        /// Constructor
        /// </summary>
        public SplitTextTransformation(TransformationType splitType)
        {
            if(splitType != TransformationType.SplitByIndexToken)
            {
                throw new ArgumentException("Invalid split transformation type", nameof(splitType));
            }

            _splitType = splitType;
        }

        /// <summary>
        /// Gets the type of this transformation
        /// </summary>
        public override TransformationType Type => _splitType;

        /// <summary>
        /// Transforms a single input value
        /// </summary>
        public override string Transform(string input, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }
            input = input?.Trim() ?? string.Empty;

            string delimiter = GetParameterValue(parameters, "Delimiter", " ");
            if (!input.Contains(delimiter))
            {
                return input;
            }

            var parts = input.Split(new[] { delimiter }, StringSplitOptions.None)
                             .Select(p => p.Trim())
                             .ToArray();

            if (parts.Length == 0)
            {
                return string.Empty;
            }

            switch (_splitType)
            {
                case TransformationType.SplitByIndexToken:
                    if (parameters.TryGetValue("TokenIndex", out var indexObj) &&
                        int.TryParse(indexObj.ToString(), out int parsedIndex) && parsedIndex >= 0)
                    {
                        LoggingService.LogInfo($"TokenIndex received: {indexObj}");
                        return parsedIndex < parts.Length ? parts[parsedIndex] : string.Empty;
                    }
                    else
                    {
                        // If TokenIndex is missing or invalid, return the entire input string
                        return string.Empty;
                    }

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Gets a user-friendly description of this transformation
        /// </summary>
        public override string GetDescription(Dictionary<string, object> parameters)
        {
            string delimiter = GetParameterValue(parameters, "Delimiter", " ");
            string delimiterDisplay = delimiter == " " ? "space" : $"'{delimiter}'";

            return _splitType switch
            {
                TransformationType.SplitByIndexToken =>
                    parameters.TryGetValue("TokenIndex", out var idx)
                        ? $"Extract token at position {Convert.ToInt32(idx) + 1} using {delimiterDisplay}"
                        : $"Extract token by index using {delimiterDisplay}",
                _ => $"Split using {delimiterDisplay}"
            };
        }


        /// <summary>
        /// Validates if the parameters are valid for this transformation
        /// </summary>
        public override bool ValidateParameters(Dictionary<string, object> parameters, out string error)
        {
            error = string.Empty;
            if(!parameters.TryGetValue("TokenIndex", out var indexObj) || !int.TryParse(indexObj.ToString(),out int index) || index < 0)
            {
                error = "Token position must be a positive integer";
                return false;
            }
            return true;
        }
    }
}
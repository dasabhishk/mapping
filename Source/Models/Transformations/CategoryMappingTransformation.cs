namespace CMMT.Models.Transformations
{
    /// <summary>
    /// Transformation that maps category values to standardized outputs (like gender codes)
    /// </summary>
    public class CategoryMappingTransformation : TransformationBase
    {
        /// <summary>
        /// Gets the type of this transformation
        /// </summary>
        public override TransformationType Type => TransformationType.CategoryMapping;

        /// <summary>
        /// Transforms a single input value to a standardized category
        /// </summary>
        public override string Transform(string input, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                // Use the default value for empty inputs
                return GetParameterValue(parameters, "DefaultValue", string.Empty);
            }

            // Get the mappings dictionary
            var mappings = GetParameterValue<Dictionary<string, string>>(parameters, "TargetMappings", new Dictionary<string, string>());

            // Check if case-sensitive comparison should be used
            bool caseSensitive = GetParameterValue(parameters, "CaseSensitive", false);

            // Try to find a direct match
            if (mappings.TryGetValue(input, out var directMatch))
            {
                return directMatch;
            }

            // If not case sensitive, try case-insensitive match
            if (!caseSensitive)
            {
                var keyMatch = mappings.Keys.FirstOrDefault(k =>
                    string.Equals(k, input, StringComparison.OrdinalIgnoreCase));

                if (keyMatch != null && mappings.TryGetValue(keyMatch, out var match))
                {
                    return match;
                }
            }

            // If no mapping found, use the default value
            return GetParameterValue(parameters, "DefaultValue", input);
        }

        /// <summary>
        /// Gets a user-friendly description of this transformation
        /// </summary>
        public override string GetDescription(Dictionary<string, object> parameters)
        {
            var mappings = GetParameterValue<Dictionary<string, string>>(parameters, "TargetMappings", new Dictionary<string, string>());
            bool caseSensitive = GetParameterValue(parameters, "CaseSensitive", false);

            string caseInfo = caseSensitive ? "case-sensitive" : "case-insensitive";

            if (mappings.Count == 0)
            {
                return "Map values (no mappings defined)";
            }

            // Get a sample of mappings to display
            var sampleMappings = mappings.Take(3).Select(m => $"'{m.Key}' → '{m.Value}'");
            string mappingsText = string.Join(", ", sampleMappings);

            if (mappings.Count > 3)
            {
                mappingsText += $", and {mappings.Count - 3} more";
            }

            return $"Map values using {caseInfo} matching: {mappingsText}";
        }

        /// <summary>
        /// Validates if the parameters are valid for this transformation
        /// </summary>
        public override bool ValidateParameters(Dictionary<string, object> parameters, out string error)
        {
            error = string.Empty;

            // Make sure mappings is a valid dictionary
            if (parameters.TryGetValue("TargetMappings", out var mappingsObj))
            {
                if (mappingsObj is not Dictionary<string, string>)
                {
                    error = "TargetMappings must be a dictionary of strings";
                    return false;
                }
            }

            return true;
        }
    }
}
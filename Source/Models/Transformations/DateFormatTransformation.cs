using System.Globalization;
using CMMT.Services;

namespace CMMT.Models.Transformations
{
    /// <summary>
    /// Transformation that standardizes date formats
    /// </summary>
    public class DateFormatTransformation : TransformationBase
    {
        /// <summary>
        /// Gets the type of this transformation
        /// </summary>
        public override TransformationType Type => TransformationType.DateFormat;

        /// <summary>
        /// A collection of common date format strings
        /// </summary>
        public static readonly Dictionary<string, string> CommonDateFormats = new Dictionary<string, string>
        {
            { "ISO8601", "yyyy-MM-dd" },
            { "US", "MM/dd/yyyy" },
            { "European", "dd/MM/yyyy" },
            { "FileFriendly", "yyyyMMdd" },
            { "LongDate", "MMMM d, yyyy" },
            { "ShortDateWithDay", "ddd, MMM d, yyyy" }
        };

        /// <summary>
        /// A collection of common date formats for parsing
        /// </summary>
        private static readonly string[] CommonParseFormats = new[]
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy", "dd/MM/yyyy",
            "yyyyMMdd", "MMMM d, yyyy", "MMM d, yyyy",
            "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss"
        };

        /// <summary>
        /// Transforms a single input date value
        /// </summary>
        public override string Transform(string input, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            string sourceFormat = GetParameterValue(parameters, "SourceFormat", "Auto-detect");
            string targetFormat = GetParameterValue(parameters, "TargetFormat", "yyyy-MM-dd");

            DateTime parsedDate;

            // If SourceFormat is specified and not Auto-detect, try it first
            if (!string.IsNullOrEmpty(sourceFormat) && sourceFormat != "Auto-detect")
            {
                if (DateTime.TryParseExact(input, sourceFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out parsedDate))
                {
                    return parsedDate.ToString(targetFormat);
                }
            }

            // Fall back to auto-detection logic
            // Try general parsing first
            if (DateTime.TryParse(input, out parsedDate))
            {
                return parsedDate.ToString(targetFormat);
            }

            // Try with specific common formats
            foreach (var format in CommonParseFormats)
            {
                if (DateTime.TryParseExact(input, format, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out parsedDate))
                {
                    return parsedDate.ToString(targetFormat);
                }
            }

            // If all parsing attempts fail, return the original value
            return input;
        }

        /// <summary>
        /// Gets a user-friendly description of this transformation
        /// </summary>
        public override string GetDescription(Dictionary<string, object> parameters)
        {
            string sourceFormat = GetParameterValue(parameters, "SourceFormat", "Auto-detect");
            string targetFormat = GetParameterValue(parameters, "TargetFormat", "yyyy-MM-dd");
            string targetFormatName = GetFormatNameForValue(targetFormat);

            if (sourceFormat == "Auto-detect")
            {
                return $"Auto-detect date format and convert to {targetFormatName} ({targetFormat})";
            }
            else
            {
                string sourceFormatName = GetFormatNameForValue(sourceFormat);
                return $"Convert from {sourceFormatName} ({sourceFormat}) to {targetFormatName} ({targetFormat})";
            }
        }

        /// <summary>
        /// Validates if the parameters are valid for this transformation
        /// </summary>
        public override bool ValidateParameters(Dictionary<string, object> parameters, out string error)
        {
            error = string.Empty;

            string sourceFormat = GetParameterValue(parameters, "SourceFormat", "Auto-detect");
            string targetFormat = GetParameterValue(parameters, "TargetFormat", "yyyy-MM-dd");

            // Validate target format
            try
            {
                DateTime sampleDate = new DateTime(2023, 1, 31);
                sampleDate.ToString(targetFormat);
            }
            catch (FormatException ex)
            {
                LoggingService.LogError($"Invalid target date format string: {targetFormat}",ex);
                return false;
            }

            // Validate source format if it's not Auto-detect
            if (!string.IsNullOrEmpty(sourceFormat) && sourceFormat != "Auto-detect")
            {
                try
                {
                    DateTime sampleDate = new DateTime(2023, 1, 31);
                    string formattedDate = sampleDate.ToString(sourceFormat);
                    // Try to parse it back to ensure the format is valid
                    DateTime.ParseExact(formattedDate, sourceFormat, CultureInfo.InvariantCulture);
                }
                catch (FormatException ex)
                {
                    LoggingService.LogError($"Invalid source date format string: {sourceFormat}", ex);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets a friendly name for a format string if available
        /// </summary>
        private string GetFormatNameForValue(string formatValue)
        {
            foreach (var kvp in CommonDateFormats)
            {
                if (kvp.Value == formatValue)
                {
                    return kvp.Key;
                }
            }

            return "Custom";
        }
    }
}
using System;
using System.Globalization;

namespace CMMT.Helpers
{
    /// <summary>
    /// Helper class for data type operations
    /// </summary>
    public static class DataTypeHelper
    {
        /// <summary>
        /// Checks if a string value can be parsed as an integer
        /// </summary>
        public static bool IsInteger(string value)
        {
            return !string.IsNullOrEmpty(value) && int.TryParse(value, out _);
        }

        /// <summary>
        /// Checks if a string value can be parsed as a decimal
        /// </summary>
        public static bool IsDecimal(string value)
        {
            return !string.IsNullOrEmpty(value) && (
                decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _) ||
                decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out _)
            );
        }

        /// <summary>
        /// Checks if a string value can be parsed as a datetime
        /// </summary>
        public static bool IsDateTime(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            // Try with different formats
            return DateTime.TryParse(value, out _) ||
                   DateTime.TryParseExact(value, 
                       new[] { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy", "yyyyMMdd", "yyyy-MM-ddTHH:mm:ss" },
                       CultureInfo.InvariantCulture, 
                       DateTimeStyles.None, 
                       out _);
        }

        /// <summary>
        /// Checks if a string value can be parsed as a boolean
        /// </summary>
        public static bool IsBoolean(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            // Standard boolean values
            if (bool.TryParse(value, out _))
                return true;

            // Additional boolean representations
            string lowered = value.ToLowerInvariant().Trim();
            return lowered == "yes" || lowered == "no" || 
                   lowered == "y" || lowered == "n" ||
                   lowered == "1" || lowered == "0";
        }
    }
}

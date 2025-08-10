namespace CMMT.Models.Transformations
{
    /// <summary>
    /// Defines the types of transformations available for derived columns
    /// </summary>
    public enum TransformationType
    {
        RegexExtract,       // Extract text using regex pattern
        SplitByIndexToken, // generic token for dynamic extraction

        // Date transformations
        DateFormat,           // Format a date string to a specific format
        DateExtractComponent, // Extract year, month, day, etc. from a date

        // Category transformations
        CategoryMapping,      // Map values to standardized categories (e.g., gender codes)

        // Number transformations
        NumberFormat,         // Format a number (decimal places, thousands separator, etc.)
        UnitConversion,        // Convert between units (e.g., inches to cm)
        ValueMapping,         // Map values between csv values and iscv db values
    }
}
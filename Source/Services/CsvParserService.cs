
using System.Globalization;
using System.IO;
using CMMT.Helpers;
using CMMT.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;

namespace CMMT.Services
{
    /// <summary>
    /// Service for parsing CSV files and extracting column information
    /// </summary>
    public class CsvParserService : ICsvParserService
    {
        public CsvParserService()
        {
            LoggingService.LogInfo("CsvParsingService initialized.");
        }

        /// <summary>
        /// Parses a CSV file and extracts column headers and sample data
        /// </summary>
        /// List of CSV columns with sample data and inferred types
        public async Task<List<CsvColumn>> ParseCsvFileAsync(string filePath)
        {

            LoggingService.LogInfo($"Attempting to parse CSV file: {filePath}");


            if (!File.Exists(filePath))
            {
                LoggingService.LogWarning("CSV file not found: {FilePath}");
                throw new FileNotFoundException("The specified CSV file was not found.", filePath);
            }

            List<CsvColumn> columns = new List<CsvColumn>();
            string delimiter = AppSettingsModel.GetInstance.Settings.CsvDelimitter;
            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    TrimOptions = TrimOptions.Trim,
                    BadDataFound = null, // Ignore bad data
                    MissingFieldFound = null, // Ignore missing fields
                    HeaderValidated = null, // Don't validate headers
                    PrepareHeaderForMatch = args => args.Header.Trim(),
                    Delimiter = delimiter
                };

                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, config);

                // Read the header record
                await csv.ReadAsync();
                csv.ReadHeader();

                var headers = csv.HeaderRecord;
                if (headers == null || headers.Length == 0)
                {
                    throw new InvalidOperationException("CSV file is empty or has no headers.");
                }

                // Filter out empty headers (caused by trailing commas)
                var validHeaders = headers
                    .Select((header, index) => new { Header = header?.Trim() ?? string.Empty, Index = index })
                    .Where(h => !string.IsNullOrWhiteSpace(h.Header))
                    .ToList();

                // Initialize columns with valid headers only
                for (int i = 0; i < validHeaders.Count; i++)
                {
                    columns.Add(new CsvColumn
                    {
                        Name = validHeaders[i].Header,
                        Index = validHeaders[i].Index, // Keep original index for reference
                        SampleValues = new List<string>()
                    });
                }

                LoggingService.LogInfo($"Found {validHeaders.Count} valid columns (filtered from {headers.Length} headers)");

                var appSettingsModel = AppSettingsModel.GetInstance;
                // Read up to MAX_SAMPLE_ROWS to get sample values
                int rowCount = 0;
                while (rowCount < appSettingsModel.Settings.MaxSampleRows && await csv.ReadAsync())
                {
                    var record = csv.Parser.Record;
                    if (record == null) continue;

                    // Add values only for valid columns
                    for (int i = 0; i < validHeaders.Count; i++)
                    {
                        var originalIndex = validHeaders[i].Index;
                        var value = originalIndex < record.Length ? record[originalIndex]?.Trim() ?? string.Empty : string.Empty;
                        columns[i].SampleValues.Add(value);
                    }

                    rowCount++;
                }

                // Infer data types for each column
                foreach (var column in columns)
                {
                    column.InferredType = InferColumnType(column.SampleValues);
                }

                LoggingService.LogInfo("Successfully parsed CSV with {columns.Count} columns and {rowCount} sample rows");


                return columns;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error parsing CSV file: {filePath}", ex);
                throw new Exception($"Error parsing CSV file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Infers the data type from a list of sample values
        /// </summary>
        private string InferColumnType(List<string> sampleValues)
        {
            if (sampleValues.Count == 0)
                return "string";

            // Filter out empty values for type inference
            var nonEmptyValues = sampleValues.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

            if (nonEmptyValues.Count == 0)
                return "string";

            // Try to infer types in order of specificity
            if (nonEmptyValues.All(DataTypeHelper.IsInteger))
                return "int";

            if (nonEmptyValues.All(DataTypeHelper.IsDecimal))
                return "decimal";

            if (nonEmptyValues.All(DataTypeHelper.IsDateTime))
                return "datetime";

            // Default to string if no specific type is detected
            return "string";
        }
    }
}

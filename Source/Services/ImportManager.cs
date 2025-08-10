using System.Data;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CMMT.Models;
using CMMT.Models.Transformations;
using CsvHelper;
using CsvHelper.Configuration;

namespace CMMT.Services
{
    public class ImportManager
    {
        /// <summary>
        /// Converts transformation parameters from JSON format to Dictionary for transformations
        /// </summary>
        private Dictionary<string, object> ConvertTransformationParameters(object transformationParameters)
        {
            if (transformationParameters == null)
                return new Dictionary<string, object>();

            // If it's already a Dictionary, return it
            if (transformationParameters is Dictionary<string, object> dict)
                return dict;

            // If it's a JsonElement, convert it properly
            if (transformationParameters is JsonElement jsonElement)
            {
                return ConvertJsonElementToDictionary(jsonElement);
            }

            // Handle string JSON case
            if (transformationParameters is string jsonString && !string.IsNullOrWhiteSpace(jsonString))
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(jsonString);
                    return ConvertJsonElementToDictionary(jsonDoc.RootElement);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Failed to parse JSON string: {jsonString}", ex);
                    return new Dictionary<string, object>();
                }
            }

            // Try to serialize and deserialize as fallback
            try
            {
                var json = JsonSerializer.Serialize(transformationParameters);
                var jsonDoc = JsonDocument.Parse(json);
                return ConvertJsonElementToDictionary(jsonDoc.RootElement);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to convert transformation parameters: {transformationParameters?.GetType()}", ex);
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Recursively converts JsonElement to Dictionary with proper type handling
        /// </summary>
        private Dictionary<string, object> ConvertJsonElementToDictionary(JsonElement element)
        {
            var result = new Dictionary<string, object>();

            if (element.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var property in element.EnumerateObject())
            {
                result[property.Name] = ConvertJsonElementToObject(property.Value);
            }

            return result;
        }

        /// <summary>
        /// Converts JsonElement to appropriate object type
        /// </summary>
        private object ConvertJsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Object => ConvertJsonElementToDictionary(element),
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToObject).ToArray(),
                _ => element.ToString()
            };
        }

        /// <summary>
        /// Validates and logs transformation parameters before execution
        /// </summary>
        private bool ValidateTransformationParameters(string transformationType, Dictionary<string, object> parameters, string columnName, int rowNumber)
        {
            try
            {
                // Log the parameters being passed
                LoggingService.LogInfo($"Validating transformation '{transformationType}' for column '{columnName}' at row {rowNumber}");
                LoggingService.LogInfo($"Parameters: {string.Join(", ", parameters.Select(p => $"{p.Key}={GetParameterDisplayValue(p.Value)}"))}");

                // Specific validation for CategoryMapping
                if (transformationType == "CategoryMapping")
                {
                    if (parameters.TryGetValue("TargetMappings", out var mappingsObj))
                    {
                        if (mappingsObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                        {
                            // Convert JsonElement object to Dictionary<string, string>
                            var stringDict = new Dictionary<string, string>();
                            foreach (var property in jsonElement.EnumerateObject())
                            {
                                stringDict[property.Name] = property.Value.GetString() ?? string.Empty;
                            }
                            parameters["TargetMappings"] = stringDict;
                            LoggingService.LogInfo($"Converted JsonElement TargetMappings to string dictionary: {string.Join(", ", stringDict.Select(m => $"'{m.Key}' → '{m.Value}'"))}");
                        }
                        else if (mappingsObj is Dictionary<string, object> dictObj)
                        {
                            // Convert Dictionary<string, object> to Dictionary<string, string>
                            var stringDict = new Dictionary<string, string>();
                            foreach (var kvp in dictObj)
                            {
                                stringDict[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                            }
                            parameters["TargetMappings"] = stringDict;
                            LoggingService.LogInfo($"Converted object TargetMappings to string dictionary: {string.Join(", ", stringDict.Select(m => $"'{m.Key}' → '{m.Value}'"))}");
                        }
                        else if (!(mappingsObj is Dictionary<string, string>))
                        {
                            LoggingService.LogError($"TargetMappings parameter is not a valid dictionary type: {mappingsObj?.GetType()}", null);
                            return false;
                        }
                    }
                    else
                    {
                        LoggingService.LogWarning($"CategoryMapping transformation missing TargetMappings parameter for column '{columnName}'");
                    }
                }

                // Specific validation for Split transformations (ensure Delimiter parameter is properly converted)
                if (transformationType.StartsWith("Split"))
                {
                    if (parameters.TryGetValue("Delimiter", out var delimiterObj))
                    {
                        if (delimiterObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
                        {
                            parameters["Delimiter"] = jsonElement.GetString();
                            LoggingService.LogInfo($"Converted JsonElement Delimiter to string: '{parameters["Delimiter"]}'");
                        }
                        else if (delimiterObj is not string)
                        {
                            parameters["Delimiter"] = delimiterObj?.ToString() ?? " ";
                            LoggingService.LogInfo($"Converted Delimiter to string: '{parameters["Delimiter"]}'");
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Parameter validation failed for transformation '{transformationType}' on column '{columnName}': {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets a display-friendly representation of parameter values
        /// </summary>
        private string GetParameterDisplayValue(object value)
        {
            if (value == null) return "null";
            if (value is Dictionary<string, string> dict)
                return $"[{string.Join(", ", dict.Select(kvp => $"{kvp.Key}={kvp.Value}"))}]";
            if (value is Dictionary<string, object> objDict)
                return $"[{string.Join(", ", objDict.Select(kvp => $"{kvp.Key}={kvp.Value}"))}]";
            return value.ToString();
        }

        public async Task<DataTable> ConvertCSVToDatatable(string filePath, MappingConfig mappingConfig, string _csvDelimiter, string tableName, ITransformationViewService _transformationService,
            int selectedArchiveType, IProgress<(int percent, string message)>? progress = null)
        {
            LoggingService.LogInfo("Read the csv meta data to convert it into a Datatable with records");

            progress?.Report((0, $"Processing the file {Path.GetFileName(filePath)} and applying the transformations..."));

            using var reader = new StreamReader(filePath);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true, // Tell CsvHelper to read the first row as headers
                Delimiter = "|"
            };
            using var csv = new CsvReader(reader, config);
            FileInfo fileInfo = new FileInfo(filePath);

            var mapping = mappingConfig.Mappings.FirstOrDefault(m => m.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));

            if (mapping == null)
            {
                LoggingService.LogError($"Mapping for table {tableName} not found.", null, true);
                return null;
            }

            var table = new DataTable();
            foreach (var map in mapping.ColumnMappings)
            {
                if (!table.Columns.Contains(map.DbColumn))
                    table.Columns.Add(map.DbColumn, typeof(string));
            }
            table.Columns.Add("FileName", typeof(string));
            table.Columns.Add("RowNumber", typeof(int));
            table.Columns.Add("Source", typeof(string));

            while (csv.Read())
            {

                int rowNumber = csv.Parser.Row;
                var record = csv.GetRecord<dynamic>();
                var keys = new List<string>();
                var vals = new List<string>();

                foreach (var kvp in record)
                {
                    keys.Add(kvp.Key);
                    vals.Add(kvp.Value);
                }

                var row = table.NewRow();

                foreach (var columnMap in mapping.ColumnMappings)
                {
                    string csvHeader = columnMap.CsvColumn;
                    string tableCol = columnMap.DbColumn;

                    try
                    {
                        if (columnMap.IsDerivedColumn && !string.IsNullOrWhiteSpace(columnMap.CsvColumn))
                        {
                            // For derived columns, get the value from the source column
                            var sourceColumnIndex = keys.IndexOf(columnMap.CsvColumn);
                            if (sourceColumnIndex >= 0 && sourceColumnIndex < vals.Count)
                            {
                                var sourceValue = vals[sourceColumnIndex];
                                if (!string.IsNullOrWhiteSpace(sourceValue) && !string.IsNullOrWhiteSpace(columnMap.TransformationType))
                                {
                                    try
                                    {
                                        if (Enum.TryParse(columnMap.TransformationType, out TransformationType transformationTypeResult))
                                        {
                                            var transformation = _transformationService.GetTransformation(transformationTypeResult);

                                            // Convert transformation parameters to proper Dictionary format
                                            var parameters = ConvertTransformationParameters(columnMap.TransformationParameters);

                                            // Validate parameters before transformation
                                            if (!ValidateTransformationParameters(columnMap.TransformationType, parameters, columnMap.DbColumn, rowNumber))
                                            {
                                                LoggingService.LogWarning($"Parameter validation failed for transformation '{columnMap.TransformationType}' on column '{columnMap.DbColumn}' at row {rowNumber}. Using source value.");
                                                row[columnMap.DbColumn] = sourceValue;
                                                continue;
                                            }

                                            LoggingService.LogInfo($"Applying {transformationTypeResult} transformation to column '{columnMap.DbColumn}' with source value: '{sourceValue}'");
                                            LoggingService.LogInfo($"Parameters: {string.Join(", ", parameters.Select(p => $"{p.Key}={GetParameterDisplayValue(p.Value)}"))}");

                                            var result = transformation.Transform(sourceValue, parameters);
                                            row[columnMap.DbColumn] = result;

                                            LoggingService.LogInfo($"Transformation SUCCESS: '{sourceValue}' -> '{result}' in column '{columnMap.DbColumn}'");
                                        }
                                        else
                                        {
                                            LoggingService.LogWarning($"Invalid transformation type '{columnMap.TransformationType}' for column '{columnMap.DbColumn}' at row {rowNumber}.");
                                            row[columnMap.DbColumn] = sourceValue; // Fallback to source value
                                        }
                                    }
                                    catch (Exception transformEx)
                                    {
                                        LoggingService.LogError($"Transformation FAILED for column '{columnMap.DbColumn}' at row {rowNumber}: {transformEx.Message}", transformEx);
                                        LoggingService.LogError($"Source value was: '{sourceValue}', Parameters: {string.Join(", ", ConvertTransformationParameters(columnMap.TransformationParameters).Select(p => $"{p.Key}={GetParameterDisplayValue(p.Value)}"))}", transformEx);
                                        row[columnMap.DbColumn] = sourceValue; // Fallback to source value
                                    }
                                }
                                else
                                {
                                    row[columnMap.DbColumn] = sourceValue;
                                }
                            }
                            else
                            {
                                LoggingService.LogWarning($"Source column '{columnMap.SourceColumnName}' not found for derived column '{columnMap.DbColumn}' at row {rowNumber}.", true);
                                row[columnMap.DbColumn] = DBNull.Value;
                            }
                        }
                        else
                        {
                            // For regular columns, get the value from the mapped CSV column
                            var valIndex = keys.IndexOf(columnMap.CsvColumn);
                            if (valIndex >= 0 && valIndex < vals.Count)
                            {
                                var value = vals[valIndex];
                                if (value != null)
                                {
                                    row[columnMap.DbColumn] = value;
                                }
                                else
                                {
                                    row[columnMap.DbColumn] = DBNull.Value;
                                }
                            } else
                            {
                                row[columnMap.DbColumn] = DBNull.Value;
                            }
                        }
                    }
                    catch (IndexOutOfRangeException ex)
                    {
                        LoggingService.LogError($"Column '{columnMap.CsvColumn}' not found in CSV at row {rowNumber}", ex);
                        row[columnMap.DbColumn] = DBNull.Value;
                        continue;
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError($"Unexpected error processing column '{columnMap.DbColumn}' at row {rowNumber}: {ex.Message}", ex, false);
                        row[columnMap.DbColumn] = DBNull.Value;
                        throw new Exception(ex.StackTrace);
                    }
                }
                row["FileName"] = fileInfo.Name;
                row["RowNumber"] = rowNumber;
                row["Source"] = selectedArchiveType;

                table.Rows.Add(row);
            }
            return await Task.FromResult(table); ;
        }
    }
}
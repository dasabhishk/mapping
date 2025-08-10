using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CMMT.Helpers;
using CMMT.Models;
using CMMT.Models.Transformations;

namespace CMMT.Services
{
    public class MappingService : IMappingService
    {
        private readonly ITransformationViewService _transformationService;
        private readonly AppSettingsModel appSettingsModel = AppSettingsModel.GetInstance;

        public MappingService(ITransformationViewService transformationService)
        {
            _transformationService = transformationService;
        }

        public Dictionary<string, string> AutoMatchColumns(ObservableCollection<CsvColumn> csvColumns, List<DatabaseColumn> dbColumns)
        {
            var result = new Dictionary<string, string>();
            LoggingService.LogInfo("Starting auto-matching of CSV columns to database columns");

            foreach (var dbColumn in dbColumns)
            {
                var exactMatch = csvColumns.FirstOrDefault(c =>
                    string.Equals(c.Name, dbColumn.Name, StringComparison.OrdinalIgnoreCase));

                if (exactMatch != null)
                {
                    LoggingService.LogInfo($"Exact match found: DB column '{dbColumn.Name}' -> CSV column '{exactMatch.Name}'");
                    result[dbColumn.Name] = exactMatch.Name;
                    continue;
                }

                string normalizedDbName = NormalizeColumnName(dbColumn.Name);
                var normalizedMatch = csvColumns.FirstOrDefault(c =>
                    string.Equals(NormalizeColumnName(c.Name), normalizedDbName, StringComparison.OrdinalIgnoreCase));

                if (normalizedMatch != null)
                {
                    result[dbColumn.Name] = normalizedMatch.Name;
                    continue;
                }

                var containsMatch = csvColumns.FirstOrDefault(c =>
                    c.Name.Contains(dbColumn.Name, StringComparison.OrdinalIgnoreCase) ||
                    dbColumn.Name.Contains(c.Name, StringComparison.OrdinalIgnoreCase));

                if (containsMatch != null)
                {
                    LoggingService.LogInfo($"Contains match found: DB column '{dbColumn.Name}' -> CSV column '{containsMatch.Name}'");
                    result[dbColumn.Name] = containsMatch.Name;
                }

                if (!result.ContainsKey(dbColumn.Name))
                {
                    LoggingService.LogWarning($"No match found for DB column '{dbColumn.Name}'");
                }
            }

            LoggingService.LogInfo("Auto-matching complete.");
            return result;
        }

        public Dictionary<string, string> ValidateMappings(
            List<ViewModels.ColumnMappingViewModel> mappingViewModels,
            ObservableCollection<CsvColumn> csvColumns,
            List<DatabaseColumn> dbColumns)
        {
            LoggingService.LogInfo("Starting validation of column mappings.");
            var errors = new Dictionary<string, string>();
            var warnings = new Dictionary<string, string>();

            foreach (var vm in mappingViewModels)
            {
                vm.ValidationWarning = string.Empty;
            }

            var csvColumnUsage = mappingViewModels
                .Where(vm => !string.IsNullOrEmpty(vm.SelectedCsvColumn) && vm.SelectedCsvColumn != appSettingsModel.Settings.NoMappingOptional)
                .GroupBy(vm => vm.SelectedCsvColumn)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var duplicateGroup in csvColumnUsage)
            {
                string csvColumnName = duplicateGroup.Key;
                var mappedDbColumns = duplicateGroup.Select(vm => vm.DbColumn.Name).ToList();

                if (csvColumnName.ToLower().Contains("name") || csvColumnName.ToLower().Contains("date")
                    || csvColumnName.ToLower().Contains("dob") || csvColumnName.ToLower().Contains("birth"))
                {
                    foreach (var vm in duplicateGroup)
                    {
                        LoggingService.LogWarning($"CSV column '{csvColumnName}' is mapped to multiple fields ({string.Join(", ", mappedDbColumns)}). Consider using transformations to split this field appropriately.");
                        warnings[vm.DbColumn.Name] = $"Potential duplicate mapping for {csvColumnName}";
                    }
                }
                else
                {
                    foreach (var vm in duplicateGroup)
                    {
                        LoggingService.LogWarning($"CSV column '{csvColumnName}' cannot be mapped to multiple database columns ({string.Join(", ", mappedDbColumns)})");
                        errors[vm.DbColumn.Name] = $"Duplicate mapping error for {csvColumnName}";
                    }
                }
            }

            foreach (var dbColumn in dbColumns)
            {
                var mappingVm = mappingViewModels.FirstOrDefault(vm => vm.DbColumn.Name == dbColumn.Name);
                if (mappingVm == null) continue;

                if (dbColumn.IsMappingRequired && (string.IsNullOrEmpty(mappingVm.SelectedCsvColumn) || mappingVm.SelectedCsvColumn == appSettingsModel.Settings.NoMappingOptional))
                {
                    LoggingService.LogWarning($"{dbColumn.Name} is required for mapping but not mapped.");
                    errors[dbColumn.Name] = "Required column is not mapped";
                    continue;
                }

                if (string.IsNullOrEmpty(mappingVm.SelectedCsvColumn) || mappingVm.SelectedCsvColumn == appSettingsModel.Settings.NoMappingOptional)
                    continue;

                var csvColumn = csvColumns.FirstOrDefault(c => c.Name == mappingVm.SelectedCsvColumn);
                if (csvColumn == null)
                {
                    LoggingService.LogWarning($"Mapped CSV column '{mappingVm.SelectedCsvColumn}' not found.");
                    errors[dbColumn.Name] = $"Mapped column not found: {mappingVm.SelectedCsvColumn}";
                    continue;
                }

                List<string> samplesToValidate = mappingVm.HasTransformation ? mappingVm.SampleValues.ToList() : csvColumn.SampleValues;

                string effectiveSourceType = mappingVm.HasTransformation
                    ? InferTypeFromSamples(samplesToValidate)
                    : csvColumn.InferredType;

                if (mappingVm.HasTransformation)
                {
                    bool hasNonEmptyValues = samplesToValidate.Any(s => !string.IsNullOrWhiteSpace(s));
                    if (!hasNonEmptyValues && ShouldWarnForEmptyTransformation(dbColumn.Name, dbColumn.IsMappingRequired))
                    {
                        LoggingService.LogWarning($"Required column '{dbColumn.Name}' has no valid values after transformation.");
                        warnings[dbColumn.Name] = "Values are empty after transformation, check for token field";
                        continue;
                    }
                }
                else
                {
                    if (!IsTypeCompatible(effectiveSourceType, dbColumn.DataType))
                    {
                        LoggingService.LogWarning($"Data type mismatch for column '{dbColumn.Name}': CSV value is '{effectiveSourceType}', DB is '{dbColumn.DataType}'.");
                        errors[dbColumn.Name] = $"Type mismatch: CSV={effectiveSourceType}, DB={dbColumn.DataType}";
                        continue;
                    }
                }
                if (mappingVm.HasTransformation && mappingVm.TransformationType.HasValue)
                {
                    // Create the transformation based on the selected type and CSV column.
                    var transformation = _transformationService.CreateTransformation(mappingVm.TransformationType.Value, mappingVm.SelectedCsvColumn);

                    // Check if all transformed samples remain unchanged.
                    bool allUnchanged = samplesToValidate.All(s => transformation.Transform(s, mappingVm.TransformationParameters) == s);

                    if (allUnchanged)
                    {
                        LoggingService.LogWarning($"Delimiter or token index didn't match for CSV column '{mappingVm.SelectedCsvColumn}'.");
                        warnings[dbColumn.Name] = "Data transformation failed. Delimiter didn't match";
                        continue;
                    }
                }

                if (dbColumn.DataType == "string")
                {
                    int maxLength = dbColumn.MaxLength ?? 4000;
                    bool hasLongValue = samplesToValidate.Any(v => !string.IsNullOrEmpty(v) && v.Length > maxLength);

                    if (hasLongValue)
                    {
                        LoggingService.LogWarning($"Values in column '{dbColumn.Name}' exceed maximum length {maxLength}");
                        errors[dbColumn.Name] = $"Max length exceeded ({maxLength})";
                        continue;
                    }

                    if (mappingVm.HasTransformation && dbColumn.Name.Contains("Physician"))
                    {
                        if (!ValidatePhysicianNameFormat(samplesToValidate))
                        {
                            LoggingService.LogWarning($"Invalid physician name format in column '{dbColumn.Name}'");
                            errors[dbColumn.Name] = "Invalid physician name format";
                            continue;
                        }
                    }
                }
            }

            foreach (var warning in warnings)
            {
                var mappingVm = mappingViewModels.FirstOrDefault(vm => vm.DbColumn.Name == warning.Key);
                if (mappingVm != null)
                {
                    mappingVm.ValidationWarning = warning.Value;
                }
            }

            LoggingService.LogInfo("Validation completed.");
            return errors;
        }

        public async Task<MultiMappingResult> LoadMappingsAsync(string filePath)
        {
            LoggingService.LogInfo($"Loading mappings from {filePath}");
            if (!File.Exists(filePath))
            {
                LoggingService.LogWarning($"Mappings file not found: {filePath}");
                return new MultiMappingResult();
            }

            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<MultiMappingResult>(json, options) ?? new MultiMappingResult();
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error loading mappings", ex);
                return new MultiMappingResult();
            }
        }

        public async Task<bool> SaveMultiMappingsAsync(MultiMappingResult mappings, string filePath)
        {
            LoggingService.LogInfo($"Saving mappings to {filePath}");
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(mappings, options);
                await File.WriteAllTextAsync(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Error saving mappings", ex);
                return false;
            }
        }

        public DerivedColumn CreateDerivedColumn(CsvColumn sourceColumn, string newColumnName, TransformationType transformationType, Dictionary<string, object> parameters)
        {
            LoggingService.LogInfo($"Creating derived column {newColumnName} with transformation {transformationType}");
            return _transformationService.CreateDerivedColumn(sourceColumn, newColumnName, transformationType, parameters);
        }

        public DerivedColumn? RecreateTransformedColumn(CsvColumn sourceColumn, string derivedColumnName, string transformationType, string transformationParametersJson)
        {
            try
            {
                if (!Enum.TryParse<TransformationType>(transformationType, out var type))
                    return null;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(transformationParametersJson, options) ?? new Dictionary<string, object>();

                return _transformationService.CreateDerivedColumn(sourceColumn, derivedColumnName, type, parameters);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error recreating derived column '{derivedColumnName}'", ex);
                return null;
            }
        }

        private string NormalizeColumnName(string name) => name.Replace(" ", "").Replace("_", "").Replace("-", "");

        private string InferTypeFromSamples(IEnumerable<string> samples)
        {
            if (samples == null || !samples.Any()) return "string";
            if (samples.All(string.IsNullOrWhiteSpace)) return "string";

            if (samples.All(s => int.TryParse(s, out _))) return "int";
            if (samples.All(s => decimal.TryParse(s, out _))) return "decimal";
            if (samples.All(s => DateTime.TryParse(s, out _))) return "date";

            return "string";
        }

        private bool ValidatePhysicianNameFormat(IEnumerable<string> physicianNames)
        {
            foreach (var name in physicianNames.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                if (!name.Contains(",") || name.Split(',', 2).Any(p => string.IsNullOrWhiteSpace(p)))
                    return false;
            }
            return true;
        }

        private string NormalizeSqlServerType(string sqlType)
        {
            if (string.IsNullOrEmpty(sqlType)) return "string";
            string t = sqlType.ToLower();

            if (t.Contains("varchar") || t.Contains("char") || t.Contains("text")) return "string";
            if (t.Contains("int") || t.Contains("smallint") || t.Contains("bigint") || t.Contains("tinyint")) return "int";
            if (t.Contains("decimal") || t.Contains("numeric") || t.Contains("float") || t.Contains("real")) return "decimal";
            if (t.Contains("date") || t.Contains("time")) return "datetime";
            if (t.Contains("bit")) return "bool";
            return "string";
        }

        private bool IsTypeCompatible(string csvType, string dbType)
        {
            string normalizedDbType = NormalizeSqlServerType(dbType);
            if (csvType == "string" || normalizedDbType == "string") return true;
            if (csvType == normalizedDbType) return true;

            return normalizedDbType switch
            {
                "int" => csvType == "int" || csvType == "decimal",
                "decimal" => csvType == "int" || csvType == "decimal" || csvType == "double" || csvType == "float",
                "datetime" => csvType == "datetime" || csvType == "string",
                "bool" => csvType == "bool" || csvType == "int",
                _ => false
            };
        }

        private bool ShouldWarnForEmptyTransformation(string columnName, bool isMappingRequired)
        {
            // Warn if mapping is required
            if (isMappingRequired)
            {
                return true;
            }

            // Otherwise, issue a warning if the column name suggests vital info (e.g., name, title, honorific)
            string lowerName = columnName.ToLowerInvariant();
            return lowerName.Contains("name") || lowerName.Contains("title") || lowerName.Contains("honorific");
        }
    }
}

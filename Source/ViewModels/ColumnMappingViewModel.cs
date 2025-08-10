using System.Collections.ObjectModel;
using CMMT.Helpers;
using CMMT.Models;
using CMMT.Models.Transformations;
using CMMT.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CMMT.ViewModels
{
    /// <summary>
    /// Validation status for column mappings
    /// </summary>
    public enum ValidationStatus
    {
        Valid,
        Warning,
        Error,
        ValidationUnmapped
    }

    /// <summary>
    /// View model for individual column mappings
    /// </summary>
    public partial class ColumnMappingViewModel : ObservableObject
    {
        private readonly AppSettingsModel appSettingsModel = AppSettingsModel.GetInstance;

        /// <summary>
        /// Database column definition
        /// </summary>
        [ObservableProperty]
        private DatabaseColumn _dbColumn = new();

        /// <summary>
        /// Selected CSV column name
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(OpenTransformationDialogCommand))]
        private string _selectedCsvColumn = string.Empty;

        /// <summary>
        /// Available CSV columns to choose from
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _availableCsvColumns = new();

        /// <summary>
        /// Sample values from the selected CSV column
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _sampleValues = new();

        /// <summary>
        /// Inferred type from the selected CSV column
        /// </summary>
        [ObservableProperty]
        private string _inferredType = string.Empty;

        /// <summary>
        /// Validation error message, if any
        /// </summary>
        [ObservableProperty]
        private string _validationError = string.Empty;

        /// <summary>
        /// Indicates if the mapping is valid
        /// </summary>
        [ObservableProperty]
        private bool _isValid = true;

        /// <summary>
        /// Indicates if this column has a transformation applied
        /// </summary>
        [ObservableProperty]
        private bool _hasTransformation = false;

        /// <summary>
        /// Type of transformation applied, if any
        /// </summary>
        [ObservableProperty]
        private TransformationType? _transformationType;

        /// <summary>
        /// Parameters for the transformation, if any
        /// </summary>
        [ObservableProperty]
        private Dictionary<string, object> _transformationParameters = new();

        /// <summary>
        /// Display name for the transformation
        /// </summary>
        [ObservableProperty]
        private string _transformationDisplayName = string.Empty;

        /// <summary>
        /// Available transformation types for this mapping
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<TransformationType> _availableTransformations = new();

        /// <summary>
        /// Indicates whether transformation should be allowed for this column
        /// </summary>
        [ObservableProperty]
        private bool _canBeTransformed;

        /// <summary>
        /// Original (pre-transformation) sample values
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _originalSampleValues = new();

        /// <summary>
        /// Validation warning message for this mapping
        /// </summary>
        [ObservableProperty]
        private string _validationWarning = string.Empty;

        /// <summary>
        /// Overall validation status computed from error, warning, unmapped, and valid states
        /// </summary>
        public ValidationStatus ValidationStatus
        {
            get
            {
                if (!string.IsNullOrEmpty(ValidationError))
                    return ValidationStatus.Error;

                if (!string.IsNullOrEmpty(ValidationWarning))
                    return ValidationStatus.Warning;

                if (IsValid)
                    return ValidationStatus.Valid;
                // Check for unmapped state BEFORE checking IsValid
                if (string.IsNullOrEmpty(SelectedCsvColumn) ||
                    SelectedCsvColumn == appSettingsModel.Settings.NoMappingOptional)
                    return ValidationStatus.ValidationUnmapped;

                return ValidationStatus.Error; // Default to error if not explicitly valid
            }
        }

        /// <summary>
        /// Event that requests opening the transformation dialog
        /// This is handled by the view to show the actual dialog
        /// </summary>
        public event EventHandler? OpenTransformationDialogRequested;

        /// <summary>
        /// Called when ValidationError changes to notify ValidationStatus
        /// </summary>
        partial void OnValidationErrorChanged(string value)
        {
            OnPropertyChanged(nameof(ValidationStatus));
        }

        /// <summary>
        /// Called when ValidationWarning changes to notify ValidationStatus
        /// </summary>
        partial void OnValidationWarningChanged(string value)
        {
            OnPropertyChanged(nameof(ValidationStatus));
        }

        /// <summary>
        /// Called when IsValid changes to notify ValidationStatus
        /// </summary>
        partial void OnIsValidChanged(bool value)
        {
            OnPropertyChanged(nameof(ValidationStatus));
        }

        /// <summary>
        /// Called when SelectedCsvColumn changes to update validation
        /// </summary>
        partial void OnSelectedCsvColumnChanged(string value)
        {
            OnPropertyChanged(nameof(ValidationStatus));
        }


        /// <summary>
        /// Command to open the transformation dialog
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanOpenTransformationDialog))]
        private void OpenTransformationDialog()
        {
            // Raise event for the view to handle
            OpenTransformationDialogRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Determines if the transformation dialog can be opened
        /// </summary>
        /// <returns>True if the column can be transformed based on schema definition</returns>
        private bool CanOpenTransformationDialog()
        {
            // Only allow transformations for columns that are marked as transformable in the schema
            return CanBeTransformed && !string.IsNullOrEmpty(SelectedCsvColumn);
        }

        /// <summary>
        /// Command to clear the current transformation
        /// </summary>
        [RelayCommand]
        private void ClearTransformation()
        {
            HasTransformation = false;
            TransformationType = null;
            TransformationParameters.Clear();
            TransformationDisplayName = string.Empty;

            // Restore original sample values
            SampleValues = new ObservableCollection<string>(OriginalSampleValues);
        }

        /// <summary>
        /// Applies a transformation to this column mapping
        /// </summary>
        /// <param name="transformation">The transformation to apply</param>
        public void ApplyTransformation(ITransformation transformation)
        {
            ApplyTransformation(transformation, new Dictionary<string, object>());
        }

        /// <summary>
        /// Applies a transformation to this column mapping with parameters
        /// </summary>
        /// <param name="transformation">The transformation to apply</param>
        /// <param name="parameters">Parameters for the transformation</param>
        public void ApplyTransformation(ITransformation transformation, Dictionary<string, object> parameters)
        {
            if (transformation == null)
                return;

            HasTransformation = true;
            TransformationType = transformation.Type;
            TransformationDisplayName = GetTransformationDisplayName(transformation);

            // Store transformation parameters from UI
            TransformationParameters.Clear();
            foreach (var param in parameters)
            {
                TransformationParameters[param.Key] = param.Value;
            }

            // Apply transformation to sample values with parameters
            var transformedSamples = new List<string>();
            foreach (var sample in OriginalSampleValues)
            {
                try
                {
                    var transformed = transformation.Transform(sample, parameters);
                    transformedSamples.Add(transformed);
                }
                catch
                {
                    transformedSamples.Add($"[Error transforming: {sample}]");
                }
            }

            SampleValues = new ObservableCollection<string>(transformedSamples);
        }

        public void ApplyValueMappingTransformation(Dictionary<string, string> mappingDict, ObservableCollection<CsvColumn> csvColumns, string defaultTargetValue)
        {
            //defensive null checks
            ArgumentNullException.ThrowIfNull(mappingDict);
            ArgumentNullException.ThrowIfNull(csvColumns);
            ArgumentNullException.ThrowIfNull(defaultTargetValue);

            var csvCol = csvColumns.FirstOrDefault(c => c.Name == SelectedCsvColumn);
            //if csv column not found
            if (csvCol == null)
            {
                LoggingService.LogWarning("Column not found");
                return;
            }
            try
            {
                //Initiate transformation
                var transformed = csvCol.SampleValues.Select(val => mappingDict.TryGetValue(val, out var mappedVal) && !string.IsNullOrWhiteSpace(mappedVal) ? mappedVal : defaultTargetValue);
                // Update all properties with proper change notifications
                SampleValues = new ObservableCollection<string>(transformed);
                HasTransformation = true;
                TransformationType = Models.Transformations.TransformationType.ValueMapping;
                //TransformationParameters = mappingDict.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
                TransformationDisplayName = "Value Mapping";

                if (DbColumn?.Name.Equals("InstitutionName", StringComparison.OrdinalIgnoreCase) == true)
                {
                    TransformationParameters = new Dictionary<string, object>
                           {
                               { "InstitutionMapping", mappingDict },
                               { "DefaultTargetValue", defaultTargetValue }
                           };
                }
                else if (DbColumn?.Name.Equals("StudyType", StringComparison.OrdinalIgnoreCase) == true)
                {
                    TransformationParameters = new Dictionary<string, object>
                           {
                               { "StudyTypeMapping", mappingDict },
                               { "DefaultTargetValue", defaultTargetValue }
                           };
                }
                else
                {
                    TransformationParameters = mappingDict.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
                }

            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error applying transformation for column '{SelectedCsvColumn}'", ex);
            }
        }

        /// <summary>
        /// Gets display name for a transformation
        /// </summary>
        private string GetTransformationDisplayName(ITransformation transformation)
        {
            return transformation.Type switch
            {
                Models.Transformations.TransformationType.SplitByIndexToken => "Select Token Index",
                Models.Transformations.TransformationType.DateFormat => "Format Date",
                _ => transformation.Type.ToString()
            };
        }

        /// <summary>
        /// Updates available transformations based on the mapping context
        /// </summary>
        public void UpdateAvailableTransformations()
        {
            AvailableTransformations.Clear();

            if (string.IsNullOrEmpty(SelectedCsvColumn) || SelectedCsvColumn == appSettingsModel.Settings.NoMappingOptional)
                return;

            if (!CanBeTransformed)
                return;

            // Use unified logic for determining available transformations
            var columnName = DbColumn.Name.ToLower();

            //InstitutionName value mapping is a special case
            if (DbColumn.Name.Equals("InstitutionName", StringComparison.OrdinalIgnoreCase))
            {
                AvailableTransformations.Add(Models.Transformations.TransformationType.ValueMapping);
                return;
            }
            // StudyType is a special case for value mapping
            if (DbColumn.Name.Equals("StudyType", StringComparison.OrdinalIgnoreCase))
            {
                AvailableTransformations.Add(Models.Transformations.TransformationType.ValueMapping);
                return;
            }

            // Name fields can be split
            if (columnName.Contains("name") ||
                columnName.Contains("title") ||
                columnName.Contains("honorific"))
            {
                AvailableTransformations.Add(Models.Transformations.TransformationType.SplitByIndexToken);
            }

            // Date fields can be formatted
            if (columnName.Contains("date") || columnName.Contains("time") || columnName.Contains("year") ||
                DbColumn.DataType?.ToLower() == "datetime" || DbColumn.DataType?.ToLower() == "date")
            {
                AvailableTransformations.Add(Models.Transformations.TransformationType.DateFormat);
            }

            // Gender and other categorical fields can be mapped
            if (columnName.Contains("gender") || columnName.Contains("status") || columnName.Contains("type") ||
                columnName.Contains("code") || columnName.Contains("category"))
            {
                AvailableTransformations.Add(Models.Transformations.TransformationType.CategoryMapping);
            }
        }
    }
}
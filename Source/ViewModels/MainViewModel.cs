using CMMT.Helpers;
using CMMT.Models;
using CMMT.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CMMT.DataStructures;
using System.Reflection;

namespace CMMT.ViewModels
{
    /// <summary>
    /// Main view model for the application
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly ICsvParserService _csvParserService;
        private readonly ISchemaLoaderService _schemaLoaderService;
        private readonly IMappingService _mappingService;
        private readonly AppSettingsModel appSettingsModel = AppSettingsModel.GetInstance;

        // Observable properties for binding
        // In MainViewModel.cs
        [ObservableProperty]
        private object _currentPageContent; 

        [ObservableProperty]
        private string _statusMessage = "Ready to map CSV to database schema.";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isSchemaLoaded;

        [ObservableProperty]
        private ObservableCollection<string> _availableCsvTypes = new();

        [ObservableProperty]
        private string _selectedCsvType = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ColumnMappingViewModel> _columnMappings = new();

        [ObservableProperty]
        private bool _canSaveMapping;

        [ObservableProperty]
        private ObservableCollection<CsvMappingTypeViewModel> _mappingTypes = new();

        [ObservableProperty]
        private CsvMappingTypeViewModel? _currentMappingType;

        [ObservableProperty]
        private bool _canAddNewMapping = true;

        [ObservableProperty]
        private MultiMappingResult _savedMappings = new();

        // Private backing fields
        private DatabaseSchema _databaseSchema = new();

        // New property for global save button binding
        public bool CanSaveAllMappings => MappingTypes != null && MappingTypes.Any(m => m.IsValid);


        /// <summary>
        /// Constructor with injected dependencies
        /// </summary>
        public MainViewModel(
            ICsvParserService csvParserService,
            ISchemaLoaderService schemaLoaderService,
            IMappingService mappingService,
            IServiceProvider serviceProvider) // Inject IServiceProvider
        {
            LoadAndValidateAppSettings();
            _csvParserService = csvParserService;
            _schemaLoaderService = schemaLoaderService;
            _mappingService = mappingService;

            var migrationViewModel = serviceProvider.GetRequiredService<MigrationViewModel>();
            var migrationPage = new CMMT.UI.Migration(migrationViewModel); // Pass the viewModel directly

            CurrentPageContent = migrationPage; // Set the page as the content
        }

        /// <summary>
        /// Command to initialize the application
        /// </summary>
        [RelayCommand]
        private async Task Initialize()
        {
            StatusMessage = "Initializing application...";

            // Automatically load schema from Configuration/dbconfig.json
            await LoadSchemaAutomatically();

            // Try to load existing mappings
            try
            {
                var existingMappings = await _mappingService.LoadMappingsAsync("mappings.json");
                if (existingMappings != null && existingMappings.Mappings.Count > 0)
                {
                    SavedMappings = existingMappings;
                    if (IsSchemaLoaded)
                    {
                        StatusMessage = $"Schema loaded. Found {existingMappings.Mappings.Count} existing mappings.";
                    }
                }
            }
            catch (Exception ex)
            {
                if (IsSchemaLoaded)
                {
                    StatusMessage = "Schema loaded. Starting fresh mapping session.";
                }
            }
        }

        /// <summary>
        /// Command to browse and select a CSV file for the current mapping type
        /// </summary>
        [RelayCommand]
        private async Task BrowseCsvFile()
        {
            if (CurrentMappingType == null)
            {
                LoggingService.LogWarning("Please select a CSV type first.", showMsgBox: true);
                return;

            }

            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                Title = $"Select a CSV file for {CurrentMappingType.CsvType}"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                CurrentMappingType.CsvFilePath = openFileDialog.FileName;
                await LoadCsvFile(CurrentMappingType);
            }
        }

        /// <summary>
        /// Load a CSV file for a specific mapping type
        /// </summary>
        private async Task LoadCsvFile(CsvMappingTypeViewModel mappingType)
        {
            if (string.IsNullOrEmpty(mappingType.CsvFilePath) || !File.Exists(mappingType.CsvFilePath))
            {
                StatusMessage = "Please select a valid CSV file.";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Loading CSV file for {mappingType.CsvType}...";

                var csvColumnsList = await _csvParserService.ParseCsvFileAsync(mappingType.CsvFilePath);
                mappingType.CsvColumns.Clear();
                foreach (var column in csvColumnsList)
                {
                    mappingType.CsvColumns.Add(column);
                }
                mappingType.IsCsvLoaded = mappingType.CsvColumns.Count > 0;

                if (mappingType.IsCsvLoaded)
                {
                    StatusMessage = $"CSV loaded for {mappingType.CsvType}: {mappingType.CsvColumns.Count} columns found.";

                    // Update the mappings
                    UpdateColumnMappings(mappingType);
                }
                else
                {
                    StatusMessage = $"Failed to load columns from CSV file for {mappingType.CsvType}.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading CSV: {ex.Message}";
                mappingType.IsCsvLoaded = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Updates the current mapping type when the selected CSV type changes
        /// </summary>
        partial void OnSelectedCsvTypeChanged(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsSchemaLoaded)
                return;

            // Find the associated table schema
            var tableSchema = _databaseSchema.Tables.FirstOrDefault(t => t.CsvType == value);
            if (tableSchema == null)
                return;

            // Check if we already have a mapping for this type
            var existingMapping = MappingTypes.FirstOrDefault(m => m.CsvType == value);
            if (existingMapping != null)
            {
                CurrentMappingType = existingMapping;
                ColumnMappings = existingMapping.ColumnMappings;
                return;
            }

            // Create a new mapping type
            var newMappingType = new CsvMappingTypeViewModel
            {
                CsvType = value,
                TableName = tableSchema.TableName,
                TableSchema = tableSchema
            };

            MappingTypes.Add(newMappingType);
            CurrentMappingType = newMappingType;
            ColumnMappings = newMappingType.ColumnMappings;

            // Check if we have a saved mapping for this type
            var savedMapping = SavedMappings.Mappings.FirstOrDefault(m => m.CsvType == value);
            if (savedMapping != null)
            {
                StatusMessage = $"Found existing mapping for {value}. Loading...";
                LoadSavedMapping(savedMapping, newMappingType);
            }
        }

        /// <summary>
        /// Loads a saved mapping into a mapping type view model
        /// </summary>
        private void LoadSavedMapping(MappingResult savedMapping, CsvMappingTypeViewModel mappingType)
        {
            // We'll populate the mappings once the CSV file is loaded
            mappingType.IsCompleted = true;
        }

        /// <summary>
        /// Updates the column mappings based on the selected table and CSV columns
        /// </summary>
        private void UpdateColumnMappings(CsvMappingTypeViewModel mappingType)
        {
            if (mappingType.TableSchema == null || mappingType.CsvColumns.Count == 0)
                return;

            mappingType.ColumnMappings.Clear();

            // Create view models for each database column (no auto-matching)
            foreach (var dbColumn in mappingType.TableSchema.Columns)
            {
                var viewModel = new ColumnMappingViewModel
                {
                    DbColumn = dbColumn,
                    AvailableCsvColumns = CreateCsvColumnOptions(mappingType.CsvColumns, dbColumn.IsMappingRequired),
                    // Only allow transformations for specific column types and purposes
                    CanBeTransformed = CanColumnBeTransformed(dbColumn),
                    // Start with no column selected - user must manually select
                    SelectedCsvColumn = string.Empty,
                    IsValid = false
                };

                // Add sample values if a CSV column is selected
                UpdateColumnMappingSampleValues(viewModel, mappingType);

                // Subscribe to property changed event to update sample values when selection changes
                viewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ColumnMappingViewModel.SelectedCsvColumn))
                    {
                        UpdateColumnMappingSampleValues(viewModel, mappingType);
                        viewModel.UpdateAvailableTransformations(); // Update available transformations when CSV column is selected
                        ValidateMappings(mappingType);
                        RefreshSaveButtonState();
                    }
                };

                mappingType.ColumnMappings.Add(viewModel);
            }

            // If this is the current mapping type, update the UI
            if (mappingType == CurrentMappingType)
            {
                ColumnMappings = mappingType.ColumnMappings;
            }

            ValidateMappings(mappingType);

            // Check for saved mappings
            var savedMapping = SavedMappings.Mappings.FirstOrDefault(m => m.CsvType == mappingType.CsvType);
            if (savedMapping != null && mappingType.IsCompleted)
            {
                ApplySavedMapping(savedMapping, mappingType);
            }
        }

        /// <summary>
        /// Applies a saved mapping to the mapping type
        /// </summary>
        private void ApplySavedMapping(MappingResult savedMapping, CsvMappingTypeViewModel mappingType)
        {
            foreach (var mapping in savedMapping.ColumnMappings)
            {
                var viewModel = mappingType.ColumnMappings.FirstOrDefault(
                    vm => vm.DbColumn.Name == mapping.DbColumn);

                if (viewModel != null && mappingType.CsvColumns.Any(c => c.Name == mapping.CsvColumn))
                {
                    viewModel.SelectedCsvColumn = mapping.CsvColumn;
                }
            }
            ValidateMappings(mappingType);
        }

        /// <summary>
        /// Determines if a column should allow transformations based on schema definition
        /// </summary>
        private bool CanColumnBeTransformed(DatabaseColumn column)
        {
            // Always respect explicit schema settings first
            // If canTransform is explicitly set to true, allow transformations
            if (column.CanTransform == true)
            {
                return true;
            }

            // If canTransform is explicitly set to false, never allow transformations
            // This check needs to be done carefully since CanTransform might be null/default
            // Check if the property was explicitly set in the JSON by looking at the source
            // For now, we'll assume any false value means explicitly disabled
            if (column.CanTransform == false)
            {
                return false;
            }

            // If canTransform is not explicitly set in the schema (null/default), use heuristic rules
            // This section would only apply to columns without explicit canTransform settings

            // Allow transformations for text fields that likely contain names
            if (column.DataType.ToLower() == "string" &&
                (column.Name.Contains("Name") || column.Name.Contains("Identifier")))
            {
                return true;
            }

            // Allow transformations for date fields
            if (column.DataType.ToLower() == "date" || column.DataType.ToLower().Contains("time"))
            {
                return true;
            }

            // Allow transformations for gender/category fields
            if (column.DataType.ToLower() == "string" &&
                (column.Name.Contains("Gender") || column.Name.Contains("Status") ||
                 column.Name.Contains("Type") || column.Name.Contains("Category")))
            {
                return true;
            }

            // Allow transformations for derived value fields (like BirthYear from DOB)
            if (column.Name.Contains("Year") || column.Name.Contains("Age") ||
                (column.DataType.ToLower() == "int" && column.Name.Contains("Birth")))
            {
                return true;
            }

            // Allow transformations for address parts that might need splitting
            if (column.DataType.ToLower() == "string" &&
                (column.Name.Contains("Address") || column.Name.Contains("Street") ||
                 column.Name.Contains("City") || column.Name.Contains("State") ||
                 column.Name.Contains("Zip") || column.Name.Contains("PostalCode")))
            {
                return true;
            }

            // By default, don't allow transformations for other columns
            return false;
        }

        /// <summary>
        /// Creates CSV column options including an unmap option for optional columns
        /// </summary>
        private ObservableCollection<string> CreateCsvColumnOptions(ObservableCollection<CsvColumn> csvColumns, bool isMappingRequired)
        {
            var options = new ObservableCollection<string>();

            // Add unmap option for optional columns only (based on IsMappingRequired)
            if (!isMappingRequired)
            {
                options.Add(appSettingsModel.Settings.NoMappingOptional);
            }

            // Add all CSV columns
            foreach (var column in csvColumns)
            {
                options.Add(column.Name);
            }

            return options;
        }

        /// <summary>
        /// Updates the sample values for a column mapping
        /// </summary>
        private void UpdateColumnMappingSampleValues(ColumnMappingViewModel mappingVm, CsvMappingTypeViewModel mappingType)
        {
            if (string.IsNullOrEmpty(mappingVm.SelectedCsvColumn) || mappingVm.SelectedCsvColumn == appSettingsModel.Settings.NoMappingOptional)
            {
                mappingVm.SampleValues = new ObservableCollection<string>();
                mappingVm.OriginalSampleValues = new ObservableCollection<string>();
                mappingVm.InferredType = string.Empty;
                return;
            }

            var csvColumn = mappingType.CsvColumns.FirstOrDefault(c => c.Name == mappingVm.SelectedCsvColumn);
            if (csvColumn != null)
            {
                // Set both current and original sample values to preserve data for transformations
                mappingVm.SampleValues = new ObservableCollection<string>(csvColumn.SampleValues);
                mappingVm.OriginalSampleValues = new ObservableCollection<string>(csvColumn.SampleValues);
                mappingVm.InferredType = csvColumn.InferredType;
            }
            else
            {
                mappingVm.SampleValues = new ObservableCollection<string>();
                mappingVm.OriginalSampleValues = new ObservableCollection<string>();
                mappingVm.InferredType = string.Empty;
            }
        }

        /// <summary>
        /// Command to save the mappings to a JSON file
        /// </summary>
        [RelayCommand]
        private async Task SaveMappings()
        {
            // Check if we have any valid mappings
            var completedMappings = MappingTypes.Where(m => m.IsValid).ToList();
            if (completedMappings.Count == 0)
            {
                LoggingService.LogWarning("No valid mappings to save. Please fix validation errors first.", showMsgBox: true);
                return;

            }

            try
            {
                IsLoading = true;
                StatusMessage = "Saving mappings...";

                // Ensure Configuration directory exists
                string configPath = "Configuration/mapping.json";
                Directory.CreateDirectory("Configuration");

                // Create the multi mapping result
                var multiMappingResult = new MultiMappingResult
                {
                    Mappings = completedMappings.Select(m => new MappingResult
                    {
                        TableName = m.TableName,
                        CsvType = m.CsvType,
                        ColumnMappings = m.ColumnMappings
                            .Where(c => !string.IsNullOrEmpty(c.SelectedCsvColumn) && c.SelectedCsvColumn != appSettingsModel.Settings.NoMappingOptional)
                            .Select(c =>
                            {
                                // Handle columns with transformations
                                if (c.HasTransformation && c.TransformationType.HasValue)
                                {
                                    // Pass parameters dictionary directly (no double serialization)
                                    return ColumnMapping.CreateForDerivedColumn(
                                        c.SelectedCsvColumn,
                                        c.DbColumn.Name,
                                        c.SelectedCsvColumn, // Original source column
                                        c.TransformationType.Value.ToString(),
                                        c.TransformationParameters
                                    );
                                }
                                else
                                {
                                    // For columns without transformations
                                    return ColumnMapping.Create(c.SelectedCsvColumn, c.DbColumn.Name);
                                }
                            })
                            .ToList()
                    }).ToList()
                };

                bool success = await _mappingService.SaveMultiMappingsAsync(multiMappingResult, configPath);

                if (success)
                {
                    SavedMappings = multiMappingResult;

                    // Generate verbose success message with mapping types
                    var savedTypes = completedMappings.Select(m => m.CsvType).ToList();
                    string typesList;

                    if (savedTypes.Count == 1)
                    {
                        typesList = savedTypes[0];
                    }
                    else if (savedTypes.Count == 2)
                    {
                        typesList = string.Join(" and ", savedTypes);
                    }
                    else
                    {
                        typesList = string.Join(", ", savedTypes.Take(savedTypes.Count - 1)) + ", and " + savedTypes.Last();
                    }

                    StatusMessage = $"Mappings saved successfully to {configPath}";
                    LoggingService.LogInfo($"Saved mappings successfully: {typesList}", showMsgBox: true);
                }
                else
                {
                    StatusMessage = "Failed to save mappings.";
                    LoggingService.LogWarning("Failed to save mappings.");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving mappings: {ex.Message}";
                LoggingService.LogError($"Error saving mappings: {ex.Message}", ex, showMsgBox: true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Command to auto-match columns for the current mapping type
        /// </summary>
        [RelayCommand]
        private void AutoMatchColumns()
        {
            if (CurrentMappingType == null || !CurrentMappingType.IsCsvLoaded)
            {
                StatusMessage = "No CSV file loaded for this mapping type.";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Auto-matching columns for {CurrentMappingType.CsvType}...";

                var autoMatches = _mappingService.AutoMatchColumns(
                    CurrentMappingType.CsvColumns,
                    CurrentMappingType.TableSchema.Columns);

                // Apply auto-matches to the column mappings
                foreach (var mapping in CurrentMappingType.ColumnMappings)
                {
                    if (autoMatches.TryGetValue(mapping.DbColumn.Name, out var matchedCsvColumn))
                    {
                        mapping.SelectedCsvColumn = matchedCsvColumn;
                    }
                }

                ValidateMappings(CurrentMappingType);
                RefreshSaveButtonState();
                StatusMessage = $"Auto-matched {autoMatches.Count} columns for {CurrentMappingType.CsvType}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during auto-matching: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Validates all column mappings for a mapping type
        /// </summary>
        /// <returns>True if all mappings are valid</returns>
        public bool ValidateMappings(CsvMappingTypeViewModel mappingType)
        {
            if (mappingType.TableSchema == null || mappingType.ColumnMappings.Count == 0)
                return false;

            var errors = _mappingService.ValidateMappings(
                mappingType.ColumnMappings.ToList(),
                mappingType.CsvColumns,
                mappingType.TableSchema.Columns);

            // Update validation errors in view models
            foreach (var mapping in mappingType.ColumnMappings)
            {
                if (errors.TryGetValue(mapping.DbColumn.Name, out var error))
                {
                    mapping.ValidationError = error;
                    mapping.IsValid = false;
                }
                else
                {
                    mapping.ValidationError = string.Empty;
                    // Use IsMappingRequired for UI validation
                    if (string.IsNullOrEmpty(mapping.SelectedCsvColumn) || mapping.SelectedCsvColumn == appSettingsModel.Settings.NoMappingOptional)
                    {
                        mapping.IsValid = false;
                    }
                    else
                    {
                        mapping.IsValid = true;
                    }
                }
            }

            // Update the mapping type validation status
            // Only columns marked as IsMappingRequired need to be mapped for the mapping to be valid
            mappingType.IsValid = errors.Count == 0 &&
                mappingType.ColumnMappings
                    .Where(m => m.DbColumn.IsMappingRequired)
                    .All(m => !string.IsNullOrEmpty(m.SelectedCsvColumn) && m.SelectedCsvColumn != appSettingsModel.Settings.NoMappingOptional);

            // If this is the current mapping type, update the UI
            if (mappingType == CurrentMappingType)
            {
                CanSaveMapping = mappingType.IsValid;
            }

            // Notify property changes for status display
            OnPropertyChanged(nameof(MappingTypes));

            // Check if we have valid mappings for both types
            UpdateCanAddNewMapping();

            return errors.Count == 0;
        }

        private void RefreshSaveButtonState()
        {
            foreach (var mappingType in MappingTypes)
            {
                ValidateMappings(mappingType);
            }
            OnPropertyChanged(nameof(CanSaveAllMappings));
        }

        /// <summary>
        /// Updates the CanAddNewMapping flag based on the state of existing mappings
        /// </summary>
        private void UpdateCanAddNewMapping()
        {
            // Count how many of each CSV type we have
            int patientStudyCount = MappingTypes.Count(m => m.CsvType.Equals(appSettingsModel.Settings.CsvTypes.PatientStudy));
            int seriesInstanceCount = MappingTypes.Count(m => m.CsvType.Equals(appSettingsModel.Settings.CsvTypes.SeriesInstance));

            // We only allow one of each type
            CanAddNewMapping = patientStudyCount < 1 || seriesInstanceCount < 1;
        }

        /// <summary>
        /// Automatically loads schema from Configuration/dbconfig.json
        /// </summary>
        private async Task LoadSchemaAutomatically()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading database schema...";

                // Clear any existing data first
                _databaseSchema = null!;
                AvailableCsvTypes.Clear();
                MappingTypes.Clear();
                CurrentMappingType = null;
                IsSchemaLoaded = false;

                _databaseSchema = await _schemaLoaderService.LoadSchemaAsync();
                IsSchemaLoaded = _databaseSchema.Tables.Count > 0;

                if (IsSchemaLoaded)
                {
                    StatusMessage = $"Schema loaded successfully: {_databaseSchema.Tables.Count} tables found.";

                    // Populate available CSV types (filter out empty/whitespace entries)
                    AvailableCsvTypes.Clear();
                    foreach (var table in _databaseSchema.Tables)
                    {
                        if (!string.IsNullOrWhiteSpace(table.CsvType))
                        {
                            AvailableCsvTypes.Add(table.CsvType.Trim());
                        }
                    }

                    // Select the first type by default if available
                    if (AvailableCsvTypes.Count > 0)
                    {
                        SelectedCsvType = AvailableCsvTypes[0];
                    }
                }
                else
                {
                    StatusMessage = "Database schema not found : no tables available.";
                }
            }
            catch (FileNotFoundException ex)
            {
                StatusMessage = "Database schema not found";
                IsSchemaLoaded = false;
                LoggingService.LogWarning($"FileNotFoundException: {ex.Message}");
                LoggingService.LogWarning($"Searched path: {ex.FileName}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading schema: {ex.Message}";
                IsSchemaLoaded = false;

                LoggingService.LogError("Error occurred while loading schema.", ex, showMsgBox: true);


                // Show detailed error in a message box for debugging
                LoggingService.LogError($"Schema loading error:\n{ex.Message}\n\nCheck Debug Output for more details.", ex, showMsgBox: true);

            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Command to initialize a new application session
        /// </summary>
        [RelayCommand]
        private void ResetCurrentMapping()
        {
            if (CurrentMappingType == null)
            {

                LoggingService.LogWarning("Please select a mapping type first.", showMsgBox: true);
                return;
            }

            LoggingService.LogWarning($"Prompting user: This will erase the current mapping for '{CurrentMappingType.CsvType}'. Do you want to continue?");
            var result = MessageBox.Show(
                $"This will erase the current mapping for '{CurrentMappingType.CsvType}'. Do you want to continue?",
                "Erase Current Mapping?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return; // User canceled the operation
            }

            try // Start of the safety net
            {
                // Reset CurrentMappingType
                CurrentMappingType.CsvFilePath = string.Empty;

                CurrentMappingType.CsvColumns?.Clear();
                CurrentMappingType.ColumnMappings?.Clear();

                CurrentMappingType.IsCsvLoaded = false;
                CurrentMappingType.IsValid = false;
                CurrentMappingType.IsCompleted = false;

                // Reset UI
                // This is safe as you are creating a new instance.
                ColumnMappings = new ObservableCollection<ColumnMappingViewModel>();

                // Remove Saved Mappings
                // Add a defensive check for SavedMappings and its inner collection.
                if (SavedMappings?.Mappings != null)
                {
                    var mappingToRemove = SavedMappings.Mappings.FirstOrDefault(m => m.CsvType == CurrentMappingType.CsvType);
                    if (mappingToRemove != null)
                    {
                        SavedMappings.Mappings.Remove(mappingToRemove);
                    }
                }
                RefreshSaveButtonState();
                StatusMessage = $"{CurrentMappingType.CsvType} mapping has been reset.";
            }
            catch (Exception ex)
            {

                LoggingService.LogError(
                    "An unexpected error occurred and the mapping could not be reset. Please check the logs for more details.",
                    ex,
                    showMsgBox: true
                );
            }
        }
        private void LoadAndValidateAppSettings()
        {
            appSettingsModel.LoadAppSettings();
            bool loadStatus = ValidateAppSettings(out var invalidSettings);
            appSettingsModel.CorrectAndSaveAppSettings(loadStatus, invalidSettings);


        }

        private bool ValidateAppSettings(out List<PropertyInfo> invalidSettings)
        {
            invalidSettings = new List<PropertyInfo>();
            if (appSettingsModel.Settings == null)
            {
                return false;
            }
            else
            {
               
                var properties = appSettingsModel.Settings.GetType().GetProperties();
                foreach (var property in properties)
                {
                    var value = property.GetValue(appSettingsModel.Settings);

                    if (value == null)
                    {
                        invalidSettings.Add(property);
                    }

                }
            }
            return true;
        }
    }
}
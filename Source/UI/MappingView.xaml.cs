using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CMMT.Helpers;
using CMMT.Models.Transformations;
using CMMT.Services;
using CMMT.ViewModels;
using System.Windows.Input;
using CMMT.Models;
using System.IO;

namespace CMMT.UI
{
    /// <summary>
    /// Interaction logic for MappingView.xaml (Page version)
    /// </summary>
    public partial class MappingView : Page
    {
        private readonly ITransformationViewService _transformationService;

        public MappingView(ITransformationViewService transformationService)
        {
            InitializeComponent();
            _transformationService = transformationService;

            // Wire up the unload event for cleanup
            Unloaded += Page_Unloaded;

            // Wire up both Loaded and DataContext changed events
            Loaded += Page_Loaded;
            DataContextChanged += OnDataContextChanged;

            // DataContext will be set by the parent window/container
        }

        /// <summary>
        /// Tracks if the page has been initialized to prevent duplicate subscriptions
        /// </summary>
        private bool _isInitialized = false;

        /// <summary>
        /// Initializes the view model when the page is loaded
        /// </summary>
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Prevent duplicate initialization
            if (_isInitialized)
                return;

            // If DataContext is already available, initialize immediately
            if (DataContext != null)
            {
                await InitializePageAsync();
            }
            // If DataContext is not set yet, OnDataContextChanged will handle initialization
        }

        /// <summary>
        /// Handles DataContext changes to ensure proper initialization
        /// </summary>
        private async void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext != null && !_isInitialized)
            {
                // Small delay to ensure DataContext is fully processed
                await Task.Delay(50);
                await InitializePageAsync();
            }
        }

        /// <summary>
        /// Initializes the page asynchronously
        /// </summary>
        private async Task InitializePageAsync()
        {
            try
            {
                if (DataContext is MainViewModel viewModel && !_isInitialized)
                {
                    // ViewModel should already be initialized when we get here
                    // Subscribe to existing mapping types
                    foreach (var mappingType in viewModel.MappingTypes)
                        SubscribeToColumnMappingEvents(mappingType);

                    // Subscribe to new mapping types being added
                    viewModel.MappingTypes.CollectionChanged += OnMappingTypesCollectionChanged;

                    // Subscribe to property changes on the main view model
                    viewModel.PropertyChanged += OnMainViewModelPropertyChanged;

                    // Mark as initialized after successful setup
                    _isInitialized = true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error initializing mapping view: {ex.Message}", ex, showMsgBox: true);

            }
        }

        /// <summary>
        /// Handles collection changes for mapping types
        /// </summary>
        private void OnMappingTypesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (CsvMappingTypeViewModel newMappingType in e.NewItems)
                    SubscribeToColumnMappingEvents(newMappingType);
            }

            if (e.OldItems != null)
            {
                foreach (CsvMappingTypeViewModel removedMappingType in e.OldItems)
                    UnsubscribeFromMappingTypeEvents(removedMappingType);
            }
        }

        /// <summary>
        /// Handles property changes on the main view model
        /// </summary>
        private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (DataContext is not MainViewModel viewModel)
                return;

            if (e.PropertyName == nameof(MainViewModel.CurrentMappingType))
            {
                // No need to clear all subscriptions - just ensure current mapping type is subscribed
                if (viewModel.CurrentMappingType != null)
                    SubscribeToColumnMappingEvents(viewModel.CurrentMappingType);
            }

            if (e.PropertyName == nameof(MainViewModel.ColumnMappings))
            {
                // When column mappings change, ensure all current mappings are subscribed
                if (viewModel.ColumnMappings != null)
                {
                    foreach (var mapping in viewModel.ColumnMappings)
                    {
                        SubscribeToSingleColumnMapping(mapping);
                    }
                }
            }
        }

        /// <summary>
        /// Dictionary to track subscribed column mappings to prevent duplicate subscriptions
        /// </summary>
        private readonly HashSet<ColumnMappingViewModel> _subscribedMappings = new HashSet<ColumnMappingViewModel>();

        /// <summary>
        /// Dictionary to track event handler subscriptions for mapping types
        /// </summary>
        private readonly Dictionary<CsvMappingTypeViewModel, System.Collections.Specialized.NotifyCollectionChangedEventHandler> _mappingTypeHandlers = new();

        /// <summary>
        /// Subscribes to transformation dialog events for a mapping type
        /// </summary>
        private void SubscribeToColumnMappingEvents(CsvMappingTypeViewModel mappingType)
        {
            if (mappingType?.ColumnMappings == null) return;

            // Subscribe to existing column mappings
            foreach (var columnMapping in mappingType.ColumnMappings)
            {
                SubscribeToSingleColumnMapping(columnMapping);
            }

            // Only subscribe to collection changed if not already subscribed
            if (!_mappingTypeHandlers.ContainsKey(mappingType))
            {
                System.Collections.Specialized.NotifyCollectionChangedEventHandler handler = (s, args) =>
                {
                    if (args.NewItems != null)
                    {
                        foreach (ColumnMappingViewModel newColumnMapping in args.NewItems)
                        {
                            SubscribeToSingleColumnMapping(newColumnMapping);
                        }
                    }

                    if (args.OldItems != null)
                    {
                        foreach (ColumnMappingViewModel removedColumnMapping in args.OldItems)
                        {
                            UnsubscribeFromSingleColumnMapping(removedColumnMapping);
                        }
                    }
                };

                mappingType.ColumnMappings.CollectionChanged += handler;
                _mappingTypeHandlers[mappingType] = handler;
            }
        }

        /// <summary>
        /// Unsubscribes from all events for a specific mapping type
        /// </summary>
        private void UnsubscribeFromMappingTypeEvents(CsvMappingTypeViewModel mappingType)
        {
            if (mappingType?.ColumnMappings == null) return;

            // Unsubscribe from all column mappings in this type
            foreach (var columnMapping in mappingType.ColumnMappings)
            {
                UnsubscribeFromSingleColumnMapping(columnMapping);
            }

            // Remove collection changed handler if it exists
            if (_mappingTypeHandlers.TryGetValue(mappingType, out var handler))
            {
                mappingType.ColumnMappings.CollectionChanged -= handler;
                _mappingTypeHandlers.Remove(mappingType);
            }
        }

        /// <summary>
        /// Subscribes to a single column mapping if not already subscribed
        /// </summary>
        private void SubscribeToSingleColumnMapping(ColumnMappingViewModel columnMapping)
        {
            if (columnMapping == null || _subscribedMappings.Contains(columnMapping))
                return;

            columnMapping.OpenTransformationDialogRequested += OnShowTransformationDialog;
            _subscribedMappings.Add(columnMapping);
        }

        /// <summary>
        /// Unsubscribes from a single column mapping
        /// </summary>
        private void UnsubscribeFromSingleColumnMapping(ColumnMappingViewModel columnMapping)
        {
            if (columnMapping == null || !_subscribedMappings.Contains(columnMapping))
                return;

            columnMapping.OpenTransformationDialogRequested -= OnShowTransformationDialog;
            _subscribedMappings.Remove(columnMapping);
        }

        /// <summary>
        /// Clears all subscriptions
        /// </summary>
        private void ClearAllSubscriptions()
        {
            // Unsubscribe from all individual column mappings
            foreach (var mapping in _subscribedMappings.ToList())
            {
                UnsubscribeFromSingleColumnMapping(mapping);
            }

            // Unsubscribe from all mapping type handlers
            foreach (var kvp in _mappingTypeHandlers.ToList())
            {
                var mappingType = kvp.Key;
                var handler = kvp.Value;

                if (mappingType?.ColumnMappings != null)
                {
                    mappingType.ColumnMappings.CollectionChanged -= handler;
                }
            }
            _mappingTypeHandlers.Clear();

            // Unsubscribe from main view model events if we have a reference
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.MappingTypes.CollectionChanged -= OnMappingTypesCollectionChanged;
                viewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
            }
        }

        /// <summary>
        /// Clean up event subscriptions when the page is unloaded
        /// </summary>
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ClearAllSubscriptions();

                // Reset initialization flag
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                // Log cleanup errors but don't show UI - page is unloading
                LoggingService.LogError("Error during page cleanup", ex);
            }
        }

        /// <summary>
        /// Handles the transformation dialog event
        /// </summary>
        private void OnShowTransformationDialog(object? sender, EventArgs e)
        {
            if (sender is ColumnMappingViewModel mappingViewModel)
            {
                ShowTransformationDialog(mappingViewModel);
            }
        }

        /// <summary>
        /// Reads all distinct values from a specific column in a CSV file
        /// </summary>
        private async Task<List<string>> GetAllDistinctCsvValuesAsync(string csvFilePath, string columnName, string delimiter)
        {
            var distinctValues = new HashSet<string>();

            try
            {
                using var reader = new StreamReader(csvFilePath);

                // Read header line to find column index
                var headerLine = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(headerLine)) return new List<string>();

                var headers = headerLine.Split(delimiter);
                var columnIndex = Array.FindIndex(headers, h => h.Trim() == columnName);

                if (columnIndex == -1)
                {
                    LoggingService.LogWarning($"Column '{columnName}' not found in CSV file.");
                    return new List<string>();
                }

                // Read all data rows
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var values = line.Split(delimiter);
                    if (values.Length > columnIndex && !string.IsNullOrWhiteSpace(values[columnIndex]))
                    {
                        distinctValues.Add(values[columnIndex].Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error reading CSV file for column '{columnName}': {ex.Message}", ex);
                return new List<string>();
            }

            return distinctValues.ToList();
        }

        /// <summary>
        /// Shows the transformation dialog for a column mapping
        /// </summary>
        private async void ShowTransformationDialog(ColumnMappingViewModel mappingViewModel)
        {
            if (string.IsNullOrEmpty(mappingViewModel.SelectedCsvColumn))
            {
                LoggingService.LogWarning("Please select a CSV column first.", showMsgBox: true);
                return;
            }

            var mainViewModel = (MainViewModel)DataContext;
            if (mainViewModel?.CurrentMappingType == null)
            {
                LoggingService.LogError("No mapping type selected.", new InvalidOperationException("No mapping type selected."), showMsgBox: true);
                return;
            }

            if (mappingViewModel.DbColumn.Name.Equals("InstitutionName", StringComparison.OrdinalIgnoreCase))
            {
                var selectedCSVColumn = mappingViewModel.SelectedCsvColumn;
                var csvCol = mainViewModel.CurrentMappingType.CsvColumns.FirstOrDefault(c => c.Name == selectedCSVColumn);
                var sources = await GetAllDistinctCsvValuesAsync(
                    mainViewModel.CurrentMappingType.CsvFilePath,
                    selectedCSVColumn,
                    AppSettingsModel.GetInstance.Settings.CsvDelimitter);

                if (!sources.Any())
                {
                    LoggingService.LogWarning("No sample values found in the selected CSV column for transformation.", showMsgBox: true);
                    return;
                }

                LoggingService.LogInfo("Loading target institute names for transformation...");

                try
                {
                    var configFilePath = ConfigFileHelper.GetConfigFilePath("Configuration", "dbconfig.json");
                    var config = await ConfigFileHelper.LoadAsync<DatabaseConfig>(configFilePath);
                    var encryptedConnStr = config.TargetDatabase.EncryptedConnectionString;

                    var targets = await DatabaseService.LoadDistinctInstituteNamesAsync(encryptedConnStr);
                    LoggingService.LogInfo($"Target mappings list: {targets}");

                    var vm = new ValueMappingDialogViewModel(sources, targets);
                    var dialog = new ValueMappingDialog(vm) { Owner = Window.GetWindow(this) };

                    var result = dialog.ShowDialog();
                    if (result == true)
                    {
                        var mappingDict = vm.GetMappings();
                        var defaultTargetValue = vm.DefaultTargetValue;
                        mappingViewModel.ApplyValueMappingTransformation(mappingDict, mainViewModel.CurrentMappingType.CsvColumns, defaultTargetValue);
                        mainViewModel.ValidateMappings(mainViewModel.CurrentMappingType);

                        var transformationSummary = string.Join(", ", mappingDict.Select(kv => $"'{kv.Key}' → '{kv.Value}'"));
                        LoggingService.LogInfo($"Value mapping transformation applied successfully: {transformationSummary}");
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogError("Failed to load target institutes for transformation.", ex, true);
                }

                return;
            }
            //TODO: Refactor this to use a common helper for all value mappings
            if (mappingViewModel.DbColumn.Name.Equals("StudyType", StringComparison.OrdinalIgnoreCase))
            {
                var selectedCSVColumn = mappingViewModel.SelectedCsvColumn;
                var csvCol = mainViewModel.CurrentMappingType.CsvColumns.FirstOrDefault(c => c.Name == selectedCSVColumn);
                var sources = await GetAllDistinctCsvValuesAsync(
                    mainViewModel.CurrentMappingType.CsvFilePath,
                    selectedCSVColumn,
                    AppSettingsModel.GetInstance.Settings.CsvDelimitter);

                if (!sources.Any())
                {
                    LoggingService.LogWarning("No sample values found in the selected CSV column for transformation.");
                    return;
                }

                LoggingService.LogInfo("Loading target study types for transformation...");

                try
                {
                    var configFilePath = ConfigFileHelper.GetConfigFilePath("Configuration", "dbconfig.json");
                    var config = await ConfigFileHelper.LoadAsync<DatabaseConfig>(configFilePath);
                    var encryptedConnStr = config.TargetDatabase.EncryptedConnectionString;

                    var targets = await DatabaseService.LoadDistinctStudyTypesAsync(encryptedConnStr);
                    LoggingService.LogInfo($"Target mappings list: {targets}");

                    var vm = new ValueMappingDialogViewModel(sources, targets);
                    var dialog = new ValueMappingDialog(vm) { Owner = Window.GetWindow(this) };

                    var result = dialog.ShowDialog();
                    if (result == true)
                    {
                        var mappingDict = vm.GetMappings();
                        var defaultTargetValue = vm.DefaultTargetValue;
                        mappingViewModel.ApplyValueMappingTransformation(mappingDict, mainViewModel.CurrentMappingType.CsvColumns, defaultTargetValue);
                        mainViewModel.ValidateMappings(mainViewModel.CurrentMappingType);

                        var transformationSummary = string.Join(", ", mappingDict.Select(kv => $"'{kv.Key}' → '{kv.Value}'"));
                        LoggingService.LogInfo($"Value mapping transformation applied successfully: {transformationSummary}");
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogError("Failed to load target study types for transformation.", ex, true);
                }

                return;
            }

            ShowTransformationConfigurationDialog(mappingViewModel, $"Configure Transformation for {mappingViewModel.SelectedCsvColumn}");
        }

        /// <summary>
        /// Shows the transformation configuration dialog
        /// </summary>
        private void ShowTransformationConfigurationDialog(ColumnMappingViewModel mappingViewModel, string title)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var scrollViewer = new ScrollViewer();
            var mainPanel = new StackPanel { Margin = new Thickness(20) };

            // Header
            var headerText = new TextBlock
            {
                Text = $"Configure Transformation for: {mappingViewModel.SelectedCsvColumn}",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            };
            mainPanel.Children.Add(headerText);

            // Get available transformations for this column
            var availableTransformations = mappingViewModel.AvailableTransformations ?? new ObservableCollection<TransformationType>();


            if (!availableTransformations.Any())
            {
                var noTransformText = new TextBlock
                {
                    Text = "No transformations available for this column type.",
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                mainPanel.Children.Add(noTransformText);
            }
            else
            {
                // Transformation Type Selection
                var typeLabel = new TextBlock
                {
                    Text = "Transformation Type",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                mainPanel.Children.Add(typeLabel);

                var transformationComboBox = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 20),
                    Height = 30
                };

                foreach (var transformationType in availableTransformations)
                {
                    transformationComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = GetTransformationDisplayName(transformationType, mappingViewModel.DbColumn?.Name ?? ""),
                        Tag = transformationType
                    });
                }

                transformationComboBox.SelectedIndex = 0;
                mainPanel.Children.Add(transformationComboBox);

                // Parameters Panel
                var parametersPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
                mainPanel.Children.Add(parametersPanel);

                // Parameters storage
                var parameters = new Dictionary<string, object>();

                // Function to update parameters panel based on selected transformation
                Action updateParametersPanel = () =>
                {
                    parametersPanel.Children.Clear();
                    if (transformationComboBox.SelectedItem is ComboBoxItem selectedItem &&
                        selectedItem.Tag is TransformationType transformationType)
                    {
                        CreateParametersUI(parametersPanel, transformationType, parameters); 
                    }
                };

                // Handle transformation type selection change
                transformationComboBox.SelectionChanged += (s, e) => updateParametersPanel();

                // Initialize parameters panel
                updateParametersPanel();

                // Sample Values Preview
                var previewLabel = new TextBlock
                {
                    Text = "Preview sample values",
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                mainPanel.Children.Add(previewLabel);

                var previewPanel = new StackPanel
                {
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    Margin = new Thickness(0, 0, 0, 20),
                    MaxHeight = 100
                };
                mainPanel.Children.Add(previewPanel);

                // Function to update preview
                Action updatePreview = () =>
                {
                    previewPanel.Children.Clear();
                    if (transformationComboBox.SelectedItem is ComboBoxItem selectedItem &&
                        selectedItem.Tag is TransformationType transformationType)
                    {
                        UpdateTransformationPreview(previewPanel, mappingViewModel, transformationType, parameters);
                    }
                };

                // Update preview when parameters change
                transformationComboBox.SelectionChanged += (s, e) => updatePreview();
                updatePreview();

                // Buttons
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 20, 0, 0)
                };

                var okButton = new Button
                {
                    Content =
                    "Apply Transformation",
                    Width = 160,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                var cancelButton = new Button
                {
                    Content = "Cancel",
                    Width = 90,
                    Margin = new Thickness(0, 0, 10, 0)

                };

                okButton.Click += (s, e) =>
                {
                    if (transformationComboBox.SelectedItem is ComboBoxItem selectedItem &&
                        selectedItem.Tag is TransformationType transformationType)
                    {
                        try
                        {
                            var transformation = _transformationService.CreateTransformation(transformationType, mappingViewModel.SelectedCsvColumn);

                            // Apply transformation WITH parameters
                            mappingViewModel.ApplyTransformation(transformation, parameters);

                            // Update validation
                            var mainViewModel = (MainViewModel)DataContext;
                            if (mainViewModel?.CurrentMappingType != null)
                            {
                                mainViewModel.ValidateMappings(mainViewModel.CurrentMappingType);
                            }

                            dialog.DialogResult = true;
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogError($"Error applying transformation: {ex.Message}", ex, showMsgBox: true);

                        }
                    }
                };

                cancelButton.Click += (s, e) => dialog.DialogResult = false;

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                mainPanel.Children.Add(buttonPanel);
            }

            scrollViewer.Content = mainPanel;
            dialog.Content = scrollViewer;
            dialog.ShowDialog();
        }

        /// <summary>
        /// Creates parameter UI controls based on transformation type
        /// </summary>
        private void CreateParametersUI(StackPanel parametersPanel, TransformationType transformationType, Dictionary<string, object> parameters)
        {
            switch (transformationType)
            {

                case TransformationType.SplitByIndexToken:
                    CreateDelimiterParameterUI(parametersPanel, parameters);
                    CreateTokenIndexParameterUI(parametersPanel, parameters); 
                    break;
                case TransformationType.DateFormat:
                    CreateDateFormatParameterUI(parametersPanel, parameters);
                    break;

                case TransformationType.CategoryMapping:
                    CreateCategoryMappingParameterUI(parametersPanel, parameters);
                    break;
            }
        }

        /// <summary>
        /// Creates delimiter parameter UI for split transformations
        /// </summary>
        private void CreateDelimiterParameterUI(StackPanel parametersPanel, Dictionary<string, object> parameters)
        {
            var delimiterLabel = new TextBlock
            {
                Text = "Choose Delimiter",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            parametersPanel.Children.Add(delimiterLabel);

            var delimiterComboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                Height = 25
            };

            // Add common delimiters
            var delimiters = new Dictionary<string, string>
            {
                {"Caret (^)","^" },
                { "Space ( )", " " },
                { "Comma (,)", "," },
                { "Semicolon (;)", ";" },
                { "Tab (\\t)", "\t" },
                { "Pipe (|)", "|" },
                { "Hyphen (-)", "-" },
                { "Underscore (_)", "_" }
            };

            foreach (var delimiter in delimiters)
            {
                delimiterComboBox.Items.Add(new ComboBoxItem
                {
                    Content = delimiter.Key,
                    Tag = delimiter.Value
                });
            }

            delimiterComboBox.SelectedIndex = 0; // Default to space
            delimiterComboBox.SelectionChanged += (s, e) =>
            {
                if (delimiterComboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    parameters["Delimiter"] = selectedItem.Tag.ToString();
                }
            };

            parametersPanel.Children.Add(delimiterComboBox);

            var helpText = new TextBlock
            {
                Text = "Select the character to split on",
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                Margin = new Thickness(0, 0, 0, 15)
            };
            parametersPanel.Children.Add(helpText);

            // Set initial value
            if (delimiterComboBox.SelectedItem is ComboBoxItem initialItem)
            {
                parameters["Delimiter"] = initialItem.Tag.ToString();
            }
        }

        /// <summary>
        /// Creates date format parameter UI
        /// </summary>
        private void CreateTokenIndexParameterUI(StackPanel parametersPanel, Dictionary<string, object> parameters)
        {
            var label = new TextBlock
            {
                Text = "Enter Token Position (1-5)",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 5)
            };
            parametersPanel.Children.Add(label);

            var textBox = new TextBox
            {
                Height = 25,
                Width = 100,
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var warningMsg = new TextBlock
            {
                Text = "Please enter a number between 1 to 5",
                Foreground = Brushes.Red,
                FontSize = 11,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 10)
            };
            parametersPanel.Children.Add(warningMsg);

            // Allow only 1 to 5
            textBox.PreviewTextInput += (s, e) =>
            {
                // Determine if the incoming character is a valid single digit 1-5
                bool isValid = Regex.IsMatch(e.Text, "^[1-5]$") && (textBox.Text.Length == 0 || textBox.SelectionLength > 0);
                e.Handled = !isValid;
                warningMsg.Visibility = isValid ? Visibility.Collapsed : Visibility.Visible;
            };
            // Disallow space
            textBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Space) e.Handled = true;
            };

            textBox.TextChanged += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    warningMsg.Text = "A valid token index is required";
                    warningMsg.Visibility = Visibility.Visible;

                    // Remove the parameter to fallback to raw string return
                    parameters.Remove("TokenIndex");
                }
                else if (int.TryParse(textBox.Text, out int val) && val >= 1 && val <= 5)
                {
                    parameters["TokenIndex"] = val - 1; // 0-based index
                    warningMsg.Visibility = Visibility.Collapsed;
                }
                else
                {
                    warningMsg.Text = "Value must be between 1 and 5";
                    warningMsg.Visibility = Visibility.Visible;

                    // Still keep previous valid value or remove?
                    parameters.Remove("TokenIndex");
                }
            };

            // Initial state
            textBox.Text = "1";  // Default input value
            parametersPanel.Children.Add(textBox);

            var helpText = new TextBlock
            {
                Text = "Example: 1=LastName, 2=FirstName, 3=MiddleName, 4=Title, 5=Honorific...",
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                Margin = new Thickness(0, 0, 0, 15)
            };
            parametersPanel.Children.Add(helpText);
        }
        private void CreateDateFormatParameterUI(StackPanel parametersPanel, Dictionary<string, object> parameters)
        {
            // Source Format Section
            var sourceFormatLabel = new TextBlock
            {
                Text = "Source Date Format:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            parametersPanel.Children.Add(sourceFormatLabel);

            var sourceFormatComboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                Height = 25
            };

            // Add common source date formats with Auto-detect as default
            var sourceDateFormats = new Dictionary<string, string>
                {
                    { "MM-dd-yyyy", "dd-MM-yyyy" },
                    { "US Format (MM/dd/yyyy)", "MM-dd-yyyy" },
                    { "European (dd/MM/yyyy)", "dd-MM-yyyy" },
                    { "ISO8601 (yyyy-MM-dd)", "yyyy-MM-dd" },
                    { "File Friendly (yyyyMMdd)", "yyyyMMdd" },
                    { "US with time (MM-dd-yyyy HH:mm:ss)", "MM/dd/yyyy HH:mm:ss" },
                    { "ISO with time (yyyy-MM-dd HH:mm:ss)", "yyyy-MM-dd HH:mm:ss" },
                    { "ISO DateTime Stamp (yyyy-MM-ddTHH:mm:ss)", "yyyy-MM-ddTHH:mm:ss" },
                    { "Long Date (MMMM d, yyyy)", "MMMM d, yyyy" },
                    { "Short Date (MMM d, yyyy)", "MMM d, yyyy" }
                };

            foreach (var format in sourceDateFormats)
            {
                sourceFormatComboBox.Items.Add(new ComboBoxItem
                {
                    Content = format.Key,
                    Tag = format.Value
                });
            }

            sourceFormatComboBox.SelectedIndex = 0; // Default to Auto-detect
            sourceFormatComboBox.SelectionChanged += (s, e) =>
            {
                if (sourceFormatComboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    parameters["SourceFormat"] = selectedItem.Tag.ToString();
                }
            };

            parametersPanel.Children.Add(sourceFormatComboBox);

            var sourceHelpText = new TextBlock
            {
                Text = "Select the format of your input dates (Auto-detect will try common formats)",
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                Margin = new Thickness(0, 0, 0, 15)
            };
            parametersPanel.Children.Add(sourceHelpText);

            // Target Format Section
            var targetFormatLabel = new TextBlock
            {
                Text = "Target Date Format:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            parametersPanel.Children.Add(targetFormatLabel);

            var targetFormatComboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                Height = 25
            };

            // Add common target date formats
            var targetDateFormats = new Dictionary<string, string>
            {
                    { "ISO8601 (yyyy-MM-dd HH:mm:ss)", "yyyy-MM-dd HH:mm:ss" }
            };

            foreach (var format in targetDateFormats)
            {
                targetFormatComboBox.Items.Add(new ComboBoxItem
                {
                    Content = format.Key,
                    Tag = format.Value
                });
            }

            targetFormatComboBox.SelectedIndex = 0; // Default to ISO8601
            targetFormatComboBox.SelectionChanged += (s, e) =>
            {
                if (targetFormatComboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    parameters["TargetFormat"] = selectedItem.Tag.ToString();
                }
            };

            parametersPanel.Children.Add(targetFormatComboBox);

            var targetHelpText = new TextBlock
            {
                Text = "Select the desired output format for dates",
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                Margin = new Thickness(0, 0, 0, 15)
            };
            parametersPanel.Children.Add(targetHelpText);

            // Set initial values
            if (sourceFormatComboBox.SelectedItem is ComboBoxItem initialSourceItem)
            {
                parameters["SourceFormat"] = initialSourceItem.Tag.ToString();
            }
            if (targetFormatComboBox.SelectedItem is ComboBoxItem initialTargetItem)
            {
                parameters["TargetFormat"] = initialTargetItem.Tag.ToString();
            }
        }

        /// <summary>
        /// Creates category mapping parameter UI
        /// </summary>
        private void CreateCategoryMappingParameterUI(StackPanel parametersPanel, Dictionary<string, object> parameters)
        {
            var mappingLabel = new TextBlock
            {
                Text = "Category Mappings:",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            parametersPanel.Children.Add(mappingLabel);

            var helpText = new TextBlock
            {
                Text = "Define how values should be mapped (e.g., Male=M, Female=F)",
                FontStyle = FontStyles.Italic,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            parametersPanel.Children.Add(helpText);

            // For now, provide a simple text area for category mappings
            var mappingTextBox = new TextBox
            {
                Text = "Male=M\nFemale=F\nUnknown=U",
                AcceptsReturn = true,
                Height = 80,
                Margin = new Thickness(0, 0, 0, 15)
            };

            mappingTextBox.TextChanged += (s, e) =>
            {
                // Parse the mapping text and store in parameters
                var mappings = new Dictionary<string, string>();
                var lines = mappingTextBox.Text.Split('\n');
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        mappings[parts[0].Trim()] = parts[1].Trim();
                    }
                }
                parameters["TargetMappings"] = mappings;
            };

            parametersPanel.Children.Add(mappingTextBox);

            // Set initial value
            var initialMappings = new Dictionary<string, string>
            {
                { "Male", "M" },
                { "Female", "F" },
                { "Unknown", "U" }
            };
            parameters["TargetMappings"] = initialMappings;
        }

        /// <summary>
        /// Updates the transformation preview with sample values
        /// </summary>
        private void UpdateTransformationPreview(StackPanel previewPanel, ColumnMappingViewModel mappingViewModel, TransformationType transformationType, Dictionary<string, object> parameters)
        {
            previewPanel.Children.Clear();

            try
            {
                var transformation = _transformationService.CreateTransformation(transformationType, mappingViewModel.SelectedCsvColumn);

                // Parameters are passed directly to Transform method, not stored in transformation
                var appSettingsModel = AppSettingsModel.GetInstance;
                // Get sample values (first 3)
                var sampleValues = mappingViewModel.SampleValues?.Take((int)appSettingsModel.Settings.PreviewSampleCount).ToList() ?? new List<string>();

                foreach (var sampleValue in sampleValues)
                {
                    var originalText = new TextBlock
                    {
                        Text = $"'{sampleValue}' → ",
                        Margin = new Thickness(0, 2, 0, 2),
                        FontFamily = new FontFamily("Consolas")
                    };

                    var transformedValue = transformation.Transform(sampleValue, parameters);
                    var transformedText = new TextBlock
                    {
                        Text = $"'{transformedValue}'",
                        Margin = new Thickness(0, 2, 0, 2),
                        FontFamily = new FontFamily("Consolas"),
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 128, 0))
                    };

                    var previewRow = new StackPanel { Orientation = Orientation.Horizontal };
                    previewRow.Children.Add(originalText);
                    previewRow.Children.Add(transformedText);

                    previewPanel.Children.Add(previewRow);
                }

                if (!sampleValues.Any())
                {
                    var noDataText = new TextBlock
                    {
                        Text = "No sample data available for preview",
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128))
                    };
                    previewPanel.Children.Add(noDataText);
                }
            }
            catch (Exception ex)
            {
                var errorText = new TextBlock
                {
                    Text = $"Preview error: {ex.Message}",
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0)),
                    FontStyle = FontStyles.Italic
                };
                previewPanel.Children.Add(errorText);
            }
        }



        /// <summary>
        /// Creates a UI element for a transformation option
        /// </summary>
        private UIElement CreateTransformationOption(TransformationType transformationType, ColumnMappingViewModel mappingViewModel)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };

            var button = new Button
            {
                Content = GetTransformationDisplayName(transformationType, mappingViewModel.DbColumn?.Name ?? ""),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 5, 10, 5)
            };

            button.Click += (s, e) => ApplyTransformation(mappingViewModel, transformationType);

            panel.Children.Add(button);
            return panel;
        }

        /// <summary>
        /// Applies a transformation to a column mapping
        /// </summary>
        private void ApplyTransformation(ColumnMappingViewModel mappingViewModel, TransformationType transformationType)
        {
            try
            {
                var transformation = _transformationService.CreateTransformation(transformationType, mappingViewModel.SelectedCsvColumn);
                mappingViewModel.ApplyTransformation(transformation);

                // Update validation
                var mainViewModel = (MainViewModel)DataContext;
                if (mainViewModel?.CurrentMappingType != null)
                {
                    mainViewModel.ValidateMappings(mainViewModel.CurrentMappingType);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error applying transformation: {ex.Message}", ex, showMsgBox: true);

            }
        }

        private string GetTransformationDisplayName(TransformationType type, string columnName)
        {
            switch (type)
            {
                case TransformationType.SplitByIndexToken:
                    return "Select Token Index";
                case TransformationType.DateFormat:
                    return columnName.Contains("Year") ? "Extract Year from Date" : "Format Date";
                case TransformationType.CategoryMapping:
                    return columnName.Contains("Gender") ? "Standardize Gender Values" : "Map Categories";
                default:
                    return type.ToString();
            }
        }

        /// <summary>
        /// Clears the transformation for the selected column mapping
        /// </summary>
        private void ClearTransformButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ColumnMappingViewModel mappingViewModel)
            {
                // Execute the clear transformation command
                mappingViewModel.ClearTransformationCommand.Execute(null);

                // Update validation
                var mainViewModel = (MainViewModel)DataContext;
                if (mainViewModel.CurrentMappingType != null)
                {
                    mainViewModel.ValidateMappings(mainViewModel.CurrentMappingType);
                }
            }
        }
    }
}
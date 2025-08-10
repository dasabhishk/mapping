using System.Collections.ObjectModel;
using CMMT.Helpers;
using CMMT.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CMMT.ViewModels
{
    /// <summary>
    /// View model for CSV mapping type management
    /// </summary>
    public partial class CsvMappingTypeViewModel : ObservableObject
    {
        /// <summary>
        /// Type of CSV mapping (PatientStudy or SeriesInstance)
        /// </summary>
        [ObservableProperty]
        private string _csvType = string.Empty;

        /// <summary>
        /// CSV columns loaded from the file
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<CsvColumn> _csvColumns = new();

        /// <summary>
        /// Flag indicating if this mapping is complete and valid
        /// </summary>
        [ObservableProperty]
        private bool _isValid;

        /// <summary>
        /// Flag indicating if this mapping type has been completed
        /// </summary>
        [ObservableProperty]
        private bool _isCompleted;

        /// <summary>
        /// Path to the CSV file for this mapping type
        /// </summary>
        [ObservableProperty]
        private string _csvFilePath = string.Empty;

        /// <summary>
        /// Name of the target database table
        /// </summary>
        [ObservableProperty]
        private string _tableName = string.Empty;

        /// <summary>
        /// Flag indicating if CSV file is loaded
        /// </summary>
        [ObservableProperty]
        private bool _isCsvLoaded;

        /// <summary>
        /// Schema table definition
        /// </summary>
        [ObservableProperty]
        private TableSchema? _tableSchema;

        /// <summary>
        /// Column mappings for this type
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ColumnMappingViewModel> _columnMappings = new();


        private readonly AppSettingsModel appSettingsModel = AppSettingsModel.GetInstance;

        /// <summary>
        /// Count of mapped columns
        /// </summary>
        public int MappedColumnsCount => ColumnMappings?.Count(c => !string.IsNullOrEmpty(c.SelectedCsvColumn) && c.SelectedCsvColumn != appSettingsModel.Settings.NoMappingOptional) ?? 0;

        /// <summary>
        /// Validation summary for display
        /// </summary>
        public string ValidationSummary
        {
            get
            {
                if (ColumnMappings == null || !ColumnMappings.Any())
                    return "No columns";

                var errors = ColumnMappings.Count(c => !string.IsNullOrEmpty(c.ValidationError));
                var warnings = ColumnMappings.Count(c => !string.IsNullOrEmpty(c.ValidationWarning));

                if (errors > 0 && warnings > 0)
                    return $"{errors} errors, {warnings} warnings";
                else if (errors > 0)
                    return $"{errors} errors";
                else if (warnings > 0)
                    return $"{warnings} warnings";
                else
                    return "No issues";
            }
        }
    }
}
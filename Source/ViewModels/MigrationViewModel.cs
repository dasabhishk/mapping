using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using CMMT.Helpers;
using CMMT.Models;
using System.Windows.Controls;
using CMMT.Services;
using CMMT.DataStructures;

namespace CMMT.ViewModels
{
    public class MigrationViewModel : INotifyPropertyChanged
    {
        private readonly ISchemaLoaderService _schemaLoaderService;
        private readonly MigrationManager _migrationManager;
        private string? _currentJobId;
        private static readonly AppSettingsModel appSettingsModel = AppSettingsModel.GetInstance;
        private static CsvTypes csvTypes = appSettingsModel.Settings.CsvTypes;
        private string _stagingDatabaseName = "Loading...";

        public string StagingDatabaseName
        {
            get => _stagingDatabaseName;
            set
            {
                SetProperty(ref _stagingDatabaseName, value);
                ValidateDatabaseNames();
            }
        }

        private string _targetDatabaseName = "Loading...";
        public string TargetDatabaseName
        {
            get => _targetDatabaseName;
            set
            {
                SetProperty(ref _targetDatabaseName, value);
                ValidateDatabaseNames();
            }
        }

        private string _databaseValidationError = string.Empty;

        public string DatabaseValidationError
        {
            get => _databaseValidationError;
            set => SetProperty(ref _databaseValidationError, value);
        }
        public bool _hasDatabaseError;
        public bool HasDatabaseError
        {
            get => _hasDatabaseError;
            set => SetProperty(ref _hasDatabaseError, value);
        }

        private int _migrationProgress;
        public int MigrationProgress
        {
            get => _migrationProgress;
            set => SetProperty(ref _migrationProgress, value);
        }

        private bool _isMigrationRunning;
        public bool IsMigrationRunning
        {
            get => _isMigrationRunning;
            set
            {
                SetProperty(ref _isMigrationRunning, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isCancelling;

        public bool IsCancelling
        {
            get => _isCancelling;
            set
            {
                SetProperty(ref _isCancelling, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }
        // Radio Button Properties for Instant/Scheduled migration
        private bool _isScheduledMigrationSelected;
        public bool IsScheduledMigrationSelected
        {
            get => _isScheduledMigrationSelected;
            set
            {
                if (_isScheduledMigrationSelected != value)
                {
                    _isScheduledMigrationSelected = value;
                    OnPropertyChanged(nameof(IsScheduledMigrationSelected));
                    SelectedMigrationOption = value ? MigrationOptionType.Scheduled : MigrationOptionType.Instant;
                    if (SelectedMigrationOption == MigrationOptionType.Scheduled)
                    {
                        TotalNoOfStudies = ""; // Reset TotalNoOfStudies for Scheduled migration
                    }
                }
            }
        }

        // Stores the actual migration type (Instant or Scheduled)
        private MigrationOptionType _selectedMigrationOption;
        public MigrationOptionType SelectedMigrationOption
        {
            get => _selectedMigrationOption;
            set => SetProperty(ref _selectedMigrationOption, value);
        }

        // Properties for Scheduled Migration times
        private DateTime _scheduledStartTime = DateTime.Now.AddMinutes(3);
        public DateTime ScheduledStartTime
        {
            get => _scheduledStartTime;
            set => SetProperty(ref _scheduledStartTime, value);
        }

        private DateTime _scheduledEndTime = DateTime.Now.AddHours(1);
        public DateTime ScheduledEndTime
        {
            get => _scheduledEndTime;
            set => SetProperty(ref _scheduledEndTime, value);
        }

        private string _totalNoOfStudies = "0";
        public string TotalNoOfStudies
        {
            get => _totalNoOfStudies;
            set => SetProperty(ref _totalNoOfStudies, value);
        }

        // Separate properties for date and time components
        public DateTime ScheduledStartDateOnly
        {
            get => _scheduledStartTime.Date;
            set => UpdateStartDateTime(value, ScheduledStartTimeOnly);
        }

        public TimeSpan ScheduledStartTimeOnly
        {
            get => _scheduledStartTime.TimeOfDay;
            set => UpdateStartDateTime(ScheduledStartDateOnly, value);
        }

        public string ScheduledStartTimeString
        {
            get => ScheduledStartTimeOnly.ToString(@"hh\:mm");
            set
            {
                if (TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan time))
                {
                    ScheduledStartTimeOnly = time;
                }
            }
        }

        public DateTime ScheduledEndDateOnly
        {
            get => _scheduledEndTime.Date;
            set => UpdateEndDateTime(value, ScheduledEndTimeOnly);
        }

        public TimeSpan ScheduledEndTimeOnly
        {
            get => _scheduledEndTime.TimeOfDay;
            set => UpdateEndDateTime(ScheduledEndDateOnly, value);
        }

        public string ScheduledEndTimeString
        {
            get => ScheduledEndTimeOnly.ToString(@"hh\:mm");
            set
            {
                if (TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan time))
                {
                    ScheduledEndTimeOnly = time;
                }
            }
        }

        private void UpdateStartDateTime(DateTime date, TimeSpan time)
        {
            _scheduledStartTime = date.Date.Add(time);
            OnPropertyChanged(nameof(ScheduledStartTime));
            OnPropertyChanged(nameof(ScheduledStartTimeString));
        }

        private void UpdateEndDateTime(DateTime date, TimeSpan time)
        {
            _scheduledEndTime = date.Date.Add(time);
            OnPropertyChanged(nameof(ScheduledEndTime));
            OnPropertyChanged(nameof(ScheduledEndTimeString));
        }

        // Available CSV types
        public ObservableCollection<string> AvailableCsvTypes { get; } = new()
        {
            csvTypes.PatientStudy,
            csvTypes.SeriesInstance
        };

        // CSV Type Selection for Migration
        private string _selectedCsvType = csvTypes.PatientStudy;
        public string SelectedCsvType
        {
            get => _selectedCsvType;
            set
            {
                _selectedCsvType = value;
                OnPropertyChanged();
            }
        }

        // Job Status
        private string _jobStatus = "Ready";
        public string JobStatus
        {
            get => _jobStatus;
            set => SetProperty(ref _jobStatus, value);
        }

        public ICommand StartMigrationCommand { get; private set; }
        public ICommand StopMigrationCommand { get; private set; }
        public ICommand LoadDatabaseNamesCommand { get; private set; }


        public MigrationViewModel(
            ISchemaLoaderService schemaLoaderService,
            MigrationManager migrationManager)
        {
            _schemaLoaderService = schemaLoaderService;
            _migrationManager = migrationManager;

            StartMigrationCommand = new RelayCommand(async (param) => await ExecuteStartMigrationAsync(), (param) => CanExecuteStartMigration());
            StopMigrationCommand = new RelayCommand(async (param) => await ExecuteStopMigrationAsync(), (param) => CanExecuteStopMigration());
            LoadDatabaseNamesCommand = new RelayCommand(async (param) => await ExecuteLoadDatabaseNamesAsync());

            SelectedMigrationOption = MigrationOptionType.Instant; // Default to Instant migration
            IsScheduledMigrationSelected = false; // Initial state reflects Instant
            MigrationProgress = 0;
            SelectedCsvType = AvailableCsvTypes.First();

            // Set default times to current time instead of midnight
            var currentTime = DateTime.Now;
            _scheduledStartTime = currentTime.AddMinutes(3);
            _scheduledEndTime = currentTime.AddHours(1);

            ValidateDatabaseNames();
            LoadDatabaseNamesCommand.Execute(null);
        }

        private void ValidateDatabaseNames()
        {
            var invalidNames = new[] { "Loading", "Error", "missing config", "N/A", "", null };
            bool stagingInvalid = invalidNames.Contains(StagingDatabaseName);
            bool targetInvalid = invalidNames.Contains(TargetDatabaseName);

            if(stagingInvalid && targetInvalid)
            {
                DatabaseValidationError = "Both staging and target database names are missing or invalid";
                HasDatabaseError = true;
            }

            else if (stagingInvalid)
            {
                DatabaseValidationError = "Staging databse is missing";
                HasDatabaseError = true;
            }

            else if (targetInvalid)
            {
                DatabaseValidationError = "Target database is missing";
                HasDatabaseError = true;
            }
            else
            {
                DatabaseValidationError = string.Empty;
                HasDatabaseError = false;
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private async Task ExecuteLoadDatabaseNamesAsync()
        {
            try
            {
                var dbConfig = await _schemaLoaderService.LoadDatabaseConfigAsync();
                if (dbConfig != null)
                {
                    StagingDatabaseName = dbConfig.StagingDatabase?.Database ?? "N/A";
                    LoggingService.LogInfo($"{StagingDatabaseName}");
                    TargetDatabaseName = dbConfig.TargetDatabase?.Database ?? "N/A";
                    LoggingService.LogInfo($"MigrationViewModel: Loaded Staging DB: {StagingDatabaseName}, Target DB: {TargetDatabaseName}");
                }
                else
                {
                    StagingDatabaseName = "Error";
                    TargetDatabaseName = "Error";
                    LoggingService.LogWarning("MigrationViewModel: Failed to load database configuration for names.", showMsgBox: true);
                }
            }
            catch (FileNotFoundException fex)
            {
                LoggingService.LogError($"MigrationViewModel: dbconfig.json not found: {fex.Message}", fex, showMsgBox: true);
                StagingDatabaseName = "Missing Config";
                TargetDatabaseName = "Missing Config";
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"MigrationViewModel: An error occurred while loading database names: {ex.Message}", ex, showMsgBox: true);
                StagingDatabaseName = "Error";
                TargetDatabaseName = "Error";
            }
        }

        private async Task ExecuteStartMigrationAsync()
        {
            IsMigrationRunning = true;
            MigrationProgress = 0;
            JobStatus = "Starting...";

            string migrationTypeDescription = SelectedMigrationOption == MigrationOptionType.Instant ? "Instant" : "Scheduled";
            LoggingService.LogInfo($"Migration started (Type: {migrationTypeDescription}, CSV Type: {SelectedCsvType})");

            if (SelectedMigrationOption == MigrationOptionType.Scheduled)
            {
                LoggingService.LogInfo($"Scheduled Migration Details: Start={ScheduledStartTime}, End={ScheduledEndTime}");

                // Comprehensive validation for scheduled migrations
                var now = DateTime.Now;
                var validationBuffer = TimeSpan.FromMinutes(2); // 2-minute buffer for scheduling

                // Check if start time is in the past
                if (ScheduledStartTime <= now.Add(validationBuffer))
                {
                    LoggingService.LogWarning($"Scheduled Start Time must be at least 2 minutes in the future. Current time: {now:yyyy-MM-dd HH:mm}, Selected: {ScheduledStartTime:yyyy-MM-dd HH:mm}", showMsgBox: true);
                    IsMigrationRunning = false;
                    JobStatus = "Validation Error";
                    return;
                }

                // Check if end time is after start time
                if (ScheduledEndTime <= ScheduledStartTime)
                {
                    LoggingService.LogWarning("Scheduled End Time must be after Start Time.", showMsgBox: true);
                    IsMigrationRunning = false;
                    JobStatus = "Validation Error";
                    return;
                }

                // Check minimum duration (15 minutes)
                var duration = ScheduledEndTime - ScheduledStartTime;
                if (duration.TotalMinutes < 15)
                {
                    LoggingService.LogWarning($"Minimum migration duration is 15 minutes. Current duration: {duration.TotalMinutes:F0} minutes", showMsgBox: true);
                    IsMigrationRunning = false;
                    JobStatus = "Validation Error";
                    return;
                }

                // Check maximum future scheduling limit (30 days)
                var maxFutureLimit = TimeSpan.FromDays(30);
                if (ScheduledStartTime > now.Add(maxFutureLimit))
                {
                    LoggingService.LogWarning($"Migration cannot be scheduled more than 30 days in the future. Selected start time: {ScheduledStartTime:yyyy-MM-dd HH:mm}", showMsgBox: true);
                    IsMigrationRunning = false;
                    JobStatus = "Validation Error";
                    return;
                }

                LoggingService.LogInfo($"Schedule validation passed - Duration: {duration.TotalHours:F2} hours");
            }
            else if (SelectedMigrationOption == MigrationOptionType.Instant)
            {
                LoggingService.LogInfo("Instant Migration selected.");

                if (!int.TryParse(TotalNoOfStudies, out int total))
                {
                    LoggingService.LogError("Invalid number format for TotalNoOfStudies: " + TotalNoOfStudies, null);
                    IsMigrationRunning = false;
                    return;
                }

                var config = new DatabaseConfig();
                var migrationConfig = new MigrationConfig
                {
                    TotalNoOfRecordsToMigrate = total
                };

                if (SelectedCsvType == csvTypes.PatientStudy)
                {
                    config.StudyMigrationProcess = migrationConfig;
                }
                else if (SelectedCsvType == csvTypes.SeriesInstance)
                {
                    config.SeriesMigrationProcess = migrationConfig;
                }

                try
                {
                    await ConfigFileService.SaveDatabaseConfig(config);
                }
                catch (Exception ex)
                {
                    LoggingService.LogError("Failed to save Total Number of Studies to Migrate into config", ex, true);
                    IsMigrationRunning = false;
                    return;
                }
            }


            try
            {
                // Start the migration job
                _currentJobId = await _migrationManager.StartMigrationAsync(
                    SelectedMigrationOption,
                    ScheduledStartTime,
                    ScheduledEndTime,
                    SelectedCsvType);

                if (string.IsNullOrEmpty(_currentJobId))
                {
                    LoggingService.LogError("Failed to start migration job.", null, showMsgBox: true);
                    JobStatus = "Failed";
                    return;
                }

                LoggingService.LogInfo($"Migration job started with ID: {_currentJobId}");
                JobStatus = SelectedMigrationOption == MigrationOptionType.Instant ? "Running" : "Scheduled";

                // For instant migrations, monitor progress
                if (SelectedMigrationOption == MigrationOptionType.Instant)
                {
                    await MonitorJobProgressAsync();
                }
                else
                {
                    LoggingService.LogInfo($"Migration scheduled successfully for {ScheduledStartTime}", showMsgBox: true);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Migration failed: {ex.Message}", ex, showMsgBox: true);
                JobStatus = "Failed";
            }
            finally
            {
                if (SelectedMigrationOption == MigrationOptionType.Instant)
                {
                    IsMigrationRunning = false;
                }
            }
        }

        private async Task MonitorJobProgressAsync()
        {
            int progressStep = 0;
            while (IsMigrationRunning && !string.IsNullOrEmpty(_currentJobId))
            {
                var status = await _migrationManager.GetMigrationStatusAsync(_currentJobId);
                JobStatus = status.ToString();

                switch (status)
                {
                    case Models.JobStatus.Running:
                        // Check if we're in stopping state
                        if (JobStatus.Contains("Stopping") || JobStatus.Contains("Completing") || JobStatus.Contains("Finalizing"))
                        {
                            // Don't update progress during stop operation, maintain current status
                            await Task.Delay(1000);
                        }
                        else
                        {
                            MigrationProgress = Math.Min(progressStep += 5, 90);
                            await Task.Delay(1000);
                        }
                        break;
                    case Models.JobStatus.Completed:
                        MigrationProgress = 100;
                        JobStatus = "Completed";
                        LoggingService.LogInfo("Migration completed successfully!", showMsgBox: true);
                        return;
                    case Models.JobStatus.Failed:
                        JobStatus = "Failed";
                        LoggingService.LogError("Migration job failed.", null, showMsgBox: true);
                        return;
                    case Models.JobStatus.Cancelled:
                        JobStatus = "Stopped";
                        LoggingService.LogInfo("Migration job was cancelled.");
                        return;
                    default:
                        await Task.Delay(1000);
                        break;
                }

                if (!IsMigrationRunning)
                {
                    break;
                }
            }
        }

        private bool CanExecuteStartMigration()
        {
            return !IsMigrationRunning && !HasDatabaseError;
        }

        private async Task ExecuteStopMigrationAsync()
        {
            // Show confirmation dialog
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to stop the migration? This will cancel the current migration.",
                "Confirm Stop Migration?",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                LoggingService.LogInfo("User cancelled the stop migration operation");
                return;
            }

            IsMigrationRunning = false;
            JobStatus = "Stopping...";

            // Show progress message to user
            LoggingService.LogInfo("Cancelling migration gracefully - current batch will complete before shutdown.", showMsgBox: true);

            try
            {
                bool stopped;
                if (!string.IsNullOrEmpty(_currentJobId))
                {
                    JobStatus = "Completing current batch...";
                    await Task.Delay(500); // Brief delay to show status update

                    stopped = await _migrationManager.StopMigrationAsync(_currentJobId);
                    _currentJobId = null;
                }
                else
                {
                    JobStatus = "Finalizing migration...";
                    await Task.Delay(500); // Brief delay to show status update

                    stopped = await _migrationManager.StopMigrationAsync();
                }

                if (stopped)
                {
                    JobStatus = "Stopped";
                }
                else
                {
                    JobStatus = "Stop Failed";
                    LoggingService.LogWarning("Failed to stop migration job.", showMsgBox: true);
                }
            }
            catch (Exception ex)
            {
                JobStatus = "Stop Failed";
                LoggingService.LogError($"Error stopping migration: {ex.Message}", ex, showMsgBox: true);
            }
        }
        private bool CanExecuteStopMigration()
        {
            return IsMigrationRunning && !IsCancelling;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (!Equals(field, newValue))
            {
                field = newValue;
                OnPropertyChanged(propertyName);
                return true;
            }
            return false;
        }

        private static void GetCsvTypes()
        {
            var appSettingsModel = AppSettingsModel.GetInstance;
            csvTypes = appSettingsModel.Settings.CsvTypes;
        }
    }


}
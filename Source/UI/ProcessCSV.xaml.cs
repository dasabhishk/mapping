using System.IO;
using System.Windows;
using System.Windows.Controls;
using CMMT.dao;
using CMMT.Helpers;
using CMMT.Models;
using CMMT.Services;
using CMMT.ViewModels;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Win32;

namespace CMMT.UI
{
    /// <summary>
    /// Interaction logic for ProcessCSV.xaml
    /// </summary>
    public partial class ProcessCSV : Page
    {
        private ProcessCSVService _processCSVService;
        private string _mappingconfigFilePath;
        private string _dbconfigFilePath;
        private string _schedulingconfigFilePath;
        private readonly string _csvDelimiter = AppSettingsModel.GetInstance.Settings.CsvDelimitter;
        List<string>? _validCsvFiles;
        private string _csvType;
        private readonly ITransformationViewService _transformationService;
        private SchedulerConfig _schedulerConfig;
        private MappingConfig _mappingConfig;
        private DatabaseConfig _dbconfig;
        private int selectedArchiveTypeId;
	    private ValidationService validateObj;
        int batchCountFlag = 0;
        public ProcessCSV(ITransformationViewService transformationService, ProcessCSVViewModel processCSVViewModel)
        {
            InitializeComponent();
            _transformationService = transformationService;
            InitializeConfigurations();
            DataContext = processCSVViewModel;
        }

        private async void InitializeConfigurations()
        {
            try
            {
                _schedulingconfigFilePath = ConfigFileHelper.GetConfigFilePath("Configuration", "scheduler.json");
                _schedulerConfig = await ConfigFileHelper.LoadAsync<SchedulerConfig>(_schedulingconfigFilePath);

                _mappingconfigFilePath = ConfigFileHelper.GetConfigFilePath("Configuration", "mapping.json");
                _mappingConfig = await ConfigFileHelper.LoadAsync<MappingConfig>(_mappingconfigFilePath);

                _dbconfigFilePath = ConfigFileHelper.GetConfigFilePath("Configuration", "dbconfig.json");
                _dbconfig = await ConfigFileHelper.LoadAsync<DatabaseConfig>(_dbconfigFilePath);
            }
            catch (FileNotFoundException ex)
            {
                LoggingService.LogError("One of the config file needed for processing the csv files is missing, please check", ex, true);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Exceprion reading the config files", ex, true);
            }

            //Use the DataContext to access the ProcessCSVViewModel instance
            if (DataContext is ProcessCSVViewModel processCSVViewModel)
            {
                selectedArchiveTypeId = processCSVViewModel.SelectedArchiveTypeId;
                if (selectedArchiveTypeId <= 0)
                {
                    btnBrowseCsv.IsEnabled = false;
                }
            }

            btnLoad.IsEnabled = false;
            btnValidate.IsEnabled = false;
            _processCSVService = new ProcessCSVService();
            validateObj = new ValidationService();
        }
        private void CsvTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = cmbCsvType.SelectedItem as ComboBoxItem;
            _csvType = selectedItem?.Content.ToString();

            ResetPage();

            LoggingService.LogInfo($"CSV Type chosen for import is {_csvType}");
        }

        private void BrowseCSVFiles_Click(object sender, RoutedEventArgs e)
        {
            LoggingService.LogInfo("Browse CSV button clicked");
            ResetPage();

            string[] selectedCSVFiles = OpenFileDlg();

            if (selectedCSVFiles == null || selectedCSVFiles.Length == 0)
            {
                LoggingService.LogError("No CSV files selected for import.", null, true);
                return;
            }

            if (IsCsvFileAlreadyProcessed(selectedCSVFiles, out var processedFiles))
            {
                var processedFilesLog = "The following CSV files have already been processed:\n" + string.Join("\n", processedFiles) + "\nIf you would like to still proceed with these files, rename the files to proceed.";
                LoggingService.LogInfo(processedFilesLog, true);
                var unprocessedCsvFiles = new List<string>(selectedCSVFiles);
                unprocessedCsvFiles.RemoveAll(file => processedFiles.Contains(Path.GetFileName(file)));
                selectedCSVFiles = unprocessedCsvFiles.ToArray();
                if (selectedCSVFiles.Length == 0)
                {
                    return;
                }

            }
            txtCsvFiles.Text = selectedCSVFiles.Aggregate((current, next) => current + ";" + next);
            _validCsvFiles = _processCSVService.CsvHeaderValidation(selectedCSVFiles, _csvType, _csvDelimiter, _mappingConfig);
            if (_validCsvFiles != null && _validCsvFiles.Count > 0)
            {
                txtCsvFiles.Text = _validCsvFiles.Aggregate((current, next) => current + ";" + next);
                btnLoad.IsEnabled = true;
            }
            else
            {
                txtCsvFiles.Text = string.Empty;
            }

        }
        private string[] OpenFileDlg()
        {
            LoggingService.LogInfo("Open File Dialog to browse the csv file");
            try
            {
                OpenFileDialog dlg = new OpenFileDialog();
                dlg.Filter = "CSV files (*.csv)|*.csv";
                dlg.Title = "Select a CSV file";
                dlg.Multiselect = true;

                if (dlg.ShowDialog() == true)
                {
                    return dlg.FileNames;
                }
                else
                {
                    return Array.Empty<string>();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error opening file dialog", ex,true);
                return Array.Empty<string>();
            }
        }
        private async void LoadCsv_Click(object sender, RoutedEventArgs e)
        {
            btnBrowseCsv.IsEnabled = false;
            
            progressBar.Visibility = Visibility.Visible;
            progressText.Visibility = Visibility.Visible;
            btnLoad.IsEnabled = false;

            if (_validCsvFiles.IsNullOrEmpty())
            {
                LoggingService.LogWarning("No valid patient or series csv files selected and hence no csv data to load and validate.", true);
                progressBar.Visibility = Visibility.Collapsed;
                progressText.Visibility = Visibility.Collapsed;
                return;
            }

            var progress = new Progress<(int percent, string message)>(tuple =>
            {
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = tuple.percent;
                    progressText.Text = tuple.message;
                });
            });

            /* code to get the dbconfig.json and make the DB connection and check if staging DB exist */
            var plainConnStr = _dbconfig.StagingDatabase.EncryptedConnectionString;
            var decryptedConnStr = SecureStringHelper.Decrypt(plainConnStr);
            var dbLayer = new DBLayer(decryptedConnStr);

            if (!dbLayer.Connect(false))
            {
                LoggingService.LogError("Failed to connect to Staging DB please check the database configuration data", null, true);
                return;
            }

            try
            {
                var job = _schedulerConfig.Jobs.FirstOrDefault(j => j.Name == "ImportJob" && j.Enabled);
                int batchSize = job.BatchSize;

                if (DataContext is ProcessCSVViewModel processCSVViewModel)
                {
                    selectedArchiveTypeId = processCSVViewModel.SelectedArchiveTypeId;
                }

                await Task.Run(async () =>
                {
                    foreach (var map in _mappingConfig.Mappings)
                    {
                        if (_validCsvFiles != null && _validCsvFiles.Count > 0 && _csvType == map.CsvType)
                        {
                            batchCountFlag = await _processCSVService.ProcessCsvData(_validCsvFiles, map.TableName, _mappingConfig, _transformationService,
                                _csvDelimiter, _dbconfig, batchSize, _csvType, dbLayer, selectedArchiveTypeId, progress);
                            break;
                        }
                    }
                });

                if (batchCountFlag == 100)
                {
                    Dispatcher.Invoke(() => btnValidate.IsEnabled = true); //after completion of load enabling validate button

                }
                else
                {
                    Dispatcher.Invoke(() => btnValidate.IsEnabled = false);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error processing CSV files and hence not loaded into staging DB", ex, true);

            }
            finally
            {
                progressBar.Visibility = Visibility.Collapsed;
                progressText.Visibility = Visibility.Collapsed;
                btnBrowseCsv.IsEnabled = true;
                btnLoad.IsEnabled = false;
                dbLayer.Disconnect();
            }
        }

        public void ResetPage()
        {
            LoggingService.LogInfo("Resetting the ProcessCSV page");
            if(txtCsvFiles != null)
            {
                txtCsvFiles.Text = "";
            }
            if(btnLoad != null)
            {
                btnLoad.IsEnabled = false;
            }
            _validCsvFiles?.Clear();
        }

        private async void ValidateCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                progressBar.Visibility = Visibility.Visible;
                progressText.Visibility = Visibility.Visible;
                btnValidate.IsEnabled = false;
                btnBrowseCsv.IsEnabled = false;

                var progress = new Progress<(int percent, string message)>(tuple =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        progressBar.Value = tuple.percent;
                        progressText.Text = tuple.message;
                    });
                });

                /* code to get the dbconfig.json and make the DB connection and check if staging DB exist */
                var plainConnStr = _dbconfig.StagingDatabase.EncryptedConnectionString;
                var decryptedConnStr = SecureStringHelper.Decrypt(plainConnStr);
                var dbLayer = new DBLayer(decryptedConnStr);

                if (!dbLayer.Connect(false))
                {
                    LoggingService.LogError("Failed to connect to Staging DB please check the database configuration data", null, true);
                    return;
                }
                int status = await Task.Run(() => validateObj.RunValidation(dbLayer, progress));
                if (status == 0)
                {
                    LoggingService.LogInfo("Data validation is performed successfully.", true);
                }
                else
                {
                    LoggingService.LogWarning("Data validation was unsuccessful", true);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Data validation was unsuccessful", ex, true);
            }
            finally
            {
                progressBar.Value = 0; // reset to 0% so it's gray
                btnValidate.IsEnabled = false;
                btnBrowseCsv.IsEnabled = true;
                progressBar.Visibility = Visibility.Collapsed;
                progressText.Visibility = Visibility.Collapsed;
            }
        }
        private bool IsCsvFileAlreadyProcessed(string[] selectedFiles, out List<string> processedFiles)
        {
            var selectedFileNames = GetSelectedFileNames(selectedFiles);
            DatabaseConfig dbConfig = JsonHelper.LoadSynchronously<DatabaseConfig>(ConfigFileService.ConfigPath);
            var decryptedConnStr = SecureStringHelper.Decrypt(dbConfig.StagingDatabase.EncryptedConnectionString);
            var dbLayer = new DBLayer(decryptedConnStr);
            DatabaseService databaseService = new DatabaseService(dbLayer);
            processedFiles = databaseService.GetProcessedFiles(selectedFileNames);
            return processedFiles.Any();
        }

        private List<string> GetSelectedFileNames(string[] selectedFiles)
        {
            var fileNames = new List<string>();
            foreach (var selectedFile in selectedFiles)
            {
                string fileName = Path.GetFileName(selectedFile);
                fileNames.Add(fileName);
            }
            return fileNames;
        }
    }
}

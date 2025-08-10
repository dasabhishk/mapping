using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using CMMT.Helpers;
using CMMT.Models;
using CMMT.Services;

namespace CMMT.ViewModels
{
    public class ProcessCSVViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ArchiveType> ArchiveTypes { get; } = new();

        private int _selectedArchiveTypeId;
        public int SelectedArchiveTypeId
        {
            get => _selectedArchiveTypeId;
            set
            {
                _selectedArchiveTypeId = value;
                OnPropertyChanged();
            }
        }
        public ProcessCSVViewModel()
        {
            LoadArchiveTypesFromDatabase();
        }

        public async void LoadArchiveTypesFromDatabase()
        {

            try
            {
                var _dbconfigFilePath = ConfigFileHelper.GetConfigFilePath("Configuration", "dbconfig.json");
                var _dbconfig = await ConfigFileHelper.LoadAsync<DatabaseConfig>(_dbconfigFilePath);
                var plainConnStr = _dbconfig.TargetDatabase.EncryptedConnectionString;

                var results = DatabaseService.LoadArchiveTypes(plainConnStr);

                ArchiveTypes.Clear();
                foreach (var item in results)
                    ArchiveTypes.Add(item);

                // Optionally set default selected value
                if (ArchiveTypes.Any())
                    SelectedArchiveTypeId = ArchiveTypes.First().StorageLocDBKey;
            }
            catch (FileNotFoundException ex)
            {
                LoggingService.LogError("DB config file needed for connecting to target is missing, please check", ex, true);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("Exception reading the config files", ex, true);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

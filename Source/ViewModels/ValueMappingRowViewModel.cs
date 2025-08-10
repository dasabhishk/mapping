using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CMMT.ViewModels
{
    public partial class ValueMappingRowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? sourceValue; // csv value

        [ObservableProperty]
        private string? targetValue; // db value user maps

        private ObservableCollection<string> filteredSources;

        public ValueMappingDialogViewModel? ParentViewModel { get; set; } // reference to parent dialog view model for validation

        public ObservableCollection<string> FilteredSources
        {
            get => filteredSources;
            set => SetProperty(ref filteredSources, value);
        }

        public ValueMappingRowViewModel()
        {
            filteredSources = new ObservableCollection<string>();
        }

        partial void OnSourceValueChanged(string? oldValue, string? newValue)
        {
            // Trigger the necessary behavior by raising the property changed event for CanAddRow
            ParentViewModel?.CanAddRowChanged(); // Notify parent view model that the ability to add a row may have changed
        }
    }
}

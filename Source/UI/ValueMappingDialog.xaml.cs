using System.Windows;
using CMMT.Services;
using CMMT.ViewModels;

namespace CMMT.UI
{
    /// <summary>
    /// Interaction logic for ValueMappingDialog.xaml
    /// </summary>
    public partial class ValueMappingDialog : Window
    {
        public ValueMappingDialog(ValueMappingDialogViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TargetComboBox.Focus(); // Ensure it's focusable
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ValueMappingDialogViewModel;
            if (vm != null && vm.Validate())
            {
                DialogResult = true; // Close dialog with success
            }
            else
            {
                LoggingService.LogError(vm?.ValidationMessage ?? "Validation failed.", null, true);
            }
        }
    }
}

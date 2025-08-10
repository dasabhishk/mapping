using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CMMT.Services;
using CMMT.ViewModels;
namespace CMMT.UI
{
    public partial class Migration : Page
    {
        private static readonly Regex _numericRegex = new Regex("^[1-9][0-9]*$");
        private bool _isUserTriggered = false;
        private string _csvType;
        public Migration(MigrationViewModel viewModel)
        {
            InitializeComponent();
            OnLoad();
            DataContext = viewModel;
        }

        private void OnLoad()
        {
            if ((bool)rbtnInstantMigration.IsChecked)
            {
                btnStartMigration.IsEnabled = false;
            }
        }

        private void Radio_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isUserTriggered = true;
        }

        private void Radio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUserTriggered)
            {
                var radio = sender as RadioButton;
                if (rbtnScheduledMigration != null && rbtnScheduledMigration.IsChecked == true)
                    btnStartMigration.IsEnabled = true;
                else if (rbtnInstantMigration != null && rbtnInstantMigration.IsChecked == true)
                    btnStartMigration.IsEnabled = false;
            }

            _isUserTriggered = false;
        }

        private void TotalNoOfStudies_KeyUp(object sender, KeyEventArgs e)
        {
            string text = txtTotalNoOfStudies.Text.Trim();

            if (string.IsNullOrWhiteSpace(text) || !_numericRegex.IsMatch(text))
            {
                txtWarningTextBlock.Visibility = Visibility.Visible;
                EnableDisableControls(false);
            }
            else
            {
                txtWarningTextBlock.Visibility = Visibility.Collapsed;
                EnableDisableControls(true);
            }
        }

        private void EnableDisableControls(bool flag)
        {
            btnStartMigration.IsEnabled = flag;
            btnStopMigration.IsEnabled = flag;
        }
    }
}
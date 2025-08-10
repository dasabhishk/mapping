using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.InteropServices.Marshalling;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CMMT.dao;
using CMMT.Helpers;
using CMMT.Models;
using CMMT.Services;

namespace CMMT.UI;

public partial class ReportingPage : Page
{
    private DateTime? _lastStartDate = null;
    private DateTime? _lastEndDate = null;
    public ObservableCollection<MigrationReportRow> Reports { get; } = new();

    public ICommand ExportCommand { get; }
    public ICommand ExportErrorRept { get; }

    public ReportingPage()
    {
        InitializeComponent();
        DataContext = this;

        ExportCommand = new RelayCommand(p => ExportFileService.ExportFileReport(
            p as MigrationReportRow,
            (p as MigrationReportRow)?.InvalidRowsTable,
            (p as MigrationReportRow)?.InvalidRows ?? 0,
            "InvalidRows",
            "InvalidRows"
        ));

        ExportErrorRept = new RelayCommand(p => ExportFileService.ExportFileReport(
            p as MigrationReportRow,
            (p as MigrationReportRow)?.ErrorRowsTable,
            (p as MigrationReportRow)?.ErrorRows ?? 0,
            "ErrorRows",
            "ErrorRows"
        ));

        dpStart.SelectedDate = DateTime.Today;
        dpEnd.SelectedDate = DateTime.Today.AddDays(1);
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (dpStart.SelectedDate is DateTime start && dpEnd.SelectedDate is DateTime end)
        {
            if (start > end)
            {
                LoggingService.LogWarning("Start date is after end date. Please select a valid date range.", true);
                return;
            }
            await LoadReportsAsync(start.Date, end.Date);
        }
        else
        {
            LoggingService.LogWarning("Start or end date is not selected. Please select both dates.", true);
        }
    }

    private async Task LoadReportsAsync(DateTime start, DateTime end)
    {
        _lastStartDate = start;
        _lastEndDate = end;
        var rows = await FetchReportsAsync(start, end);

        if (rows == null)
        {
            return;
        }

        if (!rows.Any())
        {
            if (Reports.Any())
            {
                Reports.Clear();
            }
            LoggingService.LogInfo("No records found in the selected date range.", true);
            return;
        }

        Reports.Clear();
        foreach (var r in rows) Reports.Add(r);
    }

    private static async Task<IEnumerable<MigrationReportRow>?> FetchReportsAsync(DateTime start, DateTime end)
    {
        try
        {
            var dbConfig = await ConfigFileHelper.LoadAsync<DatabaseConfig>(ConfigFileService.ConfigPath);
            if (dbConfig?.StagingDatabase?.EncryptedConnectionString == null)
                throw new InvalidOperationException("Missing encrypted connection string.");

            var connStr = SecureStringHelper.Decrypt(dbConfig.StagingDatabase.EncryptedConnectionString);
            var dbLayer = new DBLayer(connStr);

            if (!dbLayer.Connect(false))
            {
                LoggingService.LogError("Failed to connect to Staging DB please check the database configuration data", null, true);
            }

            var rows = new List<MigrationReportRow>();

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var filenames = await ReportDataService.GetDistinctFileNamesAsync(dbLayer, start, end);

            foreach (var file in filenames)
            {
                var (valid, invalid, duplicated, migrated, unknown) = await ReportDataService.GetStatusCountsAsync(dbLayer, file, start, end);
                int errors = await ReportDataService.GetErrorCountAsync(dbLayer, file, start, end);
                var invalidTable = await ReportDataService.GetInvalidRowsTableAsync(dbLayer, file, start, end);
                var errorTable = await ReportDataService.GetErrorRowsTableAsync(dbLayer, file, start, end);

                rows.Add(new MigrationReportRow
                {
                    FileName = file,
                    TotalRows = valid + invalid + duplicated + migrated + unknown + errors,
                    ValidRows = valid,
                    InvalidRows = invalid + duplicated,
                    MigratedRows = migrated,
                    ErrorRows = errors,
                    InvalidRowsTable = invalidTable,
                    ErrorRowsTable = errorTable
                });
            }

            return rows;
        }
        catch (Exception ex)
        {
            LoggingService.LogError("Error fetching migration reports, check if staging db is configured", ex, true);
            return null;
        }
    }
}

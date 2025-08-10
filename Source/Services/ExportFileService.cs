using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CMMT.Models;
using Microsoft.Win32;
using System.Windows;
using System.Data;

namespace CMMT.Services
{
    class ExportFileService
    {
        public static void ExportFileReport(MigrationReportRow? row, DataTable? data, int count, string sheetName, string suffix)
        {
            if (row is null || data is null || count == 0)
            {
                LoggingService.LogWarning($"No {suffix} found for file: {row?.FileName ?? "Unknown"}", true);
                return;
            }

            var save = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = System.IO.Path.GetFileNameWithoutExtension(row.FileName) + $"_{suffix}.xlsx"
            };
            if (save.ShowDialog() != true) return;

            try
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add(data, sheetName);
                ws.Columns().AdjustToContents();
                wb.SaveAs(save.FileName);
                LoggingService.LogInfo($"Export Successful: Saved {save.FileName}", true);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Export failed for file: {row.FileName}", ex, true);
            }
        }
    }
}

using System.Data;
using System.IO;
using CMMT.dao;
using CMMT.Helpers;
using CMMT.Models;

namespace CMMT.Services
{
    public class ProcessCSVService
    {
        Dictionary<string, int> _csvFilesProcessed;
        private ImportManager _importManager = new ImportManager();
        public List<string>? CsvHeaderValidation(string[] selectedFiles, string fileType, string _csvDelimiter, MappingConfig mappingConfig)
        {
            LoggingService.LogInfo("Validate the CSV header");
            
            List<string> validHeaderSelectedFiles = selectedFiles.ToList<string>();
            string headerValidationFailedFiles = "";
            foreach (string filePath in selectedFiles)
            {
                string fileName = Path.GetFileName(filePath);

                var mapping = mappingConfig.Mappings.FirstOrDefault(m => m.CsvType.Equals(fileType, StringComparison.OrdinalIgnoreCase));

                if (mapping == null)
                {
                    LoggingService.LogError($"{fileType} - No mapping found for the file {fileName}", null, false);
                    headerValidationFailedFiles += fileName + "; ";
                    validHeaderSelectedFiles.Remove(filePath);
                    continue;
                }

                var expectedHeaders = mapping.ColumnMappings.Select(c => c.CsvColumn).ToHashSet();

                try
                {
                    using (var reader = new StreamReader(filePath))
                    {
                        string? headerLine = reader.ReadLine();
                        if (headerLine == null)
                        {
                            LoggingService.LogError($"{fileName} - Empty file.", null, true);
                            continue;
                        }
                        var actualHeaders = headerLine.Split(_csvDelimiter).Select(h => h.Trim()).ToHashSet();
                        var missingHeaders = expectedHeaders.Except(actualHeaders).ToList();
                        if (missingHeaders.Count > 0)
                        {
                            LoggingService.LogError($"{fileName} - Headers in CSV do not match with the table columns in mapping configuration.", null, false);
                            headerValidationFailedFiles += fileName + "; ";
                            validHeaderSelectedFiles.Remove(filePath);
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"Error reading file {fileName}", ex, true);
                    validHeaderSelectedFiles.Remove(filePath);
                }
            }
            if(!string.IsNullOrWhiteSpace(headerValidationFailedFiles))
            {
                LoggingService.LogError($" Mapping not found or Header in CSV do not match with mapping config for these files - {headerValidationFailedFiles}", null, true);
            }
            return validHeaderSelectedFiles;
        }

        public async Task<int> ProcessCsvData(List<string> validCsvFiles, string tablename, MappingConfig mappingConfig,
            ITransformationViewService _transformationService, string _csvDelimiter, DatabaseConfig _dbconfig,  int batchSize, string _csvType, DBLayer dBLayer,
            int selectedArchiveTypeId, IProgress<(int percent, string message)>? progress = null)
        {
            int totalFiles = validCsvFiles.Count;
            int fileIndex = 0;
            int batchRowCount = 0;
            progress?.Report((0, "Starting to load the csv file..."));
            try
            {
                DataTable auditLogTable = CreateAuditLogDataTable();
                _csvFilesProcessed = new Dictionary<string, int>();

                // Write to SQL by reading the connection string in dbconfig.json
                var plainConnStr = _dbconfig.StagingDatabase.EncryptedConnectionString;
                string decryptedConnStr = SecureStringHelper.Decrypt(plainConnStr);

                foreach (var filepath in validCsvFiles)
                {
                   fileIndex++;
                   DataTable csvToDataTable = await _importManager.ConvertCSVToDatatable(filepath, mappingConfig, _csvDelimiter, tablename, _transformationService, selectedArchiveTypeId, progress);

                   if(csvToDataTable != null && csvToDataTable.Rows.Count > 0)
                   {
                        _csvFilesProcessed.Add(Path.GetFileName(filepath), csvToDataTable.Rows.Count);

                        var DataBatches = SplitDataTable(csvToDataTable, batchSize);
                        try
                        {
                            int totalBatches = DataBatches.Count;
                            int batchIndex = 0;
                            var appSettingsModel = AppSettingsModel.GetInstance;
                            await Parallel.ForEachAsync(DataBatches, new ParallelOptions { MaxDegreeOfParallelism = (int)appSettingsModel.Settings.MaxParallel }, async (batch, _) =>
                            {
                                batchIndex++;
                                batchRowCount = CalculatePercent(fileIndex - 1 + (double)batchIndex / totalBatches, totalFiles);
                                progress?.Report((batchRowCount, $"Loading into staging database -  batch {batchIndex} of {totalBatches} for file {Path.GetFileName(filepath)}"));
                                var (successCount, errorCount) = await BulkCopyService.SaveDataToStaging(batch, mappingConfig, decryptedConnStr, tablename);

                                lock (auditLogTable)
                                {
                                    auditLogTable.Rows.Add(
                                        Path.GetFileName(filepath),
                                        DateTime.UtcNow,
                                        batch.Rows.Count,
                                        successCount,
                                        errorCount
                                    );
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            LoggingService.LogError("Error occurred while processing of csv records.", ex, true);
                            progress?.Report((0, "Error occured during loading / processing of csv."));
                            return batchRowCount = 0;
                        }
                   }
                }

                // Save audit log to the database
                await BulkCopyService.SaveAuditLogToDB(decryptedConnStr, auditLogTable);
                if (batchRowCount == 100)
                {
                    progress?.Report((100, "Loading records into staging DB is completed successfully."));
                    LoggingService.LogInfo("Loading records into staging DB is completed successfully.", true);
                    return batchRowCount;
                }
                else
                {
                    progress?.Report((0, "Error loading the records into the staging DB."));
                    LoggingService.LogWarning("Data Loading was unsuccessful into staging DB", true);
                    return batchRowCount = 0;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error processing {tablename} data", ex, true);
                progress?.Report((0, "Error occured during loading / processing of csv."));
                return batchRowCount = 0;
            }
        }

        public static List<DataTable> SplitDataTable(DataTable sourceTable, int batchSize)
        {
            var batches = new List<DataTable>();
            int totalRows = sourceTable.Rows.Count;

            for (int i = 0; i < totalRows; i += batchSize)
            {
                // Create a new DataTable with the same structure
                var batchTable = sourceTable.Clone();

                // Import rows into the batch
                for (int j = i; j < i + batchSize && j < totalRows; j++)
                {
                    batchTable.ImportRow(sourceTable.Rows[j]);
                }

                batches.Add(batchTable);
            }

            return batches;
        }
        public Dictionary<string, int> ValidCsvFiles()
        {
            return _csvFilesProcessed;
        }

        public DataTable CreateAuditLogDataTable()
        {
            DataTable auditLogTable = new DataTable();
            auditLogTable.Columns.Add("FileName", typeof(string));
            auditLogTable.Columns.Add("CompletedAt", typeof(DateTime));
            auditLogTable.Columns.Add("TotalRowsCount", typeof(int));
            auditLogTable.Columns.Add("SucessRowsCount", typeof(int));
            auditLogTable.Columns.Add("ErrorRowsCount", typeof(int));

            return auditLogTable;
        }

        private int CalculatePercent(double current, int total)
        {
            if (total == 0) return 0;
            return (int)(current / total * 100);
        }
    }
}

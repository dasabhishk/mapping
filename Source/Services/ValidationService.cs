using CMMT.dao;
using CMMT.DataStructures;
using CMMT.Helpers;
using CMMT.Models;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text.Json;

namespace CMMT.Services
{
    internal class ValidationService
    {
        int returnStatusCode = -1;
        private string pathForStoredProcedure;
        private string readTimeoutValue;
        int setTimeOut = 0;
        public async Task<int> RunValidation( DBLayer db, IProgress<(int percent, string message)>? progress = null)
        {
            LoggingService.LogInfo("Validate button clicked");
            try
            {
                progress?.Report((0, "Starting to validate the records in staging DB..."));
                int done = 0;
                int percentageCompleted = 0;
                int totalSteps = 0;
                bool isValidationServiceCall = true;
                LoggingService.LogInfo($"Validation service invoked", false);
                pathForStoredProcedure = ConfigFileHelper.GetConfigFilePath("Configuration", "SQLProcedure.json");

                // Reading AppSettings.json file to get timeout value
                readTimeoutValue = ConfigFileHelper.GetConfigFilePath("Configuration", "AppSettings.json");
                var timeOutScript = await File.ReadAllTextAsync(readTimeoutValue); //Reading AppSettings.json file
                var appConfig = JsonSerializer.Deserialize<AppSettings>(timeOutScript)
                    ?? throw new InvalidOperationException("Failed to parse AppSettings.json");
                setTimeOut = appConfig.TimeOutCount;

                // Set the command timeout for the DBLayer instance
                db.SetCommandTimeout(setTimeOut);

                /* checking if there are any rows present in series and study table
                 *  if no rows present, then unnecessary overhead of executing procedure
                 * will be prevented */

                var countRowsStudyTable = Convert.ToInt32(
                    db.ExecuteScalar_Query(
                       @"SELECT COUNT(*)
                      FROM [cmmt].[cmmt_PatientStudyMetaData]
                      WHERE Status IS NULL
                      OR Status = ''"
                    ));

                var countRowsSeriesTable = Convert.ToInt32(
                    db.ExecuteScalar_Query(
                       @"SELECT COUNT(*)
                      FROM [cmmt].[cmmt_PatientStudySeriesData]
                      WHERE Status IS NULL
                      OR Status = ''"
                    ));

                if (countRowsStudyTable == 0 && countRowsSeriesTable == 0)  //both tables empty
                {
                    LoggingService.LogInfo($"No records for validation", true);
                    progress?.Report((0, "No records for validation"));
                    return returnStatusCode = -1;
                }


                // Load JSON config of all .sql scripts

                if (!File.Exists(pathForStoredProcedure))
                {
                    LoggingService.LogError($"Procedure JSON not found at '{pathForStoredProcedure}'", null);
                    progress?.Report((0, "Something went wrong when loading configuration..."));
                    return returnStatusCode = -1;
                }
                var sqlScripts = await File.ReadAllTextAsync(pathForStoredProcedure); //Reading all the scripts
                var SQLConfig = JsonSerializer.Deserialize<SqlProcedureConfig>(sqlScripts)
                    ?? throw new InvalidOperationException("Failed to parse SQLProcedure.json");

                var allFiles = SQLConfig.Procedures.Keys.Take(6);
                var procedureWithoutExtension = allFiles // all files without extension
                    .Select(Path.GetFileNameWithoutExtension)
                    .ToList();

                //checking if procedures already exists, if not, then deploy the procedures

                int existingCount = CheckAndDeployProcedures(db, procedureWithoutExtension);

                if (existingCount == -1)
                {
                    LoggingService.LogError("Error checking existing procedures, cannot proceed with validation.", null);
                    return returnStatusCode = -1;
                }

                if (existingCount < procedureWithoutExtension.Count)
                {
                    LoggingService.LogInfo($"Some procedures are missing, deploying all procedures first", false);
                    await DatabaseService.ProcedureCreationFromJson(db, pathForStoredProcedure, SQLConfig, isValidationServiceCall);
                }

                //adding input and output parameters when calling stored procedure
                var parms = new DBParameters();
                parms.Add("@ReturnStatus", 0, ParameterDirection.Output, SqlDbType.Int);

                if (countRowsStudyTable != 0 && countRowsSeriesTable != 0)
                {
                    LoggingService.LogInfo($"Records present in both series and study table", false);
                    var proceduresToExecuteForStudy = new List<string>{
                        procedureWithoutExtension[0], // sp_MarkPatientStudyDuplicates
                        procedureWithoutExtension[1], // sp_ImportValidationForPatient_UpdateDemographicInvalid
                        procedureWithoutExtension[2], //sp_ImportValidationForPatient_UpdateInvalidNullCheck
                        procedureWithoutExtension[3]  //sp_ImportValidationForPatient_UpdateValid
                    };

                    var proceduresToExecuteForSeries = new List<string>{
                        procedureWithoutExtension[4], // sp_ImportValidationForSeries_UpdateInvalid
                        procedureWithoutExtension[5]  // sp_ImportValidationForSeries_UpdateValid
                    };

                    var procedureResults = new Dictionary<string, object>();

                    //Patient Study Procedures
                    foreach (var procedure in proceduresToExecuteForStudy)
                    {
                        object returnObj = null;
                        await Task.Run(() =>
                        {
                            db.Execute_SP(
                                "cmmt." + procedure,
                                parms,
                                out returnObj
                            );
                        });
                        procedureResults[procedure] = returnObj ?? throw new InvalidOperationException($"Procedure '{procedure}' returned a null value.");

                        if (Convert.ToInt32(returnObj) != 0)
                        {
                            LoggingService.LogError($"Stored procedure {procedure} failed with status = {returnObj}", null);
                            progress?.Report((0, $"Validation was unsuccessful for Patient Study"));
                            return returnStatusCode = -2; // Indicating failure
                        }
                        else
                        {
                            done++;
                            totalSteps = 6;  //4 stored procedures to be executed for patient study +  2 procedures for series instance
                            percentageCompleted = (done * 100) / totalSteps; //percetage of completion after each stored procedure gets executed
                            progress?.Report((percentageCompleted, $"Validating records {percentageCompleted}% done..."));
                            LoggingService.LogInfo($"Validating records in both series and study table - STUDY", false);
                            returnStatusCode = 0;

                        }
                    }
                    //Series Procedures
                    foreach (var procedure in proceduresToExecuteForSeries)
                    {
                        object returnObj = null;
                        await Task.Run(() =>
                        {
                            db.Execute_SP(
                                "cmmt." + procedure,
                                parms,
                                out returnObj
                            );
                        });
                        procedureResults[procedure] = returnObj ?? throw new InvalidOperationException($"Procedure '{procedure}' returned a null value.");
                        if (Convert.ToInt32(returnObj) != 0)
                        {
                            LoggingService.LogError($"Stored procedure {procedure} failed with status = {returnObj}", null);
                            progress?.Report((0, $"Validation was unsuccessful for Patient Study Series"));
                            return returnStatusCode = -2; // Indicating failure
                        }
                        else
                        {
                            done++;
                            totalSteps = 6; //6 stored procedures to be executed in total
                            percentageCompleted = (done * 100) / totalSteps;
                            progress?.Report((percentageCompleted, $"Validating records {percentageCompleted}% done...")); //84%
                            LoggingService.LogInfo($"Validating records for both series and study table - SERIES", false);
                            returnStatusCode = 0;
                        }
                    }
                    if(done == totalSteps)  //checking if 6 procedures have been executed successfully
                    {
                        progress?.Report((100, $"Validation complete 100% done..."));
                        LoggingService.LogInfo($"Validation complete 100% done for both series and study", false);
                    }
                }

                else if (countRowsStudyTable != 0 && countRowsSeriesTable == 0)
                {
                    // Only Patient Study Procedures
                    var proceduresToExecuteForStudy = new List<string>{
                        procedureWithoutExtension[0], // sp_MarkPatientStudyDuplicates
                        procedureWithoutExtension[1], // sp_ImportValidationForPatient
                        procedureWithoutExtension[2],
                        procedureWithoutExtension[3]
                    };
                    foreach (var procedure in proceduresToExecuteForStudy)
                    {
                        object returnObj = null;
                        await Task.Run(() =>
                        {
                            db.Execute_SP(
                                "cmmt." + procedure,
                                parms,
                                out returnObj
                            );
                        });
                        if (Convert.ToInt32(returnObj) != 0)
                        {
                            LoggingService.LogError($"Stored procedure {procedure} failed with status = {returnObj}", null);
                            progress?.Report((0, $"Validation was unsuccessful for Patient Study"));
                            return returnStatusCode = -2; // Indicating failure
                        }
                        else
                        {
                            done++; //4 stored procedure for patient study data
                            totalSteps = 4;
                            percentageCompleted = (done * 100) / totalSteps;
                            progress?.Report((percentageCompleted, $"Validating records {percentageCompleted}% done..."));
                            LoggingService.LogInfo($"Validating records for study and no records in series table", false);
                            returnStatusCode = 0;
                        }
                    }
                    if (done == 4)
                    {
                        progress?.Report((100, $"Validation complete 100% done..."));
                        LoggingService.LogInfo($"Validation completed for study and no records in series table", false);
                    }
                }

                else if(countRowsStudyTable == 0 && countRowsSeriesTable != 0)
                {
                    // Only Patient Series Procedures
                    var proceduresToExecuteForSeries = new List<string>{
                        procedureWithoutExtension[4], // sp_ImportValidationForSeries
                        procedureWithoutExtension[5]  // sp_MarkPatientStudySeriesDuplicates
                    };
                    foreach (var procedure in proceduresToExecuteForSeries)
                    {
                        object returnObj = null;
                        await Task.Run(() =>
                        {
                            db.Execute_SP(
                                "cmmt." + procedure,
                                parms,
                                out returnObj
                            );
                        });
                        if (Convert.ToInt32(returnObj) != 0)
                        {
                            LoggingService.LogError($"Stored procedure {procedure} failed with status = {returnObj}", null);
                            progress?.Report((0, $"Validation was unsuccessful for Patient Study Series"));
                            return returnStatusCode = -2; // Indicating failure
                        }
                        else
                        {
                            done++; //2 stored procedures for patient study series data
                            totalSteps = 2;
                            percentageCompleted = (done * 100) / totalSteps;
                            progress?.Report((percentageCompleted, $"Validating records {percentageCompleted}% done..."));
                            LoggingService.LogInfo($"Validating records for series and no records in study table", false);
                            returnStatusCode = 0;
                        }
                    }
                    if (done == 2)
                    {
                        progress?.Report((100, $"Validation complete 100% done..."));
                        LoggingService.LogInfo($"Validation completed for series and no records in study table", false);

                    }
                }
            }

            catch (SqlException sqlEx)
            {
                LoggingService.LogError($"SQL error executing validation procedure: {sqlEx.Message}", sqlEx);
                progress?.Report((0, "Error when validating records"));
                returnStatusCode = -3;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Unexpected error executing validation procedure: {ex.Message}", ex);
                progress?.Report((0, "Unexpected error when executing validation procedure"));
                returnStatusCode = -3;
            }
            finally
            {
                //Disconnect from db
                db.Disconnect();
            }
            return returnStatusCode;
        }

        internal static int CheckAndDeployProcedures(DBLayer db, List<string> procedureWithoutExtension)
        {
            var existingCount = -1;
            object result = null;
            try
            {
                // Check how many procedures already exist
                var procedureNames = string.Join("','", procedureWithoutExtension);
                result = db.ExecuteScalar_Query(
                    $"SELECT COUNT(*) FROM sys.procedures " +
                    $"WHERE SCHEMA_NAME(schema_id) = 'cmmt' " +
                    $"AND name IN ('{procedureNames}')"
                );
                if(result != null && result != DBNull.Value)
                {
                    existingCount = Convert.ToInt32(result);
                }
                else
                {
                    LoggingService.LogError("Unexpected NULL/DBNULL from COUNT(*) query", null);
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Unexpected error when executing query for checking procedures: {ex.Message}", ex);
                return existingCount = -1;
            }

            return existingCount;
        }
    }
}

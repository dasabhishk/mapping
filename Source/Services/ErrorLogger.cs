using System;
using System.CodeDom;
using System.Data;
using CMMT.dao; // Adjust namespace as needed

namespace CMMT.Services
{
    public class ErrorLogger
    {
        private readonly DBLayer _dbLayer;

        public ErrorLogger(DBLayer dbLayer)
        {
            _dbLayer = dbLayer;
        }

        /// <summary>
        /// Logs an error to the [cmmt].[cmmt_ErrorLog] table.
        /// </summary>
        /// <param name="fileName">The file name related to the error.</param>
        /// <param name="rowNumber">The row number related to the error.</param>
        /// <param name="errorMessage">The error message.</param>
        public void LogError(string fileName, int rowNumber, string errorMessage)
        {
            _dbLayer.Connect(false);
            try
            {
                string sql = @"
                    INSERT INTO [cmmt].[cmmt_ErrorLog] 
                        (FileName, RowNumber, ErrorMessage)
                    VALUES 
                        (@FileName, @RowNumber, @ErrorMessage)";

                using var parameters = new DBParameters();
                parameters.Add("@FileName", fileName ?? string.Empty, ParameterDirection.Input, SqlDbType.NVarChar, 255);
                parameters.Add("@RowNumber", rowNumber, ParameterDirection.Input, SqlDbType.Int);
                parameters.Add("@ErrorMessage", errorMessage ?? string.Empty, ParameterDirection.Input, SqlDbType.NVarChar, 4000);
                _dbLayer.ExecuteQuery_WithParams<int>(sql, parameters);

            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error while inserting into the ErrorLog table", ex);
                throw;
            }
            finally
            {
                _dbLayer.Disconnect();
            }
        }
    }
}
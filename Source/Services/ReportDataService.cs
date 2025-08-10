using System.Data;
using CMMT.dao;
using CMMT.Helpers;
using CMMT.Models;

namespace CMMT.Services;

public static class ReportDataService
{
    public static async Task<HashSet<string>> GetDistinctFileNamesAsync(DBLayer dbLayer, DateTime start, DateTime end)
    {
        var filenames = new HashSet<string>();
        string query = @"
            SELECT DISTINCT FileName FROM cmmt.cmmt_PatientStudyMetaData
            WHERE CreatedAt BETWEEN @start AND @end
            UNION 
            SELECT DISTINCT FileName FROM cmmt.cmmt_PatientStudySeriesData
            WHERE CreatedAt BETWEEN @start AND @end";

        var parameters = new DBParameters();
        parameters.Add("@start", start, ParameterDirection.Input, SqlDbType.DateTime);
        parameters.Add("@end", end, ParameterDirection.Input, SqlDbType.DateTime);

        using var reader = await dbLayer.ExecuteReader_QueryWithParamsAsync(query, parameters);
        while (await reader.ReadAsync())
        {
            filenames.Add(reader.GetString(0));
        }
        return filenames;
    }

    public static async Task<(int valid, int invalid, int duplicated, int migrated, int unknown)> GetStatusCountsAsync(DBLayer dbLayer, string file, DateTime start, DateTime end)
    {
        int valid = 0, invalid = 0, duplicated = 0, migrated = 0, unknown = 0;

        string query = @"
            SELECT Status, COUNT(*) FROM (
                SELECT Status FROM cmmt.cmmt_PatientStudyMetaData 
                WHERE FileName = @file AND CreatedAt BETWEEN @start AND @end
                UNION ALL
                SELECT Status FROM cmmt.cmmt_PatientStudySeriesData 
                WHERE FileName = @file AND CreatedAt BETWEEN @start AND @end
            ) AS Combined
            GROUP BY Status";

        var parameters = new DBParameters();
        parameters.Add("@file", file, ParameterDirection.Input, SqlDbType.NVarChar, 255);
        parameters.Add("@start", start, ParameterDirection.Input, SqlDbType.DateTime);
        parameters.Add("@end", end, ParameterDirection.Input, SqlDbType.DateTime);

        using var reader = await dbLayer.ExecuteReader_QueryWithParamsAsync(query, parameters);
        while (await reader.ReadAsync())
        {
            var status = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var count = reader.GetInt32(1);
            switch (status.ToRowStatus())
            {
                case RowStatus.Valid: valid += count; break;
                case RowStatus.Invalid: invalid += count; break;
                case RowStatus.Migrated: migrated += count; break;
                case RowStatus.Duplicated: duplicated += count; break;
                case RowStatus.Unknown: unknown += count; break;
            }
        }

        return (valid, invalid, duplicated, migrated, unknown);
    }

    public static async Task<int> GetErrorCountAsync(DBLayer dbLayer, string file, DateTime start, DateTime end)
    {
        string query = @"SELECT COUNT(*) FROM cmmt.cmmt_ErrorLog WHERE FileName = @file AND CreatedAt BETWEEN @start AND @end";

        var parameters = new DBParameters();
        parameters.Add("@file", file, ParameterDirection.Input, SqlDbType.NVarChar, 255);
        parameters.Add("@start", start, ParameterDirection.Input, SqlDbType.DateTime);
        parameters.Add("@end", end, ParameterDirection.Input, SqlDbType.DateTime);

        using var reader = await dbLayer.ExecuteReader_QueryWithParamsAsync(query, parameters);
        if (await reader.ReadAsync())
        {
            return reader.GetInt32(0);
        }
        return 0;
    }

    public static async Task<DataTable> GetInvalidRowsTableAsync(DBLayer dbLayer, string file, DateTime start, DateTime end)
    {
        var table = new DataTable("InvalidRows");
        table.Columns.AddRange(new[] {
            new DataColumn("File Name"),
            new DataColumn("Row Number", typeof(int)),
            new DataColumn("StudyInstanceUID"),
            new DataColumn("StudyTime"),
            new DataColumn("Patient FirstName"),
            new DataColumn("Patient MiddleName"),
            new DataColumn("Patient LastName"),
            new DataColumn("MRN"),
            new DataColumn("Institution Name"),
            new DataColumn("InvalidReason"),
            new DataColumn("Processed DateTime")
        });

        string query = @"
            SELECT 
                m.FileName, m.RowNumber, m.StudyInstanceUID, m.StudyStartDateTime,
                m.FirstName, m.MiddleName, m.LastName, m.MRN, m.InstitutionName,
                m.Status, s.ProcessedDateTime
            FROM cmmt.cmmt_PatientStudyMetaData m
            LEFT JOIN cmmt.cmmt_PatientStudySeriesData s
                ON m.FileName = s.FileName AND m.RowNumber = s.RowNumber
            WHERE m.FileName = @file AND m.Status IN ('I','D') 
                AND m.CreatedAt BETWEEN @start AND @end

            UNION ALL

            SELECT 
                s.FileName, s.RowNumber, s.StudyInstanceUID, NULL,
                NULL, NULL, NULL, NULL, NULL,
                s.Status, s.ProcessedDateTime
            FROM cmmt.cmmt_PatientStudySeriesData s
            WHERE s.FileName = @file AND s.Status IN ('I','D')
                AND s.CreatedAt BETWEEN @start AND @end";

        var parameters = new DBParameters();
        parameters.Add("@file", file, ParameterDirection.Input, SqlDbType.NVarChar, 255);
        parameters.Add("@start", start, ParameterDirection.Input, SqlDbType.DateTime);
        parameters.Add("@end", end, ParameterDirection.Input, SqlDbType.DateTime);

        using var reader = await dbLayer.ExecuteReader_QueryWithParamsAsync(query, parameters);
        while (await reader.ReadAsync())
        {
            string status = reader.IsDBNull(9) ? string.Empty : reader.GetString(9);
            string reason = status == "I" ? "MRN / LastName / Study Instance ID is null" : status == "D" ? "Records with Duplicate Study Instance ID" : "";
            table.Rows.Add(
                GetValueOrNullLabel(reader["FileName"]),
                GetValueOrNullLabel(reader["RowNumber"]),
                GetValueOrNullLabel(reader["StudyInstanceUID"]),
                GetValueOrNullLabel(reader["StudyStartDateTime"]),
                GetValueOrNullLabel(reader["FirstName"]),
                GetValueOrNullLabel(reader["MiddleName"]),
                GetValueOrNullLabel(reader["LastName"]),
                GetValueOrNullLabel(reader["MRN"]),
                GetValueOrNullLabel(reader["InstitutionName"]),
                reason,
                GetValueOrNullLabel(reader["ProcessedDateTime"])
            );

        }
        return table;
    }

    public static async Task<DataTable> GetErrorRowsTableAsync(DBLayer dbLayer, string file, DateTime start, DateTime end)
    {
        var table = new DataTable("ErrorRows");
        table.Columns.AddRange(new[] {
            new DataColumn("File Name"),
            new DataColumn("Row Number", typeof(int)),
            new DataColumn("StudyInstanceUID"),
            new DataColumn("StudyTime"),
            new DataColumn("Patient FirstName"),
            new DataColumn("Patient Middle Name"),
            new DataColumn("Patient Last Name"),
            new DataColumn("MRN"),
            new DataColumn("Institution Name"),
            new DataColumn("ErrorReason"),
            new DataColumn("Processed DateTime")
        });

        string query = @"
            SELECT 
                e.FileName, e.RowNumber,
                m.StudyInstanceUID, m.StudyStartDateTime,
                m.FirstName, m.MiddleName, m.LastName,
                m.MRN, m.InstitutionName,
                e.ErrorMessage,
                NULL AS ProcessedDateTime
            FROM cmmt.cmmt_ErrorLog e
            JOIN cmmt.cmmt_PatientStudyMetaData m 
                ON e.FileName = m.FileName AND e.RowNumber = m.RowNumber
            WHERE e.FileName = @file
              AND e.CreatedAt BETWEEN @start AND @end

            UNION ALL

            SELECT 
                e.FileName, e.RowNumber,
                s.StudyInstanceUID, NULL,
                NULL, NULL, NULL,
                NULL, NULL,
                e.ErrorMessage,
                s.ProcessedDateTime
            FROM cmmt.cmmt_ErrorLog e
            JOIN cmmt.cmmt_PatientStudySeriesData s 
                ON e.FileName = s.FileName AND e.RowNumber = s.RowNumber
            WHERE e.FileName = @file
              AND e.CreatedAt BETWEEN @start AND @end";

        var parameters = new DBParameters();
        parameters.Add("@file", file, ParameterDirection.Input, SqlDbType.NVarChar, 255);
        parameters.Add("@start", start, ParameterDirection.Input, SqlDbType.DateTime);
        parameters.Add("@end", end, ParameterDirection.Input, SqlDbType.DateTime);

        using var reader = await dbLayer.ExecuteReader_QueryWithParamsAsync(query, parameters);
        while (await reader.ReadAsync())
        {
            table.Rows.Add(
                GetValueOrNullLabel(reader["FileName"]),
                GetValueOrNullLabel(reader["RowNumber"]),
                GetValueOrNullLabel(reader["StudyInstanceUID"]),
                GetValueOrNullLabel(reader["StudyStartDateTime"]),
                GetValueOrNullLabel(reader["FirstName"]),
                GetValueOrNullLabel(reader["MiddleName"]),
                GetValueOrNullLabel(reader["LastName"]),
                GetValueOrNullLabel(reader["MRN"]),
                GetValueOrNullLabel(reader["InstitutionName"]),
                GetValueOrNullLabel(reader["ErrorMessage"]),
                GetValueOrNullLabel(reader["ProcessedDateTime"])
            );

        }
        return table;
    }

    private static object GetValueOrNullLabel(object value)
    {
        if (value == DBNull.Value || string.IsNullOrWhiteSpace(value?.ToString()))
            return "NULL";
        return value;
    }
}

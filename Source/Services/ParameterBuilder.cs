using System.Data;
using CMMT.dao;

namespace CMMT.Services
{
    public static class ParameterBuilder
    {
        public static DBParameters BuildParametersStudyMetaData(DataRow row)
        {
            var parameters = new DBParameters();
            parameters.Add("@MRN", row["MRN"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@InstitutionName", row["InstitutionName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@MRN2", row["MRN2"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@InstitutionName2", row["InstitutionName2"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@LastName", row["LastName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@FirstName", row["FirstName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@MiddleName", row["MiddleName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@Title", row["Title"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@Honorific", row["Honorific"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@Birthdate", row["Birthdate"], ParameterDirection.Input, SqlDbType.NVarChar, 23);
            parameters.Add("@Gender", row["Gender"], ParameterDirection.Input, SqlDbType.NVarChar, 1);
            parameters.Add("@Race", row["Race"], ParameterDirection.Input, SqlDbType.NVarChar, 20);
            parameters.Add("@AddressLine1", row["AddressLine1"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@AddressLine2", row["AddressLine2"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@City", row["City"], ParameterDirection.Input, SqlDbType.NVarChar, 20);
            parameters.Add("@State", row["State"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@Zip", row["Zip"], ParameterDirection.Input, SqlDbType.NVarChar, 10);
            parameters.Add("@Country", row["Country"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@HomeTelephoneNumber", row["HomeTelephoneNumber"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@OtherTelephoneNumber", row["OtherTelephoneNumber"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@AltIdNumber", row["AltIdNumber"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@StudyInstanceUID", row["StudyInstanceUID"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@StudyStartDateTime", row["StudyStartDateTime"], ParameterDirection.Input, SqlDbType.NVarChar, 23);
            parameters.Add("@FillerOrderNumber", row["FillerOrderNumber"], ParameterDirection.Input, SqlDbType.NVarChar, 75);
            parameters.Add("@AccessionNumber", row["AccessionNumber"], ParameterDirection.Input, SqlDbType.NVarChar, 16);
            parameters.Add("@ReferringPhysicianLastName", row["ReferringPhysicianLastName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@ReferringPhysicianFirstName", row["ReferringPhysicianFirstName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@ReferringPhysicianMiddleName", row["ReferringPhysicianMiddleName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@ReferringPhysicianTitle", row["ReferringPhysicianTitle"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@ReferringPhysicianHonorific", row["ReferringPhysicianHonorific"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@TechnologistLastName", row["TechnologistLastName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@TechnologistFirstName", row["TechnologistFirstName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@TechnologistMiddleName", row["TechnologistMiddleName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@TechnologistTitle", row["TechnologistTitle"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@TechnologistHonorific", row["TechnologistHonorific"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@ScheduledStartDateTime", row["ScheduledStartDateTime"], ParameterDirection.Input, SqlDbType.NVarChar, 23);
            parameters.Add("@ReasonForStudy", row["ReasonForStudy"], ParameterDirection.Input, SqlDbType.NVarChar, 255);
            parameters.Add("@PatientHeight", row["PatientHeight"], ParameterDirection.Input, SqlDbType.NVarChar, 16);
            parameters.Add("@PatientWeight", row["PatientWeight"], ParameterDirection.Input, SqlDbType.NVarChar, 16);
            parameters.Add("@OrderingPhysicianLastName", row["OrderingPhysicianLastName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@OrderingPhysicianFirstName", row["OrderingPhysicianFirstName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@OrderingPhysicianMiddleName", row["OrderingPhysicianMiddleName"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@OrderingPhysicianTitle", row["OrderingPhysicianTitle"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@OrderingPhysicianHonorific", row["OrderingPhysicianHonorific"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@StudyType", row["StudyType"], ParameterDirection.Input, SqlDbType.NVarChar, 20);
            parameters.Add("@PatientClassCode", row["PatientClassCode"], ParameterDirection.Input, SqlDbType.NVarChar, 1);
            parameters.Add("@LocationDescription", row["LocationDescription"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@ModalityType", row["ModalityType"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@Comments", row["Comments"], ParameterDirection.Input, SqlDbType.NVarChar, 64);
            parameters.Add("@URL", row["URL"], ParameterDirection.Input, SqlDbType.NVarChar, 199);
            parameters.Add("@Source", row["Source"], ParameterDirection.Input, SqlDbType.NVarChar, 199);
            parameters.Add("@return_value", DBNull.Value, ParameterDirection.ReturnValue, SqlDbType.Int);
            return parameters;
        }
    }
}

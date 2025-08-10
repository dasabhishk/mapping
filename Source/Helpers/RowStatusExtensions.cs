using CMMT.Models;

namespace CMMT.Helpers;

public static class RowStatusExtensions
{
    public static RowStatus ToRowStatus(this string code)
    {
        return code switch
        {
            "V" => RowStatus.Valid,
            "I" => RowStatus.Invalid,
            "P" => RowStatus.Migrated,
            "D" => RowStatus.Duplicated,
            "E" => RowStatus.Error,
             _  => RowStatus.Unknown
        };
    }
}

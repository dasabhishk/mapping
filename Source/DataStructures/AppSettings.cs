using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMMT.DataStructures
{
    public class AppSettings
    {
        public required int? MaxSampleRows { get; set; }

        public required int? MaxParallel { get; set; }

        public required int? PreviewSampleCount { get; set; }

        public required CsvTypes CsvTypes { get; set; }

        public required string StudyDataProcedure { get; set; }

        public required string NoMappingOptional { get; set; }

        public required string CsvDelimitter { get; set; }

        public required int TimeOutCount { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMMT.DataStructures
{
    public class CsvTypes
    {
        /// <summary>
        /// Gets or sets the type of CSV mapping
        /// </summary>
        public string PatientStudy { get; set; }

        /// <summary>
        /// Gets or sets the type of CSV mapping for Series Instance
        /// </summary>
        public string SeriesInstance { get; set; }
    }
}

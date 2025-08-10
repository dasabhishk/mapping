using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CMMT.dao;

public class ProcedureParameters
{
    public DBParameters Parameters { get; set; }
    public string FileName { get; set; }
    public int RowNumber { get; set; }
    public object Id { get; set; }
}
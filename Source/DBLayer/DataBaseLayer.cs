using System.Data.SqlClient;
using System.Data;
using System.Collections;
using Microsoft.SqlServer.Management.Smo;
using CMMT.Services;

namespace CMMT.dao
{
    public class Connection : IDisposable
    {

        private readonly string _conectionstring = "";
        protected SqlConnection? _sqlconnection = null;
        protected SqlTransaction? _sqltransaction = null;

        #region "Constructor & Destructor"

        public Connection(string ConnectionString)
        {
            _sqlconnection = new SqlConnection
            {
                ConnectionString = ConnectionString
            };
        }

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {

                }
            }
            disposed = true;
        }

        ~Connection()
        {
            Dispose(false);
        }

        #endregion

        public void OpenConnection()
        {
            try
            {
                if (_sqlconnection != null)
                {
                    if (_sqlconnection.State != ConnectionState.Open)
                    {
                        _sqlconnection.Open();
                    }
                }
                else
                {
                    _sqlconnection = new SqlConnection
                    {
                        ConnectionString = _conectionstring
                    };
                    _sqlconnection.Open();
                }
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public SqlConnection GetConnection()
        {
            return _sqlconnection;
        }

        public bool CloseConnection()
        {
            try
            {
                if (_sqlconnection != null)
                {
                    if (_sqlconnection.State == ConnectionState.Open)
                    {
                        _sqlconnection.Close();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public bool BeginTransaction()
        {
            try
            {
                if (_sqlconnection != null)
                {
                    if (_sqlconnection.State == ConnectionState.Open)
                    {
                        _sqltransaction = _sqlconnection.BeginTransaction();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public SqlTransaction GetTransaction()
        {
            return _sqltransaction;
        }

        public bool CommitTransaction()
        {
            try
            {
                if (_sqltransaction != null)
                {
                    _sqltransaction.Commit();
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public bool RollbackTransaction()
        {
            try
            {
                if (_sqltransaction != null)
                {
                    _sqltransaction.Rollback();
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

    }

    public class DBLayer : IDisposable
    {
        private readonly string _conectionstring = "";
        protected bool _istransaction = false;
        readonly Connection oConnection;
        private int _commandTimeout;
        #region " Constructor & Destructor "

        public DBLayer(string ConnectionString)
        {
            _conectionstring = ConnectionString;
            oConnection = new Connection(_conectionstring);
        }

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {

                }
            }
            disposed = true;
        }

        ~DBLayer()
        {
            Dispose(false);
        }

        #endregion

        #region " Connect & Disconnect Methods "

        public bool Connect(bool IsTransaction)
        {

            try
            {
                if (IsTransaction == true)
                {
                    oConnection.OpenConnection();
                    oConnection.BeginTransaction();
                    _istransaction = IsTransaction;
                    return true;
                }
                else
                {
                    oConnection.OpenConnection();
                    return true;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public bool Disconnect()
        {
            try
            {
                if (_istransaction == true)
                {
                    oConnection.CommitTransaction();
                    oConnection.CloseConnection();
                    return true;
                }
                else
                {
                    oConnection.CloseConnection();
                    return true;
                }
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
        }

        #endregion

        #region " Insert Update Delete - Using Stored Procedure "

        public int Execute_SP(string StoredProcedureName, DBParameters Parameters)
        {
            int _result = 0;
            int _count = 0;
            SqlCommand _sqlcommand = new();
            SqlParameter? osqlParameter;

            try
            {
                _sqlcommand.CommandType = CommandType.StoredProcedure;
                _sqlcommand.CommandText = StoredProcedureName;
                _sqlcommand.Connection = oConnection.GetConnection();


                for (_count = 0; _count <= Parameters.Count - 1; _count++)
                {
                    osqlParameter = new SqlParameter
                    {
                        ParameterName = Parameters[_count].ParameterName,
                        SqlDbType = Parameters[_count].DataType,
                        Direction = Parameters[_count].ParameterDirection,
                        Value = Parameters[_count].Value
                    };
                    if (Parameters[_count].Size > 0)
                    {
                        if (Parameters[_count].Size != 0)
                        {
                            osqlParameter.Size = Parameters[_count].Size;
                        }
                    }
                    _sqlcommand.Parameters.Add(osqlParameter);
                    osqlParameter = null;
                }

                _result = _sqlcommand.ExecuteNonQuery();

            }
            catch (SqlException)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            catch (Exception)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
            return _result;
        }

        public int Execute_SP(string StoredProcedureName)
        {
            int _result = 0;
            SqlCommand _sqlcommand = new();

            try
            {
                _sqlcommand.CommandType = CommandType.StoredProcedure;
                _sqlcommand.CommandText = StoredProcedureName;
                _sqlcommand.Connection = oConnection.GetConnection();


                _result = _sqlcommand.ExecuteNonQuery();

            }
            catch (SqlException)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            catch (Exception)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
            return _result;
        }

        public int Execute_SP(string StoredProcedureName, DBParameters Parameters, out object ParameterValue)
        {
            int _result = 0;
            int _count = 0;
            SqlCommand _sqlcommand = new();
            int _outputCount = 0;
            SqlParameter? osqlParameter;
            try
            {
                _sqlcommand.CommandType = CommandType.StoredProcedure;
                _sqlcommand.CommandText = StoredProcedureName;
                _sqlcommand.Connection = oConnection.GetConnection();
                _sqlcommand.CommandTimeout = GetCommandTimeout();


                for (_count = 0; _count <= Parameters.Count - 1; _count++)
                {
                    if (Parameters[_count].ParameterDirection == ParameterDirection.Output || Parameters[_count].ParameterDirection == ParameterDirection.InputOutput
                        | Parameters[_count].ParameterDirection == ParameterDirection.ReturnValue)
                    {
                        _outputCount = _count;
                    }
                    osqlParameter = new SqlParameter
                    {
                        ParameterName = Parameters[_count].ParameterName,
                        SqlDbType = Parameters[_count].DataType,
                        Direction = Parameters[_count].ParameterDirection,
                        Value = Parameters[_count].Value
                    };
                    if (Parameters[_count].Size > 0)
                    {
                        if (Parameters[_count].Size != 0)
                        {
                            osqlParameter.Size = Parameters[_count].Size;
                        }
                    }
                    _sqlcommand.Parameters.Add(osqlParameter);
                    osqlParameter = null;
                }


                _result = _sqlcommand.ExecuteNonQuery();


                if (_sqlcommand.Parameters[_outputCount].Value != null)
                {
                    ParameterValue = _sqlcommand.Parameters[_outputCount].Value;
                }
                else
                {
                    ParameterValue = 0;
                }

            }
            catch (SqlException)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            catch (Exception)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
            return _result;
        }

        public object ExecuteScalar_SP(string StoredProcedureName, DBParameters Parameters)
        {
            object? _result = null;
            int _count = 0;
            SqlCommand _sqlcommand = new();
            SqlParameter? osqlParameter;

            try
            {
                _sqlcommand.CommandType = CommandType.StoredProcedure;
                _sqlcommand.CommandText = StoredProcedureName;
                _sqlcommand.Connection = oConnection.GetConnection();

                for (_count = 0; _count <= Parameters.Count - 1; _count++)
                {
                    osqlParameter = new SqlParameter
                    {
                        ParameterName = Parameters[_count].ParameterName,
                        SqlDbType = Parameters[_count].DataType,
                        Direction = Parameters[_count].ParameterDirection,
                        Value = Parameters[_count].Value
                    };
                    if (Parameters[_count].Size > 0)
                    {
                        if (Parameters[_count].Size != 0)
                        {
                            osqlParameter.Size = Parameters[_count].Size;
                        }
                    }
                    _sqlcommand.Parameters.Add(osqlParameter);
                    osqlParameter = null;
                }

                _result = _sqlcommand.ExecuteScalar();

                _result ??= "";

            }
            catch (SqlException)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            catch (Exception)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }

            return _result;
        }

        public object ExecuteScalar_SP(string StoredProcedureName)
        {
            object? _result = null;
            SqlCommand _sqlcommand = new();

            try
            {
                _sqlcommand.CommandType = CommandType.StoredProcedure;
                _sqlcommand.CommandText = StoredProcedureName;
                _sqlcommand.Connection = oConnection.GetConnection();

                _result = _sqlcommand.ExecuteScalar();

                _result ??= "";

            }
            catch (SqlException)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            catch (Exception)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }

            return _result;
        }

        #endregion

        #region " Insert Update Delete - Using SQL Query "

        public int Execute_Query(string SQLQuery)
        {
            int _result = 0;
            SqlCommand _sqlcommand = new();

            try
            {
                _sqlcommand.CommandType = CommandType.Text;
                _sqlcommand.CommandText = SQLQuery;
                _sqlcommand.Connection = oConnection.GetConnection();

                _result = _sqlcommand.ExecuteNonQuery();

            }
            catch (SqlException)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            catch (Exception)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
            return _result;
        }

        public void ExecuteReader_Query(string SQLQuery, out SqlDataReader DataReader)
        {
            SqlCommand _sqlcommand = new();
            try
            {
                _sqlcommand.CommandType = CommandType.Text;
                _sqlcommand.CommandText = SQLQuery;
                _sqlcommand.Connection = oConnection.GetConnection();

                DataReader = _sqlcommand.ExecuteReader();
            }
            catch (SqlException)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            catch (Exception)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
        }

        public async Task<SqlDataReader> ExecuteReader_QueryAsync(string SQLQuery)
        {
            SqlCommand _sqlcommand = new();

            try
            {
                _sqlcommand.CommandType = CommandType.Text;
                _sqlcommand.CommandText = SQLQuery;
                _sqlcommand.Connection = oConnection.GetConnection();

                return await _sqlcommand.ExecuteReaderAsync();
            }
            catch (SqlException ex)
            {
                if (_istransaction)
                {
                    oConnection.RollbackTransaction();
                }
                LoggingService.LogError("SQL error during ExecuteReader_QueryAsync.", ex);
                throw;
            }
            catch (Exception ex)
            {
                if (_istransaction)
                {
                    oConnection.RollbackTransaction();
                }
                LoggingService.LogError("Error during ExecuteReader_QueryAsync.", ex);
                throw;
            }
            // ❗ Do not dispose the SqlCommand here; the SqlDataReader depends on it until it's closed.
        }

        public async Task<SqlDataReader> ExecuteReader_QueryWithParamsAsync(string SQLQuery, DBParameters Parameters)
        {
            SqlCommand _sqlcommand = new();

            try
            {
                _sqlcommand.CommandType = CommandType.Text;
                _sqlcommand.CommandText = SQLQuery;
                _sqlcommand.Connection = oConnection.GetConnection();

                for (int i = 0; i < Parameters.Count; i++)
                {
                    var param = Parameters[i];
                    var sqlParam = new SqlParameter
                    {
                        ParameterName = param.ParameterName,
                        SqlDbType = param.DataType,
                        Direction = param.ParameterDirection,
                        Value = param.Value ?? DBNull.Value
                    };

                    if (param.Size > 0)
                    {
                        sqlParam.Size = param.Size;
                    }

                    _sqlcommand.Parameters.Add(sqlParam);
                }

                return await _sqlcommand.ExecuteReaderAsync();
            }
            catch (SqlException ex)
            {
                if (_istransaction)
                {
                    oConnection.RollbackTransaction();
                }
                LoggingService.LogError("SQL error during ExecuteReader_QueryWithParamsAsync.", ex);
                throw;
            }
            catch (Exception ex)
            {
                if (_istransaction)
                {
                    oConnection.RollbackTransaction();
                }
                LoggingService.LogError("Error during ExecuteReader_QueryWithParamsAsync.", ex);
                throw;
            }
            // Do NOT dispose _sqlcommand here because the returned SqlDataReader depends on it.
        }


        public object ExecuteScalar_Query(string SQLQuery)
        {
            object? _result = null;
            SqlCommand _sqlcommand = new();

            try
            {
                _sqlcommand.CommandType = CommandType.Text;
                _sqlcommand.CommandText = SQLQuery;
                _sqlcommand.Connection = oConnection.GetConnection();

                _result = _sqlcommand.ExecuteScalar();

                _result ??= "";

            }
            catch (SqlException)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            catch (Exception)
            {
                if (_istransaction == true)
                {
                    oConnection.RollbackTransaction();
                }
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }

            return _result;
        }

        public TResult ExecuteQuery_WithParams<TResult>(string SQLQuery, DBParameters parameters)
        {
            SqlCommand _sqlcommand = new();
            object result;

            try
            {
                _sqlcommand.CommandType = CommandType.Text;
                _sqlcommand.CommandText = SQLQuery;
                _sqlcommand.Connection = oConnection.GetConnection();

                for (int i = 0; i < parameters.Count; i++)
                {
                    var param = parameters[i];
                    var sqlParam = new SqlParameter(param.ParameterName, param.DataType)
                    {
                        Value = param.Value ?? DBNull.Value,
                        Direction = param.ParameterDirection
                    };

                    if (param.Size > 0)
                        sqlParam.Size = param.Size;

                    _sqlcommand.Parameters.Add(sqlParam);
                }

                result = _sqlcommand.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                    return default!;
                else
                    return (TResult)Convert.ChangeType(result, typeof(TResult));
            }
            catch (SqlException ex)
            {
                if (_istransaction)
                    oConnection.RollbackTransaction();

                LoggingService.LogError("SQL error during ExecuteQuery_WithParams.", ex);
                throw;
            }
            catch (Exception ex)
            {
                if (_istransaction)
                    oConnection.RollbackTransaction();

                LoggingService.LogError("Error during ExecuteQuery_WithParams.", ex);
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
        }

        public async Task<TResult> ExecuteQuery_WithParamsAsync<TResult>(string SQLQuery, DBParameters parameters)
        {
            SqlCommand _sqlcommand = new();
            object? result;

            try
            {
                _sqlcommand.CommandType = CommandType.Text;
                _sqlcommand.CommandText = SQLQuery;
                _sqlcommand.Connection = oConnection.GetConnection();

                for (int i = 0; i < parameters.Count; i++)
                {
                    var param = parameters[i];
                    var sqlParam = new SqlParameter(param.ParameterName, param.DataType)
                    {
                        Value = param.Value ?? DBNull.Value,
                        Direction = param.ParameterDirection
                    };

                    if (param.Size > 0)
                        sqlParam.Size = param.Size;

                    _sqlcommand.Parameters.Add(sqlParam);
                }

                result = await _sqlcommand.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value)
                    return default!;
                else
                    return (TResult)Convert.ChangeType(result, typeof(TResult));
            }
            catch (SqlException ex)
            {
                if (_istransaction)
                    oConnection.RollbackTransaction();

                LoggingService.LogError("SQL error during ExecuteQuery_WithParamsAsync.", ex);
                throw;
            }
            catch (Exception ex)
            {
                if (_istransaction)
                    oConnection.RollbackTransaction();

                LoggingService.LogError("Error during ExecuteQuery_WithParamsAsync.", ex);
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
        }



        #endregion

        #region " Methods to get data using - Stored Procedure"

        public void Execute_SP_Reader(string StoredProcedureName, DBParameters Parameters, out SqlDataReader DataReader)
        {
            int _count = 0;
            SqlCommand _sqlcommand = new();
            SqlParameter? osqlParameter;
            try
            {
                _sqlcommand.CommandType = CommandType.StoredProcedure;
                _sqlcommand.CommandText = StoredProcedureName;
                _sqlcommand.Connection = oConnection.GetConnection();

                for (_count = 0; _count <= Parameters.Count - 1; _count++)
                {
                    osqlParameter = new SqlParameter
                    {
                        ParameterName = Parameters[_count].ParameterName,
                        SqlDbType = Parameters[_count].DataType,
                        Direction = Parameters[_count].ParameterDirection,
                        Value = Parameters[_count].Value
                    };
                    if (Parameters[_count].Size > 0)
                    {
                        if (Parameters[_count].Size != 0)
                        {
                            osqlParameter.Size = Parameters[_count].Size;
                        }
                    }

                    _sqlcommand.Parameters.Add(osqlParameter);
                    osqlParameter = null;
                }

                DataReader = _sqlcommand.ExecuteReader();

            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
        }

        public void Execute_SP_DataSet(string StoredProcedureName, DBParameters Parameters, out DataSet ds)
        {
            int _count = 0;
            SqlCommand _sqlcommand = new();
            SqlParameter? osqlParameter;

            try
            {
                _sqlcommand.CommandType = CommandType.StoredProcedure;
                _sqlcommand.CommandText = StoredProcedureName;
                _sqlcommand.Connection = oConnection.GetConnection();

                for (_count = 0; _count <= Parameters.Count - 1; _count++)
                {
                    osqlParameter = new SqlParameter
                    {
                        ParameterName = Parameters[_count].ParameterName,
                        SqlDbType = Parameters[_count].DataType,
                        Direction = Parameters[_count].ParameterDirection,
                        Value = Parameters[_count].Value
                    };
                    if (Parameters[_count].Size > 0)
                    {
                        if (Parameters[_count].Size != 0)
                        {
                            osqlParameter.Size = Parameters[_count].Size;
                        }
                    }
                    _sqlcommand.Parameters.Add(osqlParameter);
                    osqlParameter = null;
                }

                SqlDataAdapter _dataAdapter = new(_sqlcommand);

                DataSet _ds = new();

                _dataAdapter.Fill(_ds);
                _dataAdapter.Dispose();

                ds = _ds;
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
        }

        public void Execute_SP_DataTable(string StoredProcedureName, DBParameters Parameters, out DataTable dt)
        {
            int _count = 0;
            SqlCommand _sqlcommand = new();
            SqlParameter osqlParameter;
            try
            {
                _sqlcommand.CommandType = CommandType.StoredProcedure;
                _sqlcommand.CommandText = StoredProcedureName;
                _sqlcommand.Connection = oConnection.GetConnection();

                for (_count = 0; _count <= Parameters.Count - 1; _count++)
                {
                    osqlParameter = new SqlParameter
                    {
                        ParameterName = Parameters[_count].ParameterName,
                        SqlDbType = Parameters[_count].DataType,
                        Direction = Parameters[_count].ParameterDirection,
                        Value = Parameters[_count].Value
                    };
                    if (Parameters[_count].Size > 0)
                    {
                        if (Parameters[_count].Size != 0)
                        {
                            osqlParameter.Size = Parameters[_count].Size;
                        }
                    }
                    _sqlcommand.Parameters.Add(osqlParameter);
                    osqlParameter = null;
                }

                SqlDataAdapter _da = new(_sqlcommand);
                DataSet _ds = new();
                DataTable _dt = new();

                _da.Fill(_ds);
                if (_ds.Tables[0] != null)
                {
                    _dt = _ds.Tables[0];
                }
                dt = _dt;

                _dt.Dispose();
                _ds.Dispose();
                _da.Dispose();
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
        }

        public void Execute_SP_Reader(string StoredProcedureName, out SqlDataReader DataReader)
        {
            SqlCommand _sqlcommand = new();

            try
            {
                _sqlcommand.CommandType = CommandType.StoredProcedure;
                _sqlcommand.CommandText = StoredProcedureName;
                _sqlcommand.Connection = oConnection.GetConnection();

                DataReader = _sqlcommand.ExecuteReader();

            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
        }

        public void Execute_SP_DatSet(string StoredProcedureName, out DataSet ds)
        {
            SqlCommand _sqlcommand = new();
            try
            {
                _sqlcommand.CommandType = CommandType.StoredProcedure;
                _sqlcommand.CommandText = StoredProcedureName;
                _sqlcommand.Connection = oConnection.GetConnection();

                SqlDataAdapter _da = new(_sqlcommand);

                DataSet _ds = new();

                _da.Fill(_ds);
                _da.Dispose();
                ds = _ds;
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
        }

        public void Execute_SP_DataTable(string StoredProcedureName, out DataTable dt)
        {
            SqlCommand _sqlcommand = new();

            try
            {
                _sqlcommand.CommandType = CommandType.StoredProcedure;
                _sqlcommand.CommandText = StoredProcedureName;
                _sqlcommand.Connection = oConnection.GetConnection();


                SqlDataAdapter _da = new(_sqlcommand);
                DataSet _ds = new();
                DataTable _dt = new();

                _da.Fill(_ds);
                if (_ds.Tables[0] != null)
                {
                    _dt = _ds.Tables[0];
                }
                dt = _dt;

                _dt.Dispose();
                _ds.Dispose();
                _da.Dispose();
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
        }

        #endregion

        #region " Methods to get data using - SQL Query"

        public void Execute_Query_Reader(string SQLQuery, out SqlDataReader DataReader)
        {
            SqlCommand _sqlcommand = new();

            try
            {
                _sqlcommand.CommandType = CommandType.Text;
                _sqlcommand.CommandText = SQLQuery;
                _sqlcommand.Connection = oConnection.GetConnection();

                DataReader = _sqlcommand.ExecuteReader();
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
        }

        public void Execute_Query_DataSet(string SQLQuery, out DataSet ds)
        {
            SqlCommand _sqlcommand = new();

            try
            {
                _sqlcommand.CommandType = CommandType.Text;
                _sqlcommand.CommandText = SQLQuery;
                _sqlcommand.Connection = oConnection.GetConnection();

                SqlDataAdapter _da = new(_sqlcommand);
                DataSet _ds = new();

                _da.Fill(_ds);
                _da.Dispose();

                ds = _ds;
            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
        }

        public void Execute_Query_DataTable(string SQLQuery, out DataTable dt)
        {
            SqlCommand _sqlcommand = new();

            try
            {
                _sqlcommand.CommandType = CommandType.Text;
                _sqlcommand.CommandText = SQLQuery;
                _sqlcommand.Connection = oConnection.GetConnection();

                SqlDataAdapter _da = new(_sqlcommand);
                DataSet _ds = new();
                DataTable _dt = new();

                _da.Fill(_ds);
                if (_ds.Tables[0] != null)
                {
                    _dt = _ds.Tables[0];
                }
                dt = _dt;

                _dt.Dispose();
                _ds.Dispose();
                _da.Dispose();

            }
            catch (SqlException)
            {
                throw;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _sqlcommand?.Dispose();
            }
        }

        #endregion

        #region " Method to set and get timeout for sql commands"
        public void SetCommandTimeout(int timeout)
        {
            if (timeout <= 0)
            {
                throw new ArgumentException("Timeout must be greater than zero.", nameof(timeout));
            }
            _commandTimeout = timeout;
        }

        public int GetCommandTimeout()
        {
            // Return the current value of _commandTimeout, which defaults to 30 if not set.
            return _commandTimeout > 0 ? _commandTimeout : 30;
        }
        #endregion

    }

    public class DBParameter : IDisposable
    {
        private string _parametername;
        private ParameterDirection _parameterdirection;
        private SqlDbType _datatype;
        private object _value;
        private int _size = 0;


        #region " Constructor & Destructor "

        public DBParameter()
        {

        }

        public DBParameter(string parametername, object value, ParameterDirection parameterdirection, SqlDbType datatype, int fieldsize)
        {
            _parametername = parametername;
            _parameterdirection = parameterdirection;
            _datatype = datatype;
            _value = value;
            _size = fieldsize;
        }

        public DBParameter(string parametername, object value, ParameterDirection parameterdirection, SqlDbType datatype)
        {
            _parametername = parametername;
            _parameterdirection = parameterdirection;
            _datatype = datatype;
            _value = value;
        }

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {

                }
            }
            disposed = true;
        }

        ~DBParameter()
        {
            Dispose(false);
        }

        #endregion

        public string ParameterName
        {
            get { return _parametername; }
            set { _parametername = value; }
        }

        public ParameterDirection ParameterDirection
        {
            get { return _parameterdirection; }
            set { _parameterdirection = value; }
        }

        public SqlDbType DataType
        {
            get { return _datatype; }
            set { _datatype = value; }
        }

        public object Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public int Size
        {
            get { return _size; }
            set { _size = value; }
        }

    }

    public class DBParameters : IDisposable
    {
        protected ArrayList _parameterlist;

        #region "Constructor & Destructor"

        public DBParameters()
        {
            _parameterlist = new ArrayList();
        }

        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {

                }
            }
            disposed = true;
        }


        ~DBParameters()
        {
            Dispose(false);
        }
        #endregion

        public int Count
        {
            get { return _parameterlist.Count; }
        }

        public void Add(DBParameter item)
        {
            _parameterlist.Add(item);
        }

        public int Add(string parametername, object value, ParameterDirection parameterdirection, SqlDbType datatype, int fieldsize)
        {
            DBParameter item = new(parametername, value, parameterdirection, datatype, fieldsize);
            return _parameterlist.Add(item);
        }

        public int Add(string parametername, object value, ParameterDirection parameterdirection, SqlDbType datatype)
        {
            DBParameter item = new(parametername, value, parameterdirection, datatype);
            return _parameterlist.Add(item);
        }

        public bool Remove(DBParameter item)
        {
            bool result = false;
            for (int i = 0; i < _parameterlist.Count; i++)
            {
                if (_parameterlist[i] is DBParameter obj && obj.ParameterName == item.ParameterName && obj.DataType == item.DataType)
                {
                    _parameterlist.RemoveAt(i);
                    result = true;
                    break;
                }
            }
            return result;
        }

        public bool RemoveAt(int index)
        {
            bool result = false;
            _parameterlist.RemoveAt(index);
            result = true;
            return result;
        }

        public void Clear()
        {
            _parameterlist.Clear();
        }

        public DBParameter this[int index] => _parameterlist[index] as DBParameter ?? throw new InvalidOperationException("The parameter at the specified index is null.");

        public bool Contains(DBParameter item)
        {
            return _parameterlist.Contains(item);
        }

        public int IndexOf(DBParameter item)
        {
            return _parameterlist.IndexOf(item);
        }

        public void CopyTo(DBParameter[] array, int index)
        {
            _parameterlist.CopyTo(array, index);
        }

    }

    public class DBHandler
    {
        private static SqlConnection? sqlConnection;
        private static SqlCommand? sqlCommand;
        private static SqlDataAdapter? sqlDataAdapter;
        private static string? connectionString;

        public static bool IsConnectSuccessfuly
        {
            get
            {
                if (sqlConnection == null)
                {
                    throw new InvalidOperationException("The SQL connection has not been initialized.");
                }

                try
                {
                    sqlConnection.Open();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public static void InitDataBase(string server, string authType, string username = "", string password = "")
        {
            connectionString = GetConnstring(server, authType, username, password);
            InitObject();
        }

        public static string GetConnstring(string server, string authType, string username = "", string password = "")
        {
            return authType == "Sql"
                ? $"Server={server};User Id={username};Password={password};TrustServerCertificate=True;"
                : $"Server={server};Trusted_Connection=True;TrustServerCertificate=True;";
        }

        public static string GetConnstring(string serverName, string dataBaseName)
        {
            return "Data Source=" + serverName + ";Initial Catalog=" +
                 dataBaseName + ";Integrated Security=True;";
        }

        public static void InitDataBase(string serverName, string dataBaseName)
        {
            connectionString = GetConnstring(serverName, dataBaseName);
            InitObject();
        }

        private static void InitObject()
        {
            sqlConnection = new SqlConnection(connectionString);
            sqlCommand = new SqlCommand("", sqlConnection);
            sqlDataAdapter = new SqlDataAdapter(sqlCommand);

        }

        public static DataTable GetTableWithSchema(string tableName)
        {
            if (sqlCommand == null)
            {
                throw new InvalidOperationException("The SQL command object has not been initialized.");
            }

            DataTable dt = new();
            sqlCommand.CommandText = "SELECT Column_Name, Data_Type, Character_Maximum_Length," +
                                     "Numeric_Precision, Numeric_Scale FROM INFORMATION_SCHEMA.COLUMNS " +
                                     "WHERE Table_Name = @TableName";

            sqlCommand.Parameters.Clear();
            sqlCommand.Parameters.AddWithValue("@TableName", tableName);

            sqlDataAdapter?.Fill(dt);
            dt.TableName = tableName;
            return dt;
        }

        public static string[] GetServerList()
        {
            DataTable dt = SmoApplication.EnumAvailableSqlServers();
            string[] str = new string[dt.Rows.Count];
            for (int i = 0; i < dt.Rows.Count; i++)
                str[i] = dt.Rows[i][0]?.ToString() ?? string.Empty; // Use null-coalescing operator to handle null values  
            return str;
        }

        public static string[] GetDataBaseList(string serverName, string userId, string password)
        {
            Server sqlServer = new();
            sqlServer.ConnectionContext.ServerInstance = serverName;
            sqlServer.ConnectionContext.LoginSecure = false;
            sqlServer.ConnectionContext.Login = userId;
            sqlServer.ConnectionContext.Password = password;
            sqlServer.ConnectionContext.Connect();
            string[] str = new string[sqlServer.Databases.Count];
            for (int i = 0; i < sqlServer.Databases.Count; i++)
                str[i] = sqlServer.Databases[i].Name;
            return str;
        }

        public static string[] GetDataBaseList(string serverName)
        {
            Server sqlServer = new();
            sqlServer.ConnectionContext.ServerInstance = serverName;
            sqlServer.ConnectionContext.Connect();
            string[] str = new string[sqlServer.Databases.Count];
            for (int i = 0; i < sqlServer.Databases.Count; i++)
                str[i] = sqlServer.Databases[i].Name;
            return str;
        }
        public static string[] GetTableList()
        {
            if (sqlCommand == null)
            {
                throw new InvalidOperationException("The SQL command object has not been initialized.");
            }

            DataTable dt = new();
            sqlCommand.CommandText = "SELECT table_name FROM INFORMATION_SCHEMA.Tables " +
                "WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY table_name";

            sqlDataAdapter?.Fill(dt);

            string[] str = new string[dt.Rows.Count];
            for (int i = 0; i < dt.Rows.Count; i++)
                str[i] = dt.Rows[i][0]?.ToString() ?? string.Empty; // Use null-coalescing operator to handle null values  

            return str;
        }
        public static bool AttatchDB(string DBName, string mdfFileNameWithPath)
        {
            string sqlQuery;
            string ldfFileName;
            try
            {
                if (sqlCommand == null)
                {
                    throw new InvalidOperationException("The SQL command object has not been initialized.");
                }

                ldfFileName = mdfFileNameWithPath.Substring(0, mdfFileNameWithPath.Length - 4);
                ldfFileName += "_log.ldf";
                sqlQuery = " CREATE DATABASE [" + DBName + "] ON ";
                sqlQuery += " ( FILENAME = N'" + mdfFileNameWithPath + "' ),";
                sqlQuery += " ( FILENAME = N'" + ldfFileName + "' )";
                sqlQuery += " FOR ATTACH";
                sqlCommand.CommandText = sqlQuery;
                sqlCommand.ExecuteNonQuery();
                sqlQuery = "if not exists (select name from master.sys.databases sd where name = N'" + DBName + "' and SUSER_SNAME(sd.owner_sid) = SUSER_SNAME() )";
                sqlQuery += " EXEC [" + DBName + "].dbo.sp_changedbowner @loginame=N'sa', @map=false";
                sqlCommand.CommandText = sqlQuery;
                sqlCommand.ExecuteNonQuery();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (sqlConnection != null && sqlConnection.State == ConnectionState.Open)
                {
                    sqlConnection.Close();
                }
            }
        }
        public static string BuildConnectionString(string server, string authType, string username = "", string password = "")
        {
            return authType == "Sql"
                ? $"Server={server};User Id={username};Password={password};Connection Timeout=3;"
                : $"Server={server};Trusted_Connection=True;Connection Timeout=3;";
        }

    }
}
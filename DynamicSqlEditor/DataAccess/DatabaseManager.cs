using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using DynamicSqlEditor.Common;

namespace DynamicSqlEditor.DataAccess
{
    public class DatabaseManager : IDisposable
    {
        private readonly string _connectionString;
        public int DefaultTimeout { get; set; }

        public DatabaseManager(string connectionString, int defaultTimeout)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            _connectionString = connectionString;
            DefaultTimeout = defaultTimeout;
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public void TestConnection()
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    connection.Open();
                    FileLogger.Info($"Connection test successful to: {connection.DataSource}/{connection.Database}");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Connection test failed: {ex.Message}");
                throw; // Re-throw to indicate failure
            }
        }

         public bool IsConnected()
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    connection.Open();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public string GetDatabaseName()
        {
             try
            {
                using (var connection = CreateConnection())
                {
                    return connection.Database;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Failed to get database name from connection string.", ex);
                return null;
            }
        }


        public async Task<DataTable> ExecuteQueryAsync(string sql, List<SqlParameter> parameters)
        {
            var dataTable = new DataTable();
            try
            {
                using (var connection = CreateConnection())
                using (var command = new SqlCommand(sql, connection))
                {
                    command.CommandTimeout = DefaultTimeout;
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters.ToArray());
                    }

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        dataTable.Load(reader);
                    }
                }
            }
            catch (SqlException ex)
            {
                FileLogger.Error($"SQL Error executing query: {sql}", ex);
                throw; // Re-throw SqlException
            }
            catch (Exception ex)
            {
                FileLogger.Error($"General Error executing query: {sql}", ex);
                throw; // Re-throw other exceptions
            }
            return dataTable;
        }

        public async Task<int> ExecuteNonQueryAsync(string sql, List<SqlParameter> parameters)
        {
            try
            {
                using (var connection = CreateConnection())
                using (var command = new SqlCommand(sql, connection))
                {
                    command.CommandTimeout = DefaultTimeout;
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters.ToArray());
                    }

                    await connection.OpenAsync();
                    return await command.ExecuteNonQueryAsync();
                }
            }
            catch (SqlException ex)
            {
                FileLogger.Error($"SQL Error executing non-query: {sql}", ex);
                throw;
            }
             catch (Exception ex)
            {
                FileLogger.Error($"General Error executing non-query: {sql}", ex);
                throw;
            }
        }

        public async Task<object> ExecuteScalarAsync(string sql, List<SqlParameter> parameters)
        {
            try
            {
                using (var connection = CreateConnection())
                using (var command = new SqlCommand(sql, connection))
                {
                    command.CommandTimeout = DefaultTimeout;
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters.ToArray());
                    }

                    await connection.OpenAsync();
                    return await command.ExecuteScalarAsync();
                }
            }
            catch (SqlException ex)
            {
                FileLogger.Error($"SQL Error executing scalar: {sql}", ex);
                throw;
            }
             catch (Exception ex)
            {
                FileLogger.Error($"General Error executing scalar: {sql}", ex);
                throw;
            }
        }

        public void Dispose()
        {
            // SqlConnection handles pooling, so explicit disposal of the manager isn't strictly necessary
            // unless holding other resources.
        }
    }
}
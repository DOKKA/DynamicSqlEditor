// File: DynamicSqlEditor/Core/CrudManager.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicSqlEditor.Common;
using DynamicSqlEditor.DataAccess;
using DynamicSqlEditor.Schema.Models;

namespace DynamicSqlEditor.Core
{
    public class CrudManager
    {
        private readonly DatabaseManager _dbManager;
        private readonly TableSchema _tableSchema;
        private readonly ConcurrencyHandler _concurrencyHandler;

        public CrudManager(DatabaseManager dbManager, TableSchema tableSchema)
        {
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
            _tableSchema = tableSchema ?? throw new ArgumentNullException(nameof(tableSchema));
            _concurrencyHandler = new ConcurrencyHandler(tableSchema);
        }

        public async Task<int> InsertRecordAsync(Dictionary<string, object> columnValues)
        {
            var insertColumns = _tableSchema.Columns
                .Where(c => !c.IsIdentity && !c.IsComputed && !c.IsTimestamp && columnValues.ContainsKey(c.ColumnName))
                .ToList();

            if (!insertColumns.Any())
            {
                bool onlySpecialCols = _tableSchema.Columns.All(c => c.IsIdentity || c.IsComputed || c.IsTimestamp);
                if (onlySpecialCols)
                {
                    FileLogger.Warning($"Attempted insert into {_tableSchema.FullName} which has no insertable columns.");
                    return 0;
                }
                throw new InvalidOperationException("No insertable columns found or provided values for insertable columns.");
            }

            var sqlBuilder = new StringBuilder($"INSERT INTO [{_tableSchema.SchemaName}].[{_tableSchema.TableName}] (");
            sqlBuilder.Append(string.Join(", ", insertColumns.Select(c => $"[{c.ColumnName}]")));
            sqlBuilder.Append(") VALUES (");
            sqlBuilder.Append(string.Join(", ", insertColumns.Select(c => $"@{c.ColumnName}")));
            sqlBuilder.Append(");");

            var identityColumn = _tableSchema.Columns.FirstOrDefault(c => c.IsIdentity);
            if (identityColumn != null)
            {
                sqlBuilder.Append(" SELECT SCOPE_IDENTITY();");
            }

            var parameters = insertColumns
                .Select(c => SqlParameterHelper.CreateParameter($"@{c.ColumnName}", columnValues[c.ColumnName], c.GetSqlDbType()))
                .ToList();

            try
            {
                if (identityColumn != null)
                {
                    object result = await _dbManager.ExecuteScalarAsync(sqlBuilder.ToString(), parameters);
                    return (result == null || result == DBNull.Value) ? 0 : Convert.ToInt32(result);
                }
                else
                {
                    int rowsAffected = await _dbManager.ExecuteNonQueryAsync(sqlBuilder.ToString(), parameters);
                    return rowsAffected;
                }
            }
            catch (SqlException ex)
            {
                FileLogger.Error($"Error inserting record into {_tableSchema.FullName}", ex);
                throw new DataException($"Failed to insert record: {ex.Message}", ex);
            }
        }

        public async Task<int> UpdateRecordAsync(Dictionary<string, object> columnValues, Dictionary<string, object> originalKeyValues, object originalTimestamp)
        {
            var updateColumns = _tableSchema.Columns
                .Where(c => !c.IsPrimaryKey && !c.IsIdentity && !c.IsComputed && !c.IsTimestamp && columnValues.ContainsKey(c.ColumnName))
                .ToList();

            if (!updateColumns.Any())
            {
                FileLogger.Warning($"No updatable columns provided for update on {_tableSchema.FullName}.");
                return 0;
            }
            if (_tableSchema.PrimaryKeys.Count == 0)
            {
                throw new InvalidOperationException($"Cannot update table '{_tableSchema.FullName}' as it has no primary key defined.");
            }
            if (originalKeyValues == null || !_tableSchema.PrimaryKeys.All(pk => originalKeyValues.ContainsKey(pk.Column.ColumnName)))
            {
                throw new ArgumentException("Original key values dictionary is null or missing values for one or more primary key columns.", nameof(originalKeyValues));
            }

            var sqlBuilder = new StringBuilder($"UPDATE [{_tableSchema.SchemaName}].[{_tableSchema.TableName}] SET ");
            sqlBuilder.Append(string.Join(", ", updateColumns.Select(c => $"[{c.ColumnName}] = @{c.ColumnName}")));

            var parameters = updateColumns
                .Select(c => SqlParameterHelper.CreateParameter($"@{c.ColumnName}", columnValues[c.ColumnName], c.GetSqlDbType()))
                .ToList();

            sqlBuilder.Append(" WHERE ");
            sqlBuilder.Append(string.Join(" AND ", _tableSchema.PrimaryKeys.Select(pk => $"[{pk.Column.ColumnName}] = @PK_{pk.Column.ColumnName}")));
            parameters.AddRange(_tableSchema.PrimaryKeys.Select(pk =>
                SqlParameterHelper.CreateParameter($"@PK_{pk.Column.ColumnName}", originalKeyValues[pk.Column.ColumnName], pk.Column.GetSqlDbType())));

            _concurrencyHandler.AddConcurrencyCheckToCommand(sqlBuilder, parameters, originalTimestamp);

            try
            {
                int rowsAffected = await _dbManager.ExecuteNonQueryAsync(sqlBuilder.ToString(), parameters);

                if (_concurrencyHandler.HasConcurrencyColumn && rowsAffected == 0)
                {
                    bool exists = await CheckRecordExistsAsync(originalKeyValues);
                    if (exists)
                    {
                        throw new DBConcurrencyException($"Update failed. The record in '{_tableSchema.FullName}' may have been modified by another user.");
                    }
                    else
                    {
                        throw new DBConcurrencyException($"Update failed. The record in '{_tableSchema.FullName}' may have been deleted by another user.");
                    }
                }
                if (rowsAffected == 0 && !_concurrencyHandler.HasConcurrencyColumn)
                {
                    FileLogger.Warning($"Update affected 0 rows for PKs {string.Join(",", originalKeyValues.Values)} in {_tableSchema.FullName}, but no concurrency column exists. Record might have been deleted.");
                    bool exists = await CheckRecordExistsAsync(originalKeyValues);
                    if (!exists)
                    {
                        throw new DataException($"Update failed. The record in '{_tableSchema.FullName}' no longer exists.");
                    }
                }

                return rowsAffected;
            }
            catch (SqlException ex)
            {
                FileLogger.Error($"Error updating record in {_tableSchema.FullName}", ex);
                throw new DataException($"Failed to update record: {ex.Message}", ex);
            }
        }

        public async Task<int> DeleteRecordAsync(Dictionary<string, object> keyValues, object originalTimestamp)
        {
            if (_tableSchema.PrimaryKeys.Count == 0)
            {
                throw new InvalidOperationException($"Cannot delete from table '{_tableSchema.FullName}' as it has no primary key defined.");
            }
            if (keyValues == null || !_tableSchema.PrimaryKeys.All(pk => keyValues.ContainsKey(pk.Column.ColumnName)))
            {
                throw new ArgumentException("Key values dictionary is null or missing values for one or more primary key columns.", nameof(keyValues));
            }

            var sqlBuilder = new StringBuilder($"DELETE FROM [{_tableSchema.SchemaName}].[{_tableSchema.TableName}]");
            var parameters = new List<SqlParameter>();

            sqlBuilder.Append(" WHERE ");
            sqlBuilder.Append(string.Join(" AND ", _tableSchema.PrimaryKeys.Select(pk => $"[{pk.Column.ColumnName}] = @PK_{pk.Column.ColumnName}")));
            parameters.AddRange(_tableSchema.PrimaryKeys.Select(pk =>
                SqlParameterHelper.CreateParameter($"@PK_{pk.Column.ColumnName}", keyValues[pk.Column.ColumnName], pk.Column.GetSqlDbType())));

            _concurrencyHandler.AddConcurrencyCheckToCommand(sqlBuilder, parameters, originalTimestamp);

            try
            {
                int rowsAffected = await _dbManager.ExecuteNonQueryAsync(sqlBuilder.ToString(), parameters);

                if (_concurrencyHandler.HasConcurrencyColumn && rowsAffected == 0)
                {
                    bool exists = await CheckRecordExistsAsync(keyValues);
                    if (exists)
                    {
                        throw new DBConcurrencyException($"Delete failed. The record in '{_tableSchema.FullName}' may have been modified by another user.");
                    }
                    else
                    {
                        FileLogger.Warning($"Delete affected 0 rows for PKs {string.Join(",", keyValues.Values)} in {_tableSchema.FullName}. Record may have already been deleted.");
                        return 0;
                    }
                }
                if (rowsAffected == 0 && !_concurrencyHandler.HasConcurrencyColumn)
                {
                    FileLogger.Warning($"Delete affected 0 rows for PKs {string.Join(",", keyValues.Values)} in {_tableSchema.FullName}, but no concurrency column exists. Record might have been deleted already.");
                    bool exists = await CheckRecordExistsAsync(keyValues);
                    if (!exists) return 0;
                }

                return rowsAffected;
            }
            catch (SqlException ex)
            {
                if (ex.Number == 547)
                {
                    FileLogger.Error($"Error deleting record from {_tableSchema.FullName} due to foreign key constraint.", ex);
                    throw new DataException($"Cannot delete record. It is referenced by data in other tables.", ex);
                }
                FileLogger.Error($"Error deleting record from {_tableSchema.FullName}", ex);
                throw new DataException($"Failed to delete record: {ex.Message}", ex);
            }
        }

        private async Task<bool> CheckRecordExistsAsync(Dictionary<string, object> keyValues)
        {
            if (_tableSchema.PrimaryKeys.Count == 0 || keyValues == null || !_tableSchema.PrimaryKeys.All(pk => keyValues.ContainsKey(pk.Column.ColumnName)))
            {
                return false;
            }

            var sqlBuilder = new StringBuilder($"SELECT COUNT(*) FROM {_tableSchema.FullName} WHERE ");
            sqlBuilder.Append(string.Join(" AND ", _tableSchema.PrimaryKeys.Select(pk => $"[{pk.Column.ColumnName}] = @PK_{pk.Column.ColumnName}")));

            var parameters = _tableSchema.PrimaryKeys.Select(pk =>
                SqlParameterHelper.CreateParameter($"@PK_{pk.Column.ColumnName}", keyValues[pk.Column.ColumnName], pk.Column.GetSqlDbType())).ToList();

            try
            {
                object result = await _dbManager.ExecuteScalarAsync(sqlBuilder.ToString(), parameters);
                return Convert.ToInt32(result ?? 0) > 0;
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Error checking record existence in {_tableSchema.FullName}", ex);
                return false;
            }
        }
    }
}
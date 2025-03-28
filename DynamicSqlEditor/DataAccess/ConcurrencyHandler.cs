// File: DynamicSqlEditor/DataAccess/ConcurrencyHandler.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using DynamicSqlEditor.Common;
using DynamicSqlEditor.Schema.Models;

namespace DynamicSqlEditor.DataAccess
{
    public class ConcurrencyHandler
    {
        private readonly ColumnSchema _timestampColumn;

        public bool HasConcurrencyColumn => _timestampColumn != null;

        public ConcurrencyHandler(TableSchema tableSchema)
        {
            _timestampColumn = tableSchema?.Columns.FirstOrDefault(c => c.IsTimestamp);
        }

        public void AddConcurrencyCheckToCommand(StringBuilder sqlBuilder, List<SqlParameter> parameters, object originalTimestampValue)
        {
            if (!HasConcurrencyColumn) return;

            if (originalTimestampValue == null || originalTimestampValue == DBNull.Value)
            {
                // This case is tricky. If the original timestamp was null (e.g., newly inserted row not refreshed?),
                // we cannot perform a reliable concurrency check based on timestamp.
                // Options:
                // 1. Throw an exception: Safest, forces refresh before edit/delete.
                // 2. Skip timestamp check: Riskier, might overwrite changes.
                // 3. Check if timestamp IS NULL: Only works if it's possible for it to be null (unlikely for rowversion).
                FileLogger.Warning($"Concurrency check skipped for table '{_timestampColumn.ParentTable.FullName}' because original timestamp value was null.");
                // For now, we skip the check if the original value is null. Consider throwing instead.
                return;
                // throw new InvalidOperationException("Cannot perform concurrency check: Original timestamp value is missing.");
            }

            // Ensure WHERE clause exists before adding AND
            string sql = sqlBuilder.ToString();
            // Use IndexOf with StringComparison instead of Contains with two arguments
            if (sql.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase) == -1) // Corrected check
            {
                // This should not happen if called after PK checks are added
                FileLogger.Error("Concurrency check attempted before WHERE clause was added.");
                throw new InvalidOperationException("WHERE clause missing before adding concurrency check.");
            }

            // Append the timestamp check condition
            sqlBuilder.Append($" AND [{_timestampColumn.ColumnName}] = @Original_{_timestampColumn.ColumnName}");

            // Add the timestamp parameter
            parameters.Add(SqlParameterHelper.CreateParameter($"@Original_{_timestampColumn.ColumnName}", originalTimestampValue, _timestampColumn.GetSqlDbType()));
        }

        public object GetTimestampValue(DataRowView rowView)
        {
            if (!HasConcurrencyColumn || rowView == null || !rowView.Row.Table.Columns.Contains(_timestampColumn.ColumnName))
            {
                return null;
            }
            return rowView[_timestampColumn.ColumnName];
        }

        public object GetTimestampValue(Dictionary<string, object> data)
        {
            if (!HasConcurrencyColumn || data == null || !data.ContainsKey(_timestampColumn.ColumnName))
            {
                return null;
            }
            return data[_timestampColumn.ColumnName];
        }
    }
}
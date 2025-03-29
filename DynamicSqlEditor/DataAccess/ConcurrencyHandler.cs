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
                FileLogger.Warning($"Concurrency check skipped for table '{_timestampColumn.ParentTable.FullName}' because original timestamp value was null.");
                return;
            }

            string sql = sqlBuilder.ToString();
            if (sql.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase) == -1)
            {
                FileLogger.Error("Concurrency check attempted before WHERE clause was added.");
                throw new InvalidOperationException("WHERE clause missing before adding concurrency check.");
            }

            sqlBuilder.Append($" AND [{_timestampColumn.ColumnName}] = @Original_{_timestampColumn.ColumnName}");
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

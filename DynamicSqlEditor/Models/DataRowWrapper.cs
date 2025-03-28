using System;
using System.Collections.Generic;
using System.Data;

namespace DynamicSqlEditor.Models
{
    // Optional helper class if direct DataRow access is cumbersome
    public class DataRowWrapper
    {
        private readonly DataRow _dataRow;

        public DataRowWrapper(DataRow dataRow)
        {
            _dataRow = dataRow ?? throw new ArgumentNullException(nameof(dataRow));
        }

        public object this[string columnName]
        {
            get
            {
                if (_dataRow.Table.Columns.Contains(columnName))
                {
                    return _dataRow[columnName];
                }
                // Handle case where column might not exist (e.g., custom query)
                return null;
            }
            set
            {
                 if (_dataRow.Table.Columns.Contains(columnName))
                {
                    _dataRow[columnName] = value ?? DBNull.Value;
                }
                 // Handle or throw if column doesn't exist?
            }
        }

        public T GetValue<T>(string columnName, T defaultValue = default)
        {
            try
            {
                object value = this[columnName];
                if (value == null || value == DBNull.Value)
                {
                    return defaultValue;
                }
                // Handle potential conversion errors
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn col in _dataRow.Table.Columns)
            {
                dict[col.ColumnName] = _dataRow[col];
            }
            return dict;
        }
    }
}
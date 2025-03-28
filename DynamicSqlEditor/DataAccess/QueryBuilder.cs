// File: DynamicSqlEditor/DataAccess/QueryBuilder.cs
using System;
using System.Collections.Generic;
using System.Data; // Required for SqlDbType
using System.Data.SqlClient; // Required for SqlParameter
using System.Linq;
using System.Text;
using System.Windows.Forms; // Contains the desired SortOrder enum
using DynamicSqlEditor.Common;
using DynamicSqlEditor.Configuration.Models;
using DynamicSqlEditor.Schema.Models;

namespace DynamicSqlEditor.DataAccess
{
    public class QueryBuilder
    {
        private readonly TableSchema _tableSchema;
        private readonly TableConfig _tableConfig;

        public QueryBuilder(TableSchema tableSchema, TableConfig tableConfig)
        {
            _tableSchema = tableSchema ?? throw new ArgumentNullException(nameof(tableSchema));
            _tableConfig = tableConfig ?? throw new ArgumentNullException(nameof(tableConfig));
        }

        public string GetSelectQuery()
        {
            if (!string.IsNullOrWhiteSpace(_tableConfig.CustomSelectQuery))
            {
                // Validate custom query contains required placeholders
                if (!_tableConfig.CustomSelectQuery.Contains(Constants.WherePlaceholder) ||
                    !_tableConfig.CustomSelectQuery.Contains(Constants.OrderByPlaceholder) ||
                    !_tableConfig.CustomSelectQuery.Contains(Constants.PagingPlaceholder))
                {
                    throw new InvalidOperationException($"CustomSelectQuery for table '{_tableSchema.SchemaName}.{_tableSchema.TableName}' must contain placeholders: {Constants.WherePlaceholder}, {Constants.OrderByPlaceholder}, {Constants.PagingPlaceholder}");
                }
                return _tableConfig.CustomSelectQuery;
            }
            else
            {
                // Build default query
                var selectableColumns = _tableSchema.Columns
                    .Where(c => !IsComplexType(c.DataType)) // Exclude complex types by default
                    .Select(c => $"[{c.ColumnName}]");

                return $"SELECT {string.Join(", ", selectableColumns)} FROM [{_tableSchema.SchemaName}].[{_tableSchema.TableName}] {Constants.WherePlaceholder} {Constants.OrderByPlaceholder} {Constants.PagingPlaceholder}";
            }
        }

        public string GetWhereClause(FilterDefinition filter)
        {
            return filter?.WhereClause; // Return null if no filter
        }

        // Use fully qualified name for the parameter type
        public string GetOrderByClause(string sortColumn, System.Windows.Forms.SortOrder sortDirection)
        {
            if (string.IsNullOrWhiteSpace(sortColumn))
            {
                // Default to PK if no sort specified
                if (_tableSchema.PrimaryKeys.Any())
                {
                    sortColumn = _tableSchema.PrimaryKeys.First().Column.ColumnName; // Use Column property of PrimaryKeySchema
                                                                                     // Use fully qualified name for the value
                    sortDirection = System.Windows.Forms.SortOrder.Ascending;
                }
                else
                {
                    // If no PK, try the first column as a last resort? Or return null?
                    // Returning null might break paging depending on implementation.
                    // Let's try the first column if available.
                    var firstCol = _tableSchema.Columns.OrderBy(c => c.OrdinalPosition).FirstOrDefault();
                    if (firstCol != null)
                    {
                        sortColumn = firstCol.ColumnName;
                        // Use fully qualified name for the value
                        sortDirection = System.Windows.Forms.SortOrder.Ascending;
                        FileLogger.Warning($"No primary key or explicit sort for {_tableSchema.DisplayName}. Defaulting sort to first column: {sortColumn}. Paging might be unreliable.");
                    }
                    else
                    {
                        FileLogger.Error($"Cannot determine sort order for {_tableSchema.DisplayName}. No PK and no columns found.");
                        return null; // Cannot sort if no column and no PK
                    }
                }
            }

            // Basic validation: Check if sortColumn exists in the schema (or could be from custom query)
            // For simplicity, we assume it's valid here. Robust validation would check against actual selectable columns.
            // Need to handle potential aliases from CustomSelectQuery if sortColumn doesn't match schema directly.
            // Use fully qualified name for the comparison
            string direction = sortDirection == System.Windows.Forms.SortOrder.Descending ? "DESC" : "ASC";

            // Attempt to quote if not already quoted and contains spaces or special chars (basic)
            // A more robust solution would parse the column name properly.
            string quotedSortColumn = sortColumn;
            if (!sortColumn.StartsWith("[") && !sortColumn.EndsWith("]") && (sortColumn.Contains(" ") || sortColumn.Contains("-"))) // Basic check
            {
                quotedSortColumn = $"[{sortColumn}]";
            }
            // If it's already quoted or a simple name, use as is.

            return $"{quotedSortColumn} {direction}";
        }


        public List<SqlParameter> GetFilterParameters(FilterDefinition filter, Dictionary<string, object> inputValues)
        {
            var parameters = new List<SqlParameter>();
            if (filter == null || string.IsNullOrEmpty(filter.RequiresInput) || inputValues == null)
            {
                return parameters;
            }

            // Simple parsing: Assume "ParamName:LookupType" or just "ParamName"
            // And inputValues dictionary keys match ParamName
            string[] requiredParams = filter.RequiresInput.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string reqParamInfo in requiredParams)
            {
                string paramName = reqParamInfo.Split(':')[0].Trim();
                string sqlParamName = paramName.StartsWith("@") ? paramName : "@" + paramName;

                if (inputValues.TryGetValue(paramName, out object value))
                {
                    // Attempt to infer SqlDbType based on the column the parameter likely relates to.
                    // This is heuristic and might be incorrect, especially with custom filters.
                    // A better approach might involve storing expected type in filter config.
                    SqlDbType? dbType = InferDbTypeFromParameterName(paramName);
                    parameters.Add(SqlParameterHelper.CreateParameter(sqlParamName, value, dbType));
                }
                else
                {
                    // This shouldn't happen if UI ensures values are provided, but log if it does.
                    FileLogger.Warning($"Value for required filter parameter '{paramName}' not found for filter '{filter.Name}'.");
                    // Add parameter with DBNull? Or throw? Adding DBNull might break queries expecting a value.
                    parameters.Add(SqlParameterHelper.CreateParameter(sqlParamName, DBNull.Value));
                }
            }

            return parameters;
        }

        private SqlDbType? InferDbTypeFromParameterName(string paramName)
        {
            // Very basic heuristic: Try to match param name (without @) to a column name
            string columnName = paramName.StartsWith("@") ? paramName.Substring(1) : paramName;
            var matchingColumn = _tableSchema.Columns.FirstOrDefault(c => c.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));
            return matchingColumn?.GetSqlDbType(); // Returns null if no match or column found
        }


        private bool IsComplexType(string dataType)
        {
            string lowerType = dataType.ToLower();
            return lowerType == "xml" || lowerType == "geography" || lowerType == "geometry" || lowerType == "hierarchyid" || lowerType == "sql_variant";
            // Timestamp/Rowversion is handled separately usually
        }
    }
}
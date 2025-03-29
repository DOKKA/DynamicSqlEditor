// File: DynamicSqlEditor/Core/DataViewManager.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DynamicSqlEditor.Common;
using DynamicSqlEditor.Configuration.Models;
using DynamicSqlEditor.DataAccess;
using DynamicSqlEditor.Schema.Models;

namespace DynamicSqlEditor.Core
{
    public class DataViewManager
    {
        private readonly DatabaseManager _dbManager;
        private readonly TableSchema _tableSchema;
        private readonly TableConfig _tableConfig;
        private readonly QueryBuilder _queryBuilder;
        private readonly DataPager _dataPager;

        public int CurrentPage { get; private set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalRecords { get; private set; } = 0;
        public int TotalPages => (TotalRecords == 0 || PageSize <= 0) ? 1 : (int)Math.Ceiling((double)TotalRecords / PageSize);

        public string CurrentSortColumn { get; private set; }
        public System.Windows.Forms.SortOrder CurrentSortDirection { get; private set; } = System.Windows.Forms.SortOrder.Ascending;

        public FilterDefinition CurrentFilter { get; private set; }
        public Dictionary<string, object> CurrentFilterParameters { get; private set; } = new Dictionary<string, object>();

        public DatabaseManager DbManager => _dbManager;

        public DataViewManager(DatabaseManager dbManager, TableSchema tableSchema, TableConfig tableConfig)
        {
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
            _tableSchema = tableSchema ?? throw new ArgumentNullException(nameof(tableSchema));
            _tableConfig = tableConfig ?? throw new ArgumentNullException(nameof(tableConfig));

            _queryBuilder = new QueryBuilder(_tableSchema, _tableConfig);
            _dataPager = new DataPager(_dbManager);

            ApplyDefaultSort();
            ApplyDefaultFilter();
        }


        public async Task<int> GetPageNumberForRowAsync(Dictionary<string, object> primaryKeyValues)
        {
            if (primaryKeyValues == null || !primaryKeyValues.Any() || !_tableSchema.PrimaryKeys.Any())
            {
                return 1; // Cannot determine page without PKs
            }

            // Ensure all PK columns are provided
            if (!_tableSchema.PrimaryKeys.All(pk => primaryKeyValues.ContainsKey(pk.Column.ColumnName)))
            {
                FileLogger.Warning($"GetPageNumberForRowAsync: Not all primary key values provided for table {_tableSchema.FullName}.");
                return 1;
            }

            string baseQuery = _queryBuilder.GetSelectQuery();
            string orderByClause = _queryBuilder.GetOrderByClause(CurrentSortColumn, this.CurrentSortDirection);
            string whereClause = _queryBuilder.GetWhereClause(CurrentFilter); // Use current filter
            var filterParameters = _queryBuilder.GetFilterParameters(CurrentFilter, CurrentFilterParameters); // Use current filter params

            if (string.IsNullOrWhiteSpace(orderByClause))
            {
                FileLogger.Warning($"GetPageNumberForRowAsync: Cannot determine page number without a valid ORDER BY clause for table {_tableSchema.FullName}.");
                return 1; // ROW_NUMBER requires ORDER BY
            }

            // --- Build the ROW_NUMBER query to find the specific row ---
            var sb = new StringBuilder();
            var parameters = new List<SqlParameter>(filterParameters); // Copy filter params

            // Construct the WHERE clause for the target PK
            var pkWhereClauses = new List<string>();
            foreach (var pk in _tableSchema.PrimaryKeys)
            {
                string paramName = $"@TargetPK_{pk.Column.ColumnName}";
                pkWhereClauses.Add($"InnerQuery.[{pk.Column.ColumnName}] = {paramName}");
                parameters.Add(SqlParameterHelper.CreateParameter(paramName, primaryKeyValues[pk.Column.ColumnName], pk.Column.GetSqlDbType()));
            }
            string targetPkWhere = string.Join(" AND ", pkWhereClauses);

            // Remove placeholders from the inner query
            string innerQuery = baseQuery;
            if (!string.IsNullOrWhiteSpace(whereClause))
            {
                innerQuery = innerQuery.Replace(Constants.WherePlaceholder, $"WHERE {whereClause}");
            }
            else
            {
                innerQuery = innerQuery.Replace(Constants.WherePlaceholder, ""); // Remove placeholder
            }
            innerQuery = innerQuery.Replace(Constants.OrderByPlaceholder, "");
            innerQuery = innerQuery.Replace(Constants.PagingPlaceholder, "");

            // Find the SELECT list end and FROM start (simplified)
            int selectEndIndex = innerQuery.IndexOf("SELECT ", StringComparison.OrdinalIgnoreCase) + "SELECT ".Length;
            int fromIndex = innerQuery.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
            if (fromIndex == -1) throw new ArgumentException("Invalid baseSelectQuery: 'FROM' clause not found.");
            string selectList = innerQuery.Substring(selectEndIndex, fromIndex - selectEndIndex).Trim();

            sb.AppendLine("WITH NumberedRows AS (");
            sb.Append("  SELECT ");
            // Select only PK columns needed for the outer WHERE clause
            sb.Append(string.Join(", ", _tableSchema.PrimaryKeys.Select(pk => $"[{pk.Column.ColumnName}]")));
            sb.AppendLine(",");
            sb.AppendLine($"    ROW_NUMBER() OVER (ORDER BY {orderByClause}) AS RowNum");
            sb.AppendLine($"  FROM ({innerQuery}) AS InnerQuery"); // Use subquery alias
            sb.AppendLine(")");
            sb.AppendLine("SELECT RowNum");
            sb.AppendLine("FROM NumberedRows InnerQuery"); // Alias needed for PK WHERE clause
            sb.AppendLine($"WHERE {targetPkWhere}");

            try
            {
                object result = await _dbManager.ExecuteScalarAsync(sb.ToString(), parameters);
                if (result != null && result != DBNull.Value)
                {
                    long rowNum = Convert.ToInt64(result);
                    int pageNumber = (PageSize <= 0) ? 1 : (int)Math.Ceiling((double)rowNum / PageSize);
                    FileLogger.Info($"Calculated page number {pageNumber} for PKs in {_tableSchema.FullName}.");
                    return pageNumber;
                }
                else
                {
                    FileLogger.Warning($"Could not find row matching PKs in {_tableSchema.FullName} to determine page number.");
                    return 1; // Row not found (maybe filtered out?), default to page 1
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Error calculating page number for row in {_tableSchema.FullName}", ex);
                return 1; // Default to page 1 on error
            }
        }

        private void ApplyDefaultSort()
        {
            CurrentSortColumn = _tableConfig.DefaultSortColumn;
            CurrentSortDirection = (System.Windows.Forms.SortOrder)_tableConfig.DefaultSortDirection;

            if (string.IsNullOrEmpty(CurrentSortColumn) && _tableSchema.PrimaryKeys.Any())
            {
                CurrentSortColumn = _tableSchema.PrimaryKeys.First().Column.ColumnName;
                CurrentSortDirection = System.Windows.Forms.SortOrder.Ascending;
            }
            else if (string.IsNullOrEmpty(CurrentSortColumn))
            {
                var firstCol = _tableSchema.Columns.OrderBy(c => c.OrdinalPosition).FirstOrDefault();
                if (firstCol != null)
                {
                    CurrentSortColumn = firstCol.ColumnName;
                    CurrentSortDirection = System.Windows.Forms.SortOrder.Ascending;
                    FileLogger.Warning($"No primary key or default sort column specified for {_tableSchema.FullName}. Defaulting to first column '{CurrentSortColumn}'.");
                }
                else
                {
                    FileLogger.Error($"Cannot determine default sort for {_tableSchema.FullName}. No PK, no default, and no columns found.");
                }
            }
        }

        private void ApplyDefaultFilter()
        {
            if (!string.IsNullOrEmpty(_tableConfig.DefaultFilterName) &&
                _tableConfig.Filters.TryGetValue(_tableConfig.DefaultFilterName, out var defaultFilter))
            {
                if (string.IsNullOrEmpty(defaultFilter.RequiresInput))
                {
                    CurrentFilter = defaultFilter;
                }
                else
                {
                    FileLogger.Info($"Default filter '{_tableConfig.DefaultFilterName}' requires input and will not be applied automatically on load.");
                }
            }
        }

        public async Task<(DataTable Data, int TotalRecords)> LoadDataAsync(int pageNumber)
        {
            if (pageNumber < 1) pageNumber = 1;

            string baseQuery = _queryBuilder.GetSelectQuery();
            string orderByClause = _queryBuilder.GetOrderByClause(CurrentSortColumn, this.CurrentSortDirection);
            string whereClause = _queryBuilder.GetWhereClause(CurrentFilter);

            var parameters = _queryBuilder.GetFilterParameters(CurrentFilter, CurrentFilterParameters);

            try
            {
                var (data, total) = await _dataPager.GetPagedDataAsync(baseQuery, whereClause, orderByClause, parameters, pageNumber, PageSize);
                TotalRecords = total;

                int actualTotalPages = this.TotalPages;
                if (pageNumber > actualTotalPages && actualTotalPages > 0)
                {
                    CurrentPage = actualTotalPages;
                    FileLogger.Info($"Requested page {pageNumber} exceeds total pages {actualTotalPages}. Loading page {CurrentPage} instead.");
                    (data, total) = await _dataPager.GetPagedDataAsync(baseQuery, whereClause, orderByClause, parameters, CurrentPage, PageSize);
                    TotalRecords = total;
                }
                else if (actualTotalPages == 0)
                {
                    CurrentPage = 1;
                }
                else
                {
                    CurrentPage = pageNumber;
                }

                return (data, total);
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Error loading data for table {_tableSchema.FullName}", ex);
                TotalRecords = 0;
                CurrentPage = 1;
                throw;
            }
        }

        public async Task<(DataTable Data, int TotalRecords)> RefreshDataAsync()
        {
            return await LoadDataAsync(CurrentPage);
        }

        public async Task<(DataTable Data, int TotalRecords)> GoToPageAsync(int pageNumber)
        {
            if (pageNumber < 1) pageNumber = 1;
            return await LoadDataAsync(pageNumber);
        }

        public async Task<(DataTable Data, int TotalRecords)> NextPageAsync()
        {
            return await GoToPageAsync(CurrentPage + 1);
        }

        public async Task<(DataTable Data, int TotalRecords)> PreviousPageAsync()
        {
            return await GoToPageAsync(CurrentPage - 1);
        }

        public async Task<(DataTable Data, int TotalRecords)> FirstPageAsync()
        {
            return await GoToPageAsync(1);
        }

        public async Task<(DataTable Data, int TotalRecords)> LastPageAsync()
        {
            int lastPage = this.TotalPages;
            return await GoToPageAsync(lastPage > 0 ? lastPage : 1);
        }

        public async Task<(DataTable Data, int TotalRecords)> ApplySortAsync(string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
            {
                ApplyDefaultSort();
                return await LoadDataAsync(1);
            }

            if (CurrentSortColumn == columnName)
            {
                CurrentSortDirection = (CurrentSortDirection == System.Windows.Forms.SortOrder.Ascending) ? System.Windows.Forms.SortOrder.Descending : System.Windows.Forms.SortOrder.Ascending;
            }
            else
            {
                CurrentSortColumn = columnName;
                CurrentSortDirection = System.Windows.Forms.SortOrder.Ascending;
            }
            return await LoadDataAsync(1);
        }

        public async Task<(DataTable Data, int TotalRecords)> ApplyFilterAsync(FilterDefinition filter, Dictionary<string, object> parameters)
        {
            CurrentFilter = filter;
            CurrentFilterParameters = parameters ?? new Dictionary<string, object>();
            return await LoadDataAsync(1);
        }

        public async Task<(DataTable Data, int TotalRecords)> ClearFilterAsync()
        {
            CurrentFilter = null;
            CurrentFilterParameters.Clear();
            return await LoadDataAsync(1);
        }

        public async Task<DataTable> GetLookupDataAsync(FKLookupDefinition lookupConfig)
        {
            if (lookupConfig == null) throw new ArgumentNullException(nameof(lookupConfig));

            string valueCol = lookupConfig.ValueColumn ?? await GetPrimaryKeyColumnAsync(lookupConfig.ReferencedTable);
            if (string.IsNullOrEmpty(valueCol))
            {
                throw new InvalidOperationException($"Cannot determine ValueColumn for lookup on table {lookupConfig.ReferencedTable}. Specify ValueColumn in config or ensure table has a single PK.");
            }

            string displayColQuoted = $"[{lookupConfig.DisplayColumn.Trim('[', ']')}]";
            string valueColQuoted = $"[{valueCol.Trim('[', ']')}]";
            string safeReferencedTable = $"[{PARSENAME(lookupConfig.ReferencedTable, 2)}].[{PARSENAME(lookupConfig.ReferencedTable, 1)}]";

            string sql = $"SELECT DISTINCT {displayColQuoted}, {valueColQuoted} FROM {safeReferencedTable} ORDER BY {displayColQuoted}";

            try
            {
                return await _dbManager.ExecuteQueryAsync(sql, null);
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Error fetching lookup data for {lookupConfig.ReferencedTable}", ex);
                throw;
            }
        }

        public async Task<DataTable> GetRelatedDataAsync(RelatedChildDefinition relatedConfig, Dictionary<string, object> parentKeyValues)
        {
            if (relatedConfig == null) throw new ArgumentNullException(nameof(relatedConfig));
            if (parentKeyValues == null || !parentKeyValues.Any()) return new DataTable();

            string parentPkColumn = relatedConfig.ParentPKColumn;
            if (string.IsNullOrEmpty(parentPkColumn))
            {
                if (_tableSchema.PrimaryKeys.Count == 1)
                {
                    parentPkColumn = _tableSchema.PrimaryKeys.First().Column.ColumnName;
                }
                else
                {
                    throw new InvalidOperationException($"Cannot determine ParentPKColumn for relation '{relatedConfig.RelationName}'. Specify ParentPKColumn in config or ensure parent table '{_tableSchema.FullName}' has a single PK.");
                }
            }

            if (!parentKeyValues.ContainsKey(parentPkColumn))
            {
                FileLogger.Error($"Parent key dictionary does not contain the required key '{parentPkColumn}' for relation '{relatedConfig.RelationName}'.");
                return new DataTable();
            }

            string safeChildTable = $"[{PARSENAME(relatedConfig.ChildTable, 2)}].[{PARSENAME(relatedConfig.ChildTable, 1)}]";
            string childFkColQuoted = $"[{relatedConfig.ChildFKColumn.Trim('[', ']')}]";

            var sqlBuilder = new StringBuilder($"SELECT * FROM {safeChildTable}");
            sqlBuilder.Append($" WHERE {childFkColQuoted} = @ParentKeyValue");

            var parentPkColSchema = _tableSchema.GetColumn(parentPkColumn);
            var parameters = new List<SqlParameter>
            {
                SqlParameterHelper.CreateParameter("@ParentKeyValue", parentKeyValues[parentPkColumn], parentPkColSchema?.GetSqlDbType())
            };

            if (!string.IsNullOrWhiteSpace(relatedConfig.ChildFilter))
            {
                if (relatedConfig.ChildFilter.Contains("--") || relatedConfig.ChildFilter.Contains(";") || relatedConfig.ChildFilter.Contains("/*"))
                {
                    FileLogger.Error($"Potential SQL injection detected in RelatedChild.ChildFilter for relation '{relatedConfig.RelationName}'. Aborting query.");
                    throw new InvalidOperationException("Invalid characters detected in ChildFilter configuration.");
                }
                sqlBuilder.Append($" AND ({relatedConfig.ChildFilter})");
            }

            sqlBuilder.Append($" ORDER BY {childFkColQuoted}");

            try
            {
                return await _dbManager.ExecuteQueryAsync(sqlBuilder.ToString(), parameters);
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Error fetching related data for relation '{relatedConfig.RelationName}'", ex);
                throw;
            }
        }

        private async Task<string> GetPrimaryKeyColumnAsync(string fullTableName)
        {
            try
            {
                string safeSchema = PARSENAME(fullTableName, 2);
                string safeTable = PARSENAME(fullTableName, 1);

                if (safeSchema == null || safeTable == null)
                {
                    FileLogger.Error($"Invalid table name format for PK lookup: {fullTableName}");
                    return null;
                }

                string query = @"
                    SELECT TOP 1 COLUMN_NAME
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                    WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + QUOTENAME(CONSTRAINT_NAME)), 'IsPrimaryKey') = 1
                    AND TABLE_SCHEMA = @Schema AND TABLE_NAME = @Table;";
                var parameters = new List<SqlParameter> {
                    SqlParameterHelper.CreateParameter("@Schema", safeSchema),
                    SqlParameterHelper.CreateParameter("@Table", safeTable)
                };
                object result = await _dbManager.ExecuteScalarAsync(query, parameters);
                return result?.ToString();
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Failed to get primary key for table {fullTableName}", ex);
                return null;
            }
        }

        private string PARSENAME(string objectName, int part)
        {
            var parts = objectName.Split('.');
            if (part == 1)
            {
                return parts.Length > 1 ? parts[1].Trim('[', ']') : parts[0].Trim('[', ']');
            }
            if (part == 2)
            {
                return parts.Length > 1 ? parts[0].Trim('[', ']') : "dbo";
            }
            return null;
        }
    }
}
// File: DynamicSqlEditor/Core/DataViewManager.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient; // Includes System.Data.SqlClient.SortOrder
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms; // Includes System.Windows.Forms.SortOrder
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
        public int PageSize { get; set; } = 50; // Default page size
        public int TotalRecords { get; private set; } = 0;
        public int TotalPages => (TotalRecords == 0 || PageSize <= 0) ? 1 : (int)Math.Ceiling((double)TotalRecords / PageSize); // Avoid division by zero, ensure at least 1 page if 0 records

        public string CurrentSortColumn { get; private set; }
        // Explicitly qualify the type here
        public System.Windows.Forms.SortOrder CurrentSortDirection { get; private set; } = System.Windows.Forms.SortOrder.Ascending;

        public FilterDefinition CurrentFilter { get; private set; }
        public Dictionary<string, object> CurrentFilterParameters { get; private set; } = new Dictionary<string, object>();

        // Add DbManager property if needed by others (like FilterInputDialog lookup helpers)
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

        private void ApplyDefaultSort()
        {
            CurrentSortColumn = _tableConfig.DefaultSortColumn;
            // The type of _tableConfig.DefaultSortDirection is already System.Windows.Forms.SortOrder
            // The type of CurrentSortDirection is now explicitly System.Windows.Forms.SortOrder
            // So this assignment should now be valid without explicit cast/qualification here.
            CurrentSortDirection = (System.Windows.Forms.SortOrder)_tableConfig.DefaultSortDirection; // Line 49

            if (string.IsNullOrEmpty(CurrentSortColumn) && _tableSchema.PrimaryKeys.Any())
            {
                CurrentSortColumn = _tableSchema.PrimaryKeys.First().Column.ColumnName;
                CurrentSortDirection = System.Windows.Forms.SortOrder.Ascending; // Qualify here too for clarity
            }
            // Add fallback if no PK and no default sort?
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
                    // Leave CurrentSortColumn null, QueryBuilder might handle it
                }
            }
        }

        private void ApplyDefaultFilter()
        {
            if (!string.IsNullOrEmpty(_tableConfig.DefaultFilterName) &&
                _tableConfig.Filters.TryGetValue(_tableConfig.DefaultFilterName, out var defaultFilter))
            {
                if (string.IsNullOrEmpty(defaultFilter.RequiresInput)) // Only apply if no input needed initially
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
            // Don't clamp pageNumber to TotalPages here, let DataPager handle it after count

            string baseQuery = _queryBuilder.GetSelectQuery();
            // Pass the explicitly typed CurrentSortDirection
            string orderByClause = _queryBuilder.GetOrderByClause(CurrentSortColumn, this.CurrentSortDirection);
            string whereClause = _queryBuilder.GetWhereClause(CurrentFilter);

            var parameters = _queryBuilder.GetFilterParameters(CurrentFilter, CurrentFilterParameters);

            try
            {
                // Let DataPager calculate total and adjust page if needed
                var (data, total) = await _dataPager.GetPagedDataAsync(baseQuery, whereClause, orderByClause, parameters, pageNumber, PageSize);
                TotalRecords = total;

                // Now determine the correct CurrentPage based on results
                int actualTotalPages = this.TotalPages; // Use calculated property
                if (pageNumber > actualTotalPages && actualTotalPages > 0)
                {
                    CurrentPage = actualTotalPages;
                    // Reload data for the *actual* last page if the requested page was too high
                    FileLogger.Info($"Requested page {pageNumber} exceeds total pages {actualTotalPages}. Loading page {CurrentPage} instead.");
                    (data, total) = await _dataPager.GetPagedDataAsync(baseQuery, whereClause, orderByClause, parameters, CurrentPage, PageSize);
                    // TotalRecords should be the same, but update just in case
                    TotalRecords = total;
                }
                else if (actualTotalPages == 0) // Handle case where filter results in 0 records
                {
                    CurrentPage = 1;
                }
                else
                {
                    CurrentPage = pageNumber; // Requested page was valid
                }

                return (data, total);
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Error loading data for table {_tableSchema.FullName}", ex);
                TotalRecords = 0; // Reset count on error
                CurrentPage = 1;
                throw; // Re-throw to be handled by UI
            }
        }

        public async Task<(DataTable Data, int TotalRecords)> RefreshDataAsync()
        {
            // Reload data for the current page, sort, and filter
            return await LoadDataAsync(CurrentPage);
        }

        public async Task<(DataTable Data, int TotalRecords)> GoToPageAsync(int pageNumber)
        {
            // Basic validation, LoadDataAsync will handle clamping if needed after count
            if (pageNumber < 1) pageNumber = 1;
            return await LoadDataAsync(pageNumber);
        }

        public async Task<(DataTable Data, int TotalRecords)> NextPageAsync()
        {
            // Allow going past end, LoadDataAsync will clamp
            return await GoToPageAsync(CurrentPage + 1);
        }

        public async Task<(DataTable Data, int TotalRecords)> PreviousPageAsync()
        {
            return await GoToPageAsync(CurrentPage - 1); // Clamped to 1 by GoToPageAsync/LoadDataAsync
        }

        public async Task<(DataTable Data, int TotalRecords)> FirstPageAsync()
        {
            return await GoToPageAsync(1);
        }

        public async Task<(DataTable Data, int TotalRecords)> LastPageAsync()
        {
            // Calculate TotalPages based on current TotalRecords before navigating
            int lastPage = this.TotalPages;
            return await GoToPageAsync(lastPage > 0 ? lastPage : 1);
        }

        public async Task<(DataTable Data, int TotalRecords)> ApplySortAsync(string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
            {
                // Maybe reset to default sort? Or clear sort?
                // Let's reset to default defined in config or by PK
                ApplyDefaultSort();
                return await LoadDataAsync(1);
            }

            if (CurrentSortColumn == columnName)
            {
                // Toggle direction, explicitly qualify
                CurrentSortDirection = (CurrentSortDirection == System.Windows.Forms.SortOrder.Ascending) ? System.Windows.Forms.SortOrder.Descending : System.Windows.Forms.SortOrder.Ascending;
            }
            else
            {
                CurrentSortColumn = columnName;
                CurrentSortDirection = System.Windows.Forms.SortOrder.Ascending; // Default to Ascending on new column
            }
            return await LoadDataAsync(1); // Reset to first page on sort change
        }

        public async Task<(DataTable Data, int TotalRecords)> ApplyFilterAsync(FilterDefinition filter, Dictionary<string, object> parameters)
        {
            CurrentFilter = filter;
            CurrentFilterParameters = parameters ?? new Dictionary<string, object>();
            return await LoadDataAsync(1); // Reset to first page on filter change
        }

        public async Task<(DataTable Data, int TotalRecords)> ClearFilterAsync()
        {
            CurrentFilter = null;
            CurrentFilterParameters.Clear();
            return await LoadDataAsync(1); // Reset to first page
        }

        public async Task<DataTable> GetLookupDataAsync(FKLookupDefinition lookupConfig)
        {
            if (lookupConfig == null) throw new ArgumentNullException(nameof(lookupConfig));

            string valueCol = lookupConfig.ValueColumn ?? await GetPrimaryKeyColumnAsync(lookupConfig.ReferencedTable);
            if (string.IsNullOrEmpty(valueCol))
            {
                throw new InvalidOperationException($"Cannot determine ValueColumn for lookup on table {lookupConfig.ReferencedTable}. Specify ValueColumn in config or ensure table has a single PK.");
            }

            // Ensure columns are quoted correctly, especially if coming from config/heuristics
            string displayColQuoted = $"[{lookupConfig.DisplayColumn.Trim('[', ']')}]";
            string valueColQuoted = $"[{valueCol.Trim('[', ']')}]";

            // Use PARSENAME for robust table name splitting and quoting
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
            if (parentKeyValues == null || !parentKeyValues.Any()) return new DataTable(); // No parent selected

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

            // Use PARSENAME for robust table name splitting and quoting
            string safeChildTable = $"[{PARSENAME(relatedConfig.ChildTable, 2)}].[{PARSENAME(relatedConfig.ChildTable, 1)}]";
            string childFkColQuoted = $"[{relatedConfig.ChildFKColumn.Trim('[', ']')}]";


            var sqlBuilder = new StringBuilder($"SELECT * FROM {safeChildTable}");
            sqlBuilder.Append($" WHERE {childFkColQuoted} = @ParentKeyValue");

            // Get SqlDbType for the parameter if possible
            var parentPkColSchema = _tableSchema.GetColumn(parentPkColumn);
            var parameters = new List<SqlParameter>
             {
                 SqlParameterHelper.CreateParameter("@ParentKeyValue", parentKeyValues[parentPkColumn], parentPkColSchema?.GetSqlDbType())
             };

            if (!string.IsNullOrWhiteSpace(relatedConfig.ChildFilter))
            {
                // WARNING: Potential for SQL injection if ChildFilter contains dynamic parts not parameterized.
                // Assume ChildFilter is static for now. For dynamic values, parameterization is needed.
                // Basic validation: Check for comments, common injection patterns? Risky.
                if (relatedConfig.ChildFilter.Contains("--") || relatedConfig.ChildFilter.Contains(";") || relatedConfig.ChildFilter.Contains("/*"))
                {
                    FileLogger.Error($"Potential SQL injection detected in RelatedChild.ChildFilter for relation '{relatedConfig.RelationName}'. Aborting query.");
                    throw new InvalidOperationException("Invalid characters detected in ChildFilter configuration.");
                }
                sqlBuilder.Append($" AND ({relatedConfig.ChildFilter})");
            }

            sqlBuilder.Append($" ORDER BY {childFkColQuoted}"); // Default sort, could be configurable

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
            // This might require a separate schema lookup if not already cached
            // For simplicity, assume single PK for now or require explicit ValueColumn
            // A real implementation would query INFORMATION_SCHEMA.KEY_COLUMN_USAGE
            try
            {
                // Use PARSENAME for safety
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

        // Helper for safe table/schema name parsing
        private string PARSENAME(string objectName, int part)
        {
            // Basic simulation if PARSENAME isn't available or for non-SQL source
            // Assumes "Schema.Table" or just "Table" format
            var parts = objectName.Split('.');
            if (part == 1) // Table Name
            {
                return parts.Length > 1 ? parts[1].Trim('[', ']') : parts[0].Trim('[', ']');
            }
            if (part == 2) // Schema Name
            {
                return parts.Length > 1 ? parts[0].Trim('[', ']') : "dbo"; // Default schema if not specified
            }
            return null; // Invalid part
        }
    }
}
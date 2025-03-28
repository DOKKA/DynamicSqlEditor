using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicSqlEditor.Common;

namespace DynamicSqlEditor.DataAccess
{
    public class DataPager
    {
        private readonly DatabaseManager _dbManager;
        private bool? _supportsOffsetFetch = null;

        public DataPager(DatabaseManager dbManager)
        {
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
        }

        private async Task<bool> CheckOffsetFetchSupportAsync()
        {
            if (_supportsOffsetFetch.HasValue) return _supportsOffsetFetch.Value;

            try
            {
                // Simple query using OFFSET FETCH to check syntax support
                string testQuery = "SELECT 1 ORDER BY (SELECT NULL) OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY;";
                await _dbManager.ExecuteScalarAsync(testQuery, null);
                _supportsOffsetFetch = true;
                FileLogger.Info("Database supports OFFSET/FETCH paging.");
            }
            catch (SqlException ex)
            {
                // Any SQL exception means it's not supported
                _supportsOffsetFetch = false;
                FileLogger.Info($"Database does not support OFFSET/FETCH paging (SQL error {ex.Number}). Falling back to ROW_NUMBER().");
            }
            catch (Exception ex)
            {
                FileLogger.Error("Error checking OFFSET/FETCH support. Assuming not supported.", ex);
                _supportsOffsetFetch = false; // Assume not supported on other errors
            }
            return _supportsOffsetFetch.Value;
        }

        public async Task<(DataTable Data, int TotalRecords)> GetPagedDataAsync(
            string baseSelectQuery,
            string whereClause,
            string orderByClause,
            List<SqlParameter> parameters,
            int pageNumber,
            int pageSize)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 1; // Avoid division by zero or invalid fetch

            parameters = parameters ?? new List<SqlParameter>();

            // --- 1. Get Total Record Count ---
            string countQuery = BuildCountQuery(baseSelectQuery, whereClause);
            object totalRecordsObj = await _dbManager.ExecuteScalarAsync(countQuery, parameters);
            int totalRecords = Convert.ToInt32(totalRecordsObj ?? 0);

            if (totalRecords == 0)
            {
                return (new DataTable(), 0); // No records, return empty table
            }

            // --- 2. Get Paged Data ---
            string pagedQuery;
            bool useOffsetFetch = await CheckOffsetFetchSupportAsync();

            if (useOffsetFetch)
            {
                pagedQuery = BuildOffsetFetchQuery(baseSelectQuery, whereClause, orderByClause, pageNumber, pageSize);
            }
            else
            {
                pagedQuery = BuildRowNumberQuery(baseSelectQuery, whereClause, orderByClause, pageNumber, pageSize);
            }

            // Add paging parameters (might be reused if names are consistent)
            if (!parameters.Any(p => p.ParameterName == "@PageSize"))
                parameters.Add(SqlParameterHelper.CreateParameter("@PageSize", pageSize));
            if (!parameters.Any(p => p.ParameterName == "@Offset"))
                parameters.Add(SqlParameterHelper.CreateParameter("@Offset", (pageNumber - 1) * pageSize));
             if (!useOffsetFetch) // ROW_NUMBER needs start/end row numbers
             {
                 if (!parameters.Any(p => p.ParameterName == "@StartRow"))
                    parameters.Add(SqlParameterHelper.CreateParameter("@StartRow", (pageNumber - 1) * pageSize + 1));
                 if (!parameters.Any(p => p.ParameterName == "@EndRow"))
                    parameters.Add(SqlParameterHelper.CreateParameter("@EndRow", pageNumber * pageSize));
             }


            DataTable data = await _dbManager.ExecuteQueryAsync(pagedQuery, parameters);

            return (data, totalRecords);
        }

        private string BuildCountQuery(string baseSelectQuery, string whereClause)
        {
             // Basic approach: Replace SELECT list with COUNT(*)
             // More robust parsing might be needed for complex SELECTs (e.g., with subqueries in select list)
             int fromIndex = baseSelectQuery.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
             if (fromIndex == -1) throw new ArgumentException("Invalid baseSelectQuery: 'FROM' clause not found.");

             string fromAndWhere = baseSelectQuery.Substring(fromIndex);
             if (!string.IsNullOrWhiteSpace(whereClause))
             {
                 // Check if base query already has WHERE
                 int existingWhereIndex = fromAndWhere.IndexOf(Constants.WherePlaceholder, StringComparison.OrdinalIgnoreCase);
                 if (existingWhereIndex != -1)
                 {
                     fromAndWhere = fromAndWhere.Replace(Constants.WherePlaceholder, $"WHERE {whereClause}");
                 }
                 else // Append if no placeholder
                 {
                      // Need to check if the base query *itself* might contain a WHERE clause already
                      // This simple replacement assumes {WHERE} is the only place it goes.
                      // A safer approach might involve more complex SQL parsing or requiring {WHERE}
                      if (!fromAndWhere.TrimEnd().EndsWith("WHERE", StringComparison.OrdinalIgnoreCase)) // Avoid double WHERE
                      {
                           fromAndWhere += $" WHERE {whereClause}";
                      }
                      else
                      {
                           fromAndWhere += $" {whereClause}"; // Append condition if base ends with WHERE
                      }
                 }
             }
             else
             {
                 // Remove placeholder if no filter applied
                 fromAndWhere = fromAndWhere.Replace(Constants.WherePlaceholder, "");
             }

             // Remove ORDER BY and PAGING placeholders if they exist in the count context
             fromAndWhere = fromAndWhere.Replace(Constants.OrderByPlaceholder, "");
             fromAndWhere = fromAndWhere.Replace(Constants.PagingPlaceholder, "");


             return $"SELECT COUNT(*) {fromAndWhere}";
        }

        private string BuildOffsetFetchQuery(string baseSelectQuery, string whereClause, string orderByClause, int pageNumber, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(orderByClause))
            {
                throw new InvalidOperationException("ORDER BY clause is required for OFFSET/FETCH paging.");
            }

            string pagingClause = $"OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            string query = baseSelectQuery;

            if (!string.IsNullOrWhiteSpace(whereClause))
            {
                 query = query.Replace(Constants.WherePlaceholder, $"WHERE {whereClause}");
            }
            else
            {
                 query = query.Replace(Constants.WherePlaceholder, ""); // Remove placeholder
            }

            query = query.Replace(Constants.OrderByPlaceholder, $"ORDER BY {orderByClause}");
            query = query.Replace(Constants.PagingPlaceholder, pagingClause);

            // Ensure placeholders were present and replaced
            if (!query.Contains(pagingClause)) query += " " + pagingClause; // Append if placeholder missing
            if (!query.Contains($"ORDER BY {orderByClause}")) query += $" ORDER BY {orderByClause} {pagingClause}"; // Append order by + paging if missing


            return query;
        }

        private string BuildRowNumberQuery(string baseSelectQuery, string whereClause, string orderByClause, int pageNumber, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(orderByClause))
            {
                throw new InvalidOperationException("ORDER BY clause is required for ROW_NUMBER() paging.");
            }

            // Remove placeholders from the inner query first
            string innerQuery = baseSelectQuery;
             if (!string.IsNullOrWhiteSpace(whereClause))
            {
                 innerQuery = innerQuery.Replace(Constants.WherePlaceholder, $"WHERE {whereClause}");
            }
            else
            {
                 innerQuery = innerQuery.Replace(Constants.WherePlaceholder, ""); // Remove placeholder
            }
            // Remove ORDER BY and PAGING from inner as they are handled by ROW_NUMBER()
            innerQuery = innerQuery.Replace(Constants.OrderByPlaceholder, "");
            innerQuery = innerQuery.Replace(Constants.PagingPlaceholder, "");


            // Find the SELECT list end and FROM start
            int selectEndIndex = innerQuery.IndexOf("SELECT ", StringComparison.OrdinalIgnoreCase) + "SELECT ".Length;
            int fromIndex = innerQuery.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
            if (fromIndex == -1) throw new ArgumentException("Invalid baseSelectQuery: 'FROM' clause not found.");

            string selectList = innerQuery.Substring(selectEndIndex, fromIndex - selectEndIndex).Trim();
            string fromClause = innerQuery.Substring(fromIndex);

            // Construct the ROW_NUMBER query
            var sb = new StringBuilder();
            sb.AppendLine("WITH PagedData AS (");
            sb.Append("  SELECT ");
            // Handle SELECT * - needs expansion or alias
            if (selectList.Trim() == "*")
            {
                 sb.Append("InnerQuery.*"); // Assuming no column name conflicts
            }
            else
            {
                 sb.Append(selectList);
            }
            sb.AppendLine(",");
            sb.AppendLine($"    ROW_NUMBER() OVER (ORDER BY {orderByClause}) AS RowNum");
            // Use a subquery alias to handle potential SELECT *
            sb.AppendLine($"  FROM ({innerQuery}) AS InnerQuery");
            sb.AppendLine(")");
            sb.AppendLine("SELECT *"); // Select all columns including RowNum initially, or list explicitly
            // Could refine to exclude RowNum: SELECT col1, col2... FROM PagedData WHERE RowNum BETWEEN...
            sb.AppendLine("FROM PagedData");
            sb.AppendLine("WHERE RowNum BETWEEN @StartRow AND @EndRow");
            sb.AppendLine($"ORDER BY RowNum;"); // Order by RowNum to maintain sequence

            return sb.ToString();
        }
    }
}
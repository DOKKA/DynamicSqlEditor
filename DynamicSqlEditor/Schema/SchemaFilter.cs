// File: DynamicSqlEditor/Schema/SchemaFilter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions; // For wildcard matching
using DynamicSqlEditor.Configuration.Models;
using DynamicSqlEditor.Schema.Models;

namespace DynamicSqlEditor.Schema
{
    public static class SchemaFilter
    {
        public static List<TableSchema> FilterTables(List<TableSchema> allTables, GlobalConfig globalConfig)
        {
            if (globalConfig == null)
            {
                return allTables; // No config, return all
            }

            var filteredTables = allTables;

            // 1. Apply IncludeSchemas (if specified)
            if (globalConfig.IncludeSchemas != null && globalConfig.IncludeSchemas.Any())
            {
                filteredTables = filteredTables.Where(table =>
                    globalConfig.IncludeSchemas.Any(includePattern =>
                        MatchesWildcard(table.SchemaName, includePattern))
                ).ToList();
            }

            // 2. Apply ExcludeTables (after includes)
            if (globalConfig.ExcludeTables != null && globalConfig.ExcludeTables.Any())
            {
                filteredTables = filteredTables.Where(table =>
                    !globalConfig.ExcludeTables.Any(excludePattern =>
                        MatchesWildcard($"{table.SchemaName}.{table.TableName}", excludePattern)) // Match against Schema.Table
                ).ToList();
            }

            return filteredTables;
        }

        // Simple wildcard matching supporting *, ?, %
        private static bool MatchesWildcard(string text, string pattern)
        {
            // Convert SQL wildcards to Regex pattern
            // Escape regex special characters except for our wildcards
            string regexPattern = Regex.Escape(pattern)
                                     .Replace(@"\*", ".*")   // * => .* (match zero or more characters)
                                     .Replace(@"\?", ".")    // ? => . (match exactly one character)
                                     .Replace(@"%", ".*");   // % => .* (SQL % is like *)

            // Use Regex for matching (case-insensitive)
            try
            {
                return Regex.IsMatch(text, $"^{regexPattern}$", RegexOptions.IgnoreCase);
            }
            catch (ArgumentException ex)
            {
                // Log invalid pattern from config?
                Common.FileLogger.Warning($"Invalid wildcard pattern '{pattern}' in configuration: {ex.Message}");
                return false; // Treat invalid pattern as non-matching
            }
        }
    }
}
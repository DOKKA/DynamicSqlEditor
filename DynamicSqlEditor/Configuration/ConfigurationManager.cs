// File: DynamicSqlEditor/Configuration/ConfigurationManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms; // <-- Add this using directive for SortOrder
using DynamicSqlEditor.Common;
using DynamicSqlEditor.Configuration.Models;

namespace DynamicSqlEditor.Configuration
{
    public class ConfigurationManager
    {
        private readonly DslParser _parser = new DslParser();
        public AppConfig CurrentConfig { get; private set; } = new AppConfig();
        public List<string> ParsingErrors { get; private set; } = new List<string>();

        public TableConfig GetTableConfig(string schemaName, string tableName)
        {
            string fullTableName = $"{schemaName}.{tableName}";
            // Use CurrentConfig which holds the loaded configuration
            CurrentConfig.Tables.TryGetValue(fullTableName, out var config);
            // Return a new default config if not found, ensuring it's never null
            return config ?? new TableConfig(fullTableName);
        }

        public bool LoadConfiguration(string databaseName = null)
        {
            ParsingErrors.Clear();
            // Create new instances to ensure merging doesn't carry over old state incorrectly
            CurrentConfig = new AppConfig();
            _parser.ClearInternalState(); // Add a method to clear parser state if needed

            string baseConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.ConfigDirectory, Constants.DefaultConfigFileName);
            string dbSpecificConfigPath = null;
            if (!string.IsNullOrEmpty(databaseName))
            {
                dbSpecificConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.ConfigDirectory, $"{databaseName}.dsl");
            }

            // Load base first, then merge DB specific over it
            bool baseLoaded = LoadAndParseFile(baseConfigPath, false); // Don't merge yet

            // Create a new parser instance or clear state before parsing the second file
            // to ensure settings are merged correctly if the DslParser keeps state.
            // Assuming DslParser's Parse method handles merging or overwriting correctly when called multiple times.
            // If not, the logic needs adjustment here to load into separate DslParser instances and merge manually.

            bool dbSpecificLoaded = false;
            if (dbSpecificConfigPath != null && File.Exists(dbSpecificConfigPath))
            {
                // Parse the DB-specific file, letting the parser merge/overwrite
                dbSpecificLoaded = LoadAndParseFile(dbSpecificConfigPath, true); // Indicate merge
            }

            if (!baseLoaded && !dbSpecificLoaded)
            {
                FileLogger.Warning($"No configuration files found or loaded ({baseConfigPath}, {dbSpecificConfigPath}). Using defaults.");
                // Allow continuing with defaults, but log it.
            }

            ProcessParsedData();

            if (ParsingErrors.Any())
            {
                FileLogger.Warning("Configuration loading completed with errors:");
                foreach (var error in ParsingErrors)
                {
                    FileLogger.Warning($"- {error}");
                }
            }
            else
            {
                FileLogger.Info("Configuration loaded successfully.");
            }

            // Return true only if no critical errors (ignore warnings like duplicates)
            return !ParsingErrors.Any(e => !e.Contains("Duplicate key") && !e.StartsWith("Unknown key") && !e.StartsWith("Unknown configuration section"));
        }

        // Modified LoadAndParseFile to potentially handle merging state if parser doesn't
        private bool LoadAndParseFile(string filePath, bool merge)
        {
            if (!File.Exists(filePath)) return false;

            FileLogger.Info($"Attempting to load configuration from: {filePath}");
            // If DslParser doesn't merge internally, create a temp parser here
            // DslParser tempParser = new DslParser();
            // bool success = tempParser.Parse(filePath);
            // Merge tempParser._sections into _parser._sections manually here if needed
            bool success = _parser.Parse(filePath); // Assuming Parse handles merging/overwriting
            ParsingErrors.AddRange(_parser.Errors);
            return success;
        }

        private void ProcessParsedData()
        {
            // Ensure base structures exist
            if (CurrentConfig.Connection == null) CurrentConfig.Connection = new ConnectionConfig();
            if (CurrentConfig.Global == null) CurrentConfig.Global = new GlobalConfig();

            foreach (var sectionPair in _parser.GetAllSections())
            {
                string sectionName = sectionPair.Key;
                var settings = sectionPair.Value;

                try
                {
                    if (sectionName.Equals(Constants.Sections.Connection, StringComparison.OrdinalIgnoreCase))
                    {
                        ProcessConnectionSection(settings);
                    }
                    else if (sectionName.Equals(Constants.Sections.Global, StringComparison.OrdinalIgnoreCase))
                    {
                        ProcessGlobalSection(settings);
                    }
                    else if (sectionName.StartsWith(Constants.Sections.TablePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string tableName = sectionName.Substring(Constants.Sections.TablePrefix.Length);
                        ProcessTableSection(tableName, settings);
                    }
                    else
                    {
                        ParsingErrors.Add($"Unknown configuration section: [{sectionName}]");
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Error processing section [{sectionName}]: {ex.Message}";
                    ParsingErrors.Add(errorMsg);
                    FileLogger.Error(errorMsg, ex);
                }
            }

            // Apply global defaults if not set after processing all files
            if (!CurrentConfig.Global.DefaultFKDisplayHeuristic.Any())
            {
                CurrentConfig.Global.DefaultFKDisplayHeuristic = SplitCsv(Constants.DefaultFKHeuristic);
            }
        }

        private void ProcessConnectionSection(Dictionary<string, string> settings)
        {
            // Use existing or create new if null
            CurrentConfig.Connection = CurrentConfig.Connection ?? new ConnectionConfig();

            if (settings.TryGetValue(Constants.Keys.ConnectionString, out var connectionString))
                CurrentConfig.Connection.ConnectionString = connectionString;

            if (settings.TryGetValue(Constants.Keys.QueryTimeout, out var timeoutStr) && int.TryParse(timeoutStr, out int timeout))
            {
                CurrentConfig.Connection.QueryTimeout = timeout;
            }
            // Ensure default timeout if not specified
            else if (CurrentConfig.Connection.QueryTimeout <= 0)
            {
                CurrentConfig.Connection.QueryTimeout = Constants.DefaultQueryTimeout;
            }
        }

        private void ProcessGlobalSection(Dictionary<string, string> settings)
        {
            CurrentConfig.Global = CurrentConfig.Global ?? new GlobalConfig();

            if (settings.TryGetValue(Constants.Keys.IncludeSchemas, out var includeSchemas))
                CurrentConfig.Global.IncludeSchemas = SplitCsv(includeSchemas);

            if (settings.TryGetValue(Constants.Keys.ExcludeTables, out var excludeTables))
                CurrentConfig.Global.ExcludeTables = SplitCsv(excludeTables);

            if (settings.TryGetValue(Constants.Keys.DefaultFKDisplayHeuristic, out var fkHeuristic))
                CurrentConfig.Global.DefaultFKDisplayHeuristic = SplitCsv(fkHeuristic);
            // Default heuristic applied later in ProcessParsedData if still empty


            if (settings.TryGetValue(Constants.Keys.DisableCustomActionExecution, out var disableStr) && bool.TryParse(disableStr, out bool disable))
            {
                CurrentConfig.Global.DisableCustomActionExecution = disable;
            }
        }

        private void ProcessTableSection(string tableName, Dictionary<string, string> settings)
        {
            if (!CurrentConfig.Tables.TryGetValue(tableName, out var tableConfig))
            {
                tableConfig = new TableConfig(tableName);
                CurrentConfig.Tables[tableName] = tableConfig;
            }

            foreach (var kvp in settings)
            {
                string key = kvp.Key;
                string value = kvp.Value;

                try
                {
                    if (key.Equals(Constants.Keys.CustomSelectQuery, StringComparison.OrdinalIgnoreCase))
                    {
                        tableConfig.CustomSelectQuery = value;
                    }
                    else if (key.Equals(Constants.Keys.DefaultSortColumn, StringComparison.OrdinalIgnoreCase))
                    {
                        tableConfig.DefaultSortColumn = value;
                    }
                    else if (key.Equals(Constants.Keys.DefaultSortDirection, StringComparison.OrdinalIgnoreCase))
                    {
                        // Line ~171 where the error occurred
                        if (Enum.TryParse<SortOrder>(value, true, out var direction))
                        {
                            tableConfig.DefaultSortDirection = direction;
                        }
                        else
                        {
                            ParsingErrors.Add($"Invalid DefaultSortDirection '{value}' for table '{tableName}'. Use Ascending or Descending.");
                        }
                    }
                    else if (key.Equals(Constants.Keys.FilterDefault, StringComparison.OrdinalIgnoreCase))
                    {
                        var attrs = DslParser.ParseAttributes(value);
                        if (attrs.TryGetValue(Constants.Attributes.FilterName, out var filterName))
                        {
                            tableConfig.DefaultFilterName = filterName;
                        }
                        else
                        {
                            ParsingErrors.Add($"Missing '{Constants.Attributes.FilterName}' attribute for '{key}' in table '{tableName}'.");
                        }
                    }
                    else if (key.StartsWith(Constants.Keys.FilterPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string filterName = key.Substring(Constants.Keys.FilterPrefix.Length);
                        if (filterName.Equals("Default", StringComparison.OrdinalIgnoreCase)) continue; // Skip Filter.Default here
                        var filter = ParseFilterDefinition(filterName, value, tableName);
                        if (filter != null) tableConfig.Filters[filterName] = filter;
                    }
                    else if (key.StartsWith(Constants.Keys.DetailFormFieldPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string columnName = key.Substring(Constants.Keys.DetailFormFieldPrefix.Length);
                        var field = ParseDetailFormFieldDefinition(columnName, value, tableName);
                        if (field != null) tableConfig.DetailFormFields[columnName] = field;
                    }
                    else if (key.StartsWith(Constants.Keys.FKLookupPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string fkColumnName = key.Substring(Constants.Keys.FKLookupPrefix.Length);
                        var lookup = ParseFKLookupDefinition(fkColumnName, value, tableName);
                        if (lookup != null) tableConfig.FKLookups[fkColumnName] = lookup;
                    }
                    else if (key.StartsWith(Constants.Keys.ActionButtonPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string buttonName = key.Substring(Constants.Keys.ActionButtonPrefix.Length);
                        var button = ParseActionButtonDefinition(buttonName, value, tableName);
                        if (button != null) tableConfig.ActionButtons[buttonName] = button;
                    }
                    else if (key.StartsWith(Constants.Keys.RelatedChildPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string relationName = key.Substring(Constants.Keys.RelatedChildPrefix.Length);
                        var related = ParseRelatedChildDefinition(relationName, value, tableName);
                        if (related != null) tableConfig.RelatedChildren[relationName] = related;
                    }
                    else
                    {
                        ParsingErrors.Add($"Unknown key '{key}' in section '[{Constants.Sections.TablePrefix}{tableName}]'.");
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = $"Error processing key '{key}' in section '[{Constants.Sections.TablePrefix}{tableName}]': {ex.Message}";
                    ParsingErrors.Add(errorMsg);
                    FileLogger.Error(errorMsg, ex);
                }
            }
        }

        private FilterDefinition ParseFilterDefinition(string name, string value, string tableName)
        {
            var attrs = DslParser.ParseAttributes(value);
            if (!attrs.TryGetValue(Constants.Attributes.Label, out var label))
            {
                ParsingErrors.Add($"Missing '{Constants.Attributes.Label}' attribute for Filter '{name}' in table '{tableName}'.");
                return null;
            }
            if (!attrs.TryGetValue(Constants.Attributes.WhereClause, out var whereClause))
            {
                ParsingErrors.Add($"Missing '{Constants.Attributes.WhereClause}' attribute for Filter '{name}' in table '{tableName}'.");
                return null;
            }
            attrs.TryGetValue(Constants.Attributes.RequiresInput, out var requiresInput);

            return new FilterDefinition
            {
                Name = name,
                Label = label,
                WhereClause = whereClause,
                RequiresInput = requiresInput
            };
        }

        private DetailFormFieldDefinition ParseDetailFormFieldDefinition(string columnName, string value, string tableName)
        {
            var attrs = DslParser.ParseAttributes(value);
            var field = new DetailFormFieldDefinition { ColumnName = columnName };

            if (attrs.TryGetValue(Constants.Attributes.Order, out var orderStr) && int.TryParse(orderStr, out int order))
            {
                field.Order = order;
            }
            attrs.TryGetValue(Constants.Attributes.Label, out var label);
            field.Label = label; // Can be null

            if (attrs.TryGetValue(Constants.Attributes.ReadOnly, out var readOnlyStr) && bool.TryParse(readOnlyStr, out bool readOnly))
            {
                field.ReadOnly = readOnly;
            }
            if (attrs.TryGetValue(Constants.Attributes.Visible, out var visibleStr) && bool.TryParse(visibleStr, out bool visible))
            {
                field.Visible = visible;
            }
            attrs.TryGetValue(Constants.Attributes.ControlType, out var controlType);
            field.ControlType = controlType; // Can be null

            return field;
        }

        private FKLookupDefinition ParseFKLookupDefinition(string fkColumnName, string value, string tableName)
        {
            var attrs = DslParser.ParseAttributes(value);
            if (!attrs.TryGetValue(Constants.Attributes.ReferencedTable, out var referencedTable))
            {
                ParsingErrors.Add($"Missing '{Constants.Attributes.ReferencedTable}' attribute for FKLookup '{fkColumnName}' in table '{tableName}'.");
                return null;
            }
            if (!attrs.TryGetValue(Constants.Attributes.DisplayColumn, out var displayColumn))
            {
                ParsingErrors.Add($"Missing '{Constants.Attributes.DisplayColumn}' attribute for FKLookup '{fkColumnName}' in table '{tableName}'.");
                return null;
            }
            attrs.TryGetValue(Constants.Attributes.ValueColumn, out var valueColumn);
            attrs.TryGetValue(Constants.Attributes.ReferencedColumn, out var referencedColumn);

            return new FKLookupDefinition
            {
                FKColumnName = fkColumnName,
                ReferencedTable = referencedTable,
                DisplayColumn = displayColumn,
                ValueColumn = valueColumn, // Can be null
                ReferencedColumn = referencedColumn // Can be null
            };
        }

        private ActionButtonDefinition ParseActionButtonDefinition(string buttonName, string value, string tableName)
        {
            var attrs = DslParser.ParseAttributes(value);
            if (!attrs.TryGetValue(Constants.Attributes.Label, out var label))
            {
                ParsingErrors.Add($"Missing '{Constants.Attributes.Label}' attribute for ActionButton '{buttonName}' in table '{tableName}'.");
                return null;
            }
            if (!attrs.TryGetValue(Constants.Attributes.Command, out var command))
            {
                ParsingErrors.Add($"Missing '{Constants.Attributes.Command}' attribute for ActionButton '{buttonName}' in table '{tableName}'.");
                return null;
            }
            bool requiresSelection = true;
            if (attrs.TryGetValue(Constants.Attributes.RequiresSelection, out var reqSelStr) && bool.TryParse(reqSelStr, out bool reqSel))
            {
                requiresSelection = reqSel;
            }
            attrs.TryGetValue(Constants.Attributes.SuccessMessage, out var successMessage);

            return new ActionButtonDefinition
            {
                Name = buttonName,
                Label = label,
                Command = command,
                RequiresSelection = requiresSelection,
                SuccessMessage = successMessage
            };
        }

        private RelatedChildDefinition ParseRelatedChildDefinition(string relationName, string value, string tableName)
        {
            var attrs = DslParser.ParseAttributes(value);
            if (!attrs.TryGetValue(Constants.Attributes.Label, out var label))
            {
                ParsingErrors.Add($"Missing '{Constants.Attributes.Label}' attribute for RelatedChild '{relationName}' in table '{tableName}'.");
                return null;
            }
            if (!attrs.TryGetValue(Constants.Attributes.ChildTable, out var childTable))
            {
                ParsingErrors.Add($"Missing '{Constants.Attributes.ChildTable}' attribute for RelatedChild '{relationName}' in table '{tableName}'.");
                return null;
            }
            if (!attrs.TryGetValue(Constants.Attributes.ChildFKColumn, out var childFkColumn))
            {
                ParsingErrors.Add($"Missing '{Constants.Attributes.ChildFKColumn}' attribute for RelatedChild '{relationName}' in table '{tableName}'.");
                return null;
            }
            attrs.TryGetValue(Constants.Attributes.ParentPKColumn, out var parentPkColumn);
            attrs.TryGetValue(Constants.Attributes.ChildFilter, out var childFilter);

            return new RelatedChildDefinition
            {
                RelationName = relationName,
                Label = label,
                ChildTable = childTable,
                ChildFKColumn = childFkColumn,
                ParentPKColumn = parentPkColumn, // Can be null
                ChildFilter = childFilter // Can be null
            };
        }

        private List<string> SplitCsv(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return new List<string>();
            }
            return csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => s.Trim())
                      .Where(s => !string.IsNullOrEmpty(s))
                      .ToList();
        }

        // Helper for DslParser state clearing if needed
        public void ClearParserState()
        {
            _parser.ClearInternalState(); // Assuming DslParser has this method
        }
    }

    // Add this extension method to DslParser if it doesn't have a clear method
    public static class DslParserExtensions
    {
        // Add this method if DslParser needs explicit clearing
        public static void ClearInternalState(this DslParser parser)
        {
            // Access internal fields via reflection if necessary and allowed,
            // or preferably add a public Clear method to DslParser itself.
            // Example (if _sections and _errors were public/internal):
            // parser._sections.Clear();
            // parser._errors.Clear();

            // If they are private, reflection is needed (less ideal):
            var sectionsField = typeof(DslParser).GetField("_sections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var errorsField = typeof(DslParser).GetField("_errors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (sectionsField?.GetValue(parser) is IDictionary<string, Dictionary<string, string>> sections)
            {
                sections.Clear();
            }
            if (errorsField?.GetValue(parser) is List<string> errors)
            {
                errors.Clear();
            }
        }
    }
}
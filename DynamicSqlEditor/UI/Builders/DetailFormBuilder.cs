// File: DynamicSqlEditor/UI/Builders/DetailFormBuilder.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DynamicSqlEditor.Common;
using DynamicSqlEditor.Configuration.Models;
using DynamicSqlEditor.Core;
using DynamicSqlEditor.Schema.Models;
using DynamicSqlEditor.UI.Controls;

namespace DynamicSqlEditor.UI.Builders
{
    public class DetailFormBuilder
    {
        private readonly Panel _detailPanel;
        private readonly TableSchema _tableSchema;
        private readonly TableConfig _tableConfig;
        private readonly GlobalConfig _globalConfig;
        private readonly StateManager _stateManager;
        private readonly DynamicSqlEditor.Core.DataViewManager _dataViewManager;

        public DetailFormBuilder(Panel detailPanel, TableSchema tableSchema, TableConfig tableConfig, GlobalConfig globalConfig, StateManager stateManager, DynamicSqlEditor.Core.DataViewManager dataViewManager)
        {
            _detailPanel = detailPanel ?? throw new ArgumentNullException(nameof(detailPanel));
            _tableSchema = tableSchema ?? throw new ArgumentNullException(nameof(tableSchema));
            _tableConfig = tableConfig ?? throw new ArgumentNullException(nameof(tableConfig));
            _globalConfig = globalConfig ?? throw new ArgumentNullException(nameof(globalConfig));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _dataViewManager = dataViewManager ?? throw new ArgumentNullException(nameof(dataViewManager));
        }

        // Instance method now
        public async Task BuildFormAsync(EventHandler valueChangedHandler)
        {
            _detailPanel.Controls.Clear();
            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3, // Label, Control, IsNull CheckBox
                Padding = new Padding(10)
            };
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Label
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Control (takes remaining space)
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // IsNull CheckBox

            var fieldsToDisplay = GetFieldsToDisplay(); // Instance method call

            foreach (var fieldInfo in fieldsToDisplay)
            {
                var column = fieldInfo.Column;
                var fieldConfig = fieldInfo.Config;

                Label label = ControlFactory.CreateLabel(column, fieldConfig);
                Control control = ControlFactory.CreateControl(column, fieldConfig);
                CheckBox isNullCheckBox = ControlFactory.CreateIsNullCheckBox(column);

                control.Name = column.ColumnName; // Set control name for easy lookup
                control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; // Allow horizontal stretching
                control.Margin = new Padding(3);
                label.Margin = new Padding(3, 6, 3, 3); // Align label vertically with control center

                // Configure ComboBox for FKs
                if (control is ComboBox comboBox)
                {
                    ConfigureForeignKeyComboBox(comboBox, column.ColumnName); // Instance method call
                }

                // Wire up ValueChanged event
                if (control is TextBoxBase txt) txt.TextChanged += valueChangedHandler;
                else if (control is CheckBox chk && chk != isNullCheckBox) chk.CheckedChanged += valueChangedHandler;
                else if (control is DateTimePicker dtp) dtp.ValueChanged += valueChangedHandler;
                else if (control is ComboBox cmb) cmb.SelectedIndexChanged += valueChangedHandler;
                else if (control is NullableDateTimePicker ndtp) ndtp.ValueChanged += valueChangedHandler;

                // Wire up IsNull checkbox event
                if (isNullCheckBox != null)
                {
                    isNullCheckBox.Name = $"IsNullChk_{column.ColumnName}";
                    isNullCheckBox.Margin = new Padding(3, 6, 3, 3);
                    isNullCheckBox.CheckedChanged += valueChangedHandler; // Track changes on this too
                }

                // Add to layout panel
                tableLayout.RowCount++;
                tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                tableLayout.Controls.Add(label, 0, tableLayout.RowCount - 1);
                tableLayout.Controls.Add(control, 1, tableLayout.RowCount - 1);
                if (isNullCheckBox != null)
                {
                    tableLayout.Controls.Add(isNullCheckBox, 2, tableLayout.RowCount - 1);
                }
            }

            _detailPanel.Controls.Add(tableLayout);
            await Task.CompletedTask;
        }

        // Instance method
        private List<(ColumnSchema Column, DetailFormFieldDefinition Config)> GetFieldsToDisplay()
        {
            var displayList = new List<(ColumnSchema Column, DetailFormFieldDefinition Config)>();

            // Start with columns from schema
            foreach (var col in this._tableSchema.Columns.OrderBy(c => c.OrdinalPosition)) // Use instance field
            {
                this._tableConfig.DetailFormFields.TryGetValue(col.ColumnName, out var fieldConfig); // Use instance field

                // Determine visibility
                bool isVisibleByDefault = !col.IsTimestamp && !IsComplexType(col.DataType);
                bool isVisible = fieldConfig?.Visible ?? isVisibleByDefault;

                if (isVisible)
                {
                    // Use config order if specified, otherwise use schema order
                    int order = fieldConfig?.Order ?? col.OrdinalPosition + 1000; // Add offset to prioritize configured order
                    displayList.Add((col, fieldConfig ?? new DetailFormFieldDefinition { ColumnName = col.ColumnName, Order = order }));
                }
            }

            // Sort based on final Order value
            return displayList.OrderBy(item => item.Config.Order).ToList();
        }

        // Instance method
        private void ConfigureForeignKeyComboBox(ComboBox comboBox, string fkColumnName)
        {
            FKLookupDefinition lookupConfig = null;

            // 1. Check explicit FKLookup config
            if (this._tableConfig.FKLookups.TryGetValue(fkColumnName, out var explicitConfig)) // Use instance field
            {
                lookupConfig = explicitConfig;
            }
            else
            {
                // 2. Check discovered FK constraints
                var discoveredFk = this._tableSchema.GetForeignKey(fkColumnName); // Use instance field
                if (discoveredFk != null)
                {
                    // 3. Use FK Heuristics to find display column
                    string displayColumn = FindBestDisplayColumn(discoveredFk.ReferencedTable); // Instance method call
                    if (displayColumn != null)
                    {
                        lookupConfig = new FKLookupDefinition
                        {
                            FKColumnName = fkColumnName,
                            ReferencedTable = discoveredFk.ReferencedTable.FullName.Replace("[", "").Replace("]", ""), // Use Schema.Table format
                            DisplayColumn = displayColumn,
                            ValueColumn = discoveredFk.ReferencedColumn.ColumnName // Use the actual referenced column
                            // ReferencedColumn defaults correctly here
                        };
                    }
                    else
                    {
                        FileLogger.Warning($"Could not determine display column using heuristics for FK '{fkColumnName}' referencing '{discoveredFk.ReferencedTable.DisplayName}'. ComboBox will not be populated.");
                        comboBox.Enabled = false; // Disable if lookup cannot be configured
                        comboBox.Items.Add("Lookup Error");
                        return;
                    }
                }
            }

            if (lookupConfig == null)
            {
                // If ControlType=ComboBox was forced but no FK found/configured
                FileLogger.Warning($"Control for '{fkColumnName}' is ComboBox, but no FKLookup configuration or discoverable FK relationship found.");
                comboBox.Enabled = false;
                comboBox.Items.Add("Config Error");
                return;
            }

            // Populate ComboBox - Keep this part async as it involves DB access via DataViewManager
            // Use an async void lambda or a separate async helper method to load data after setup
            LoadComboBoxDataAsync(comboBox, lookupConfig, fkColumnName); // Instance method call
        }

        // Instance method
        private async void LoadComboBoxDataAsync(ComboBox comboBox, FKLookupDefinition lookupConfig, string fkColumnName)
        {
            try
            {
                comboBox.Enabled = false; // Disable while loading
                comboBox.DataSource = null; // Clear previous
                comboBox.Items.Clear();
                comboBox.Items.Add("Loading...");
                comboBox.SelectedIndex = 0;

                DataTable lookupData = await this._dataViewManager.GetLookupDataAsync(lookupConfig); // Use instance field

                // Determine ValueMember using the now synchronous helper
                string valueMember = lookupConfig.ValueColumn ?? GetPrimaryKeyColumnName(lookupConfig.ReferencedTable); // Instance method call
                if (string.IsNullOrEmpty(valueMember))
                {
                    throw new InvalidOperationException($"Could not determine ValueMember for ComboBox '{fkColumnName}'.");
                }

                comboBox.DataSource = lookupData;
                comboBox.DisplayMember = lookupConfig.DisplayColumn;
                comboBox.ValueMember = valueMember;
                comboBox.SelectedIndex = -1; // Start with no selection
                comboBox.Enabled = true; // Re-enable after loading
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Failed to populate ComboBox for FK '{fkColumnName}' using lookup '{lookupConfig.ReferencedTable}'.", ex);
                comboBox.DataSource = null;
                comboBox.Items.Clear();
                comboBox.Items.Add("Data Load Error");
                comboBox.Enabled = false; // Keep disabled on error
            }
        }

        // Instance method
        private string FindBestDisplayColumn(TableSchema referencedTable)
        {
            // Use the _globalConfig field here
            var heuristicOrder = this._globalConfig.DefaultFKDisplayHeuristic; // Use instance field

            foreach (string heuristic in heuristicOrder)
            {
                if (heuristic.Contains("*")) // Wildcard match (e.g., *ID)
                {
                    // Ensure pattern is valid regex (escape special chars if needed, but * is handled)
                    string pattern = heuristic.Replace(".", @"\.").Replace("*", ".*"); // Escape dots, convert *
                    try
                    {
                        var match = referencedTable.Columns.FirstOrDefault(c => System.Text.RegularExpressions.Regex.IsMatch(c.ColumnName, $"^{pattern}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
                        if (match != null) return match.ColumnName;
                    }
                    catch (ArgumentException regexEx)
                    {
                        FileLogger.Warning($"Invalid regex pattern generated from heuristic '{heuristic}': {regexEx.Message}");
                        // Continue to next heuristic
                    }
                }
                else // Exact match
                {
                    var match = referencedTable.Columns.FirstOrDefault(c => c.ColumnName.Equals(heuristic, StringComparison.OrdinalIgnoreCase));
                    if (match != null) return match.ColumnName;
                }
            }

            // Fallback: Use the first string column? Or the PK itself?
            // Let's try first string column
            var firstStringCol = referencedTable.Columns.FirstOrDefault(c => c.DataType.ToLower().Contains("char") || c.DataType.ToLower().Contains("text"));
            if (firstStringCol != null)
            {
                FileLogger.Info($"FK heuristic fallback: Using first string column '{firstStringCol.ColumnName}' for table '{referencedTable.DisplayName}'.");
                return firstStringCol.ColumnName;
            }

            // Final fallback: use PK if single PK
            if (referencedTable.PrimaryKeys.Count == 1)
            {
                FileLogger.Info($"FK heuristic fallback: Using single primary key column '{referencedTable.PrimaryKeys[0].Column.ColumnName}' for table '{referencedTable.DisplayName}'.");
                return referencedTable.PrimaryKeys[0].Column.ColumnName;
            }


            FileLogger.Warning($"FK heuristic failed for table '{referencedTable.DisplayName}'. No suitable display column found.");
            return null; // Return null if no heuristic matches and fallbacks fail
        }

        // Instance method
        private string GetPrimaryKeyColumnName(string fullTableName)
        {
            try
            {
                // Parse Schema.Table format
                string schema = null;
                string table = fullTableName;
                if (fullTableName.Contains("."))
                {
                    var parts = fullTableName.Split('.');
                    schema = parts[0].Trim('[', ']');
                    table = parts[1].Trim('[', ']');
                }
                else // Handle table name without schema (assume default like dbo)
                {
                    schema = "dbo"; // Or get default schema if possible
                }


                // Find the TableSchema in the StateManager's loaded list
                var referencedTableSchema = this._stateManager.AvailableTables.FirstOrDefault(t => // Use instance field
                    t.TableName.Equals(table, StringComparison.OrdinalIgnoreCase) &&
                    t.SchemaName.Equals(schema, StringComparison.OrdinalIgnoreCase)); // Ensure schema matches

                if (referencedTableSchema == null)
                {
                    FileLogger.Error($"Could not find schema information for referenced table '{fullTableName}' in StateManager.");
                    return null;
                }

                // Check if there's exactly one primary key
                if (referencedTableSchema.PrimaryKeys.Count == 1)
                {
                    return referencedTableSchema.PrimaryKeys[0].Column.ColumnName;
                }
                else if (referencedTableSchema.PrimaryKeys.Count == 0)
                {
                    FileLogger.Warning($"Referenced table '{fullTableName}' has no primary key defined in schema.");
                    return null;
                }
                else // Composite key
                {
                    FileLogger.Warning($"Referenced table '{fullTableName}' has a composite primary key. Cannot automatically determine ValueMember for ComboBox. Specify 'ValueColumn' in FKLookup config.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Error getting primary key for table {fullTableName} from schema cache", ex);
                return null;
            }
        }

        // Instance method - removed 'static' and 'Panel panel' parameter
        public void PopulateControls(DataRowView rowView)
        {
            if (rowView == null)
            {
                ClearControls(); // Call instance method
                return;
            }

            foreach (Control control in GetAllControlsRecursive(this._detailPanel)) // Use instance field
            {
                string columnName = control.Name;
                if (string.IsNullOrEmpty(columnName) || !rowView.Row.Table.Columns.Contains(columnName))
                {
                    // Handle IsNull checkbox separately
                    if (control is CheckBox isNullChk && isNullChk.Tag?.ToString() == "IsNullCheckBox")
                    {
                        string relatedColumnName = isNullChk.Name.Replace("IsNullChk_", "");
                        if (rowView.Row.Table.Columns.Contains(relatedColumnName))
                        {
                            isNullChk.Checked = rowView[relatedColumnName] == DBNull.Value;
                            var relatedControl = this._detailPanel.Controls.Find(relatedColumnName, true).FirstOrDefault(); // Use instance field
                            if (relatedControl != null) relatedControl.Enabled = !isNullChk.Checked;
                        }
                    }
                    continue;
                }

                object value = rowView[columnName];
                var columnSchema = this._tableSchema.GetColumn(columnName); // Use instance field
                this._tableConfig.DetailFormFields.TryGetValue(columnName, out var fieldConfig); // Use instance field
                bool isReadOnly = IsControlReadOnly(columnSchema, fieldConfig); // Call instance method overload

                try
                {
                    if (control is TextBoxBase txt)
                    {
                        txt.Text = value == DBNull.Value ? string.Empty : value.ToString();
                        txt.ReadOnly = isReadOnly; // Use calculated value
                    }
                    else if (control is CheckBox chk && chk.Tag?.ToString() != "IsNullCheckBox")
                    {
                        chk.Checked = value != DBNull.Value && Convert.ToBoolean(value);
                        chk.Enabled = !isReadOnly; // Use calculated value
                    }
                    else if (control is DateTimePicker dtp)
                    {
                        if (value != DBNull.Value) dtp.Value = Convert.ToDateTime(value);
                        // Handle null for standard picker? Maybe disable if null? Or rely on IsNullChk
                        dtp.Enabled = !isReadOnly; // Use calculated value
                    }
                    else if (control is NullableDateTimePicker ndtp)
                    {
                        ndtp.Value = (value == DBNull.Value) ? (DateTime?)null : Convert.ToDateTime(value);
                        ndtp.Enabled = !isReadOnly; // Use calculated value
                    }
                    else if (control is ComboBox cmb)
                    {
                        try
                        {
                            if (cmb.DataSource != null || cmb.Items.Count > 0) cmb.SelectedValue = value ?? DBNull.Value;
                            else FileLogger.Info($"ComboBox '{cmb.Name}' data source not ready when trying to set value '{value}'.");
                        }
                        catch (Exception svEx)
                        {
                            FileLogger.Warning($"Error setting SelectedValue for ComboBox '{cmb.Name}' to '{value}': {svEx.Message}");
                            cmb.SelectedIndex = -1;
                        }
                        cmb.Enabled = !isReadOnly; // Use calculated value
                    }
                    else if (control is Label lbl)
                    {
                        lbl.Text = value == DBNull.Value ? string.Empty : FormatDisplayValue(value);
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.Warning($"Error populating control '{control.Name}' for column '{columnName}': {ex.Message}");
                }
            }
        }

        // Instance method - removed 'static' and 'Panel panel' parameter
        public void ClearControls()
        {
            foreach (Control control in GetAllControlsRecursive(this._detailPanel)) // Use instance field
            {
                if (control is TextBoxBase txt) txt.Clear();
                else if (control is CheckBox chk && chk.Tag?.ToString() != "IsNullCheckBox") chk.Checked = false;
                else if (control is DateTimePicker dtp) dtp.Value = DateTime.Now;
                else if (control is NullableDateTimePicker ndtp) ndtp.Value = null;
                else if (control is ComboBox cmb) cmb.SelectedIndex = -1;
                else if (control is Label lbl && !lbl.Text.EndsWith(":")) lbl.Text = string.Empty;

                if (control is CheckBox isNullChk && isNullChk.Tag?.ToString() == "IsNullCheckBox")
                {
                    isNullChk.Checked = false;
                    var relatedControl = this._detailPanel.Controls.Find(isNullChk.Name.Replace("IsNullChk_", ""), true).FirstOrDefault(); // Use instance field
                    if (relatedControl != null) relatedControl.Enabled = true; // Re-enable related control
                }
            }
        }

        // Instance method - removed 'static' and 'Panel panel' parameter
        public Dictionary<string, object> GetControlValues()
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (Control control in GetAllControlsRecursive(this._detailPanel)) // Use instance field
            {
                string columnName = control.Name;
                if (string.IsNullOrEmpty(columnName) || values.ContainsKey(columnName)) continue;

                var isNullChk = this._detailPanel.Controls.Find($"IsNullChk_{columnName}", true).FirstOrDefault() as CheckBox; // Use instance field
                if (isNullChk != null && isNullChk.Checked)
                {
                    values[columnName] = DBNull.Value;
                    continue;
                }

                object value = null;
                if (control is TextBoxBase txt) value = string.IsNullOrEmpty(txt.Text) ? (object)DBNull.Value : txt.Text;
                else if (control is CheckBox chk && chk.Tag?.ToString() != "IsNullCheckBox") value = chk.Checked;
                else if (control is DateTimePicker dtp) value = dtp.Value;
                else if (control is NullableDateTimePicker ndtp) value = ndtp.Value.HasValue ? (object)ndtp.Value.Value : DBNull.Value;
                else if (control is ComboBox cmb) value = cmb.SelectedValue ?? DBNull.Value;

                if (value != null)
                {
                    var columnSchema = this._tableSchema.GetColumn(columnName); // Use instance field
                    if (columnSchema != null && value != DBNull.Value)
                    {
                        try
                        {
                            Type targetType = GetClrType(columnSchema.DataType);
                            if (targetType != null && value.GetType() != targetType)
                            {
                                if (targetType == typeof(Guid) && value is string sGuid && Guid.TryParse(sGuid, out Guid guidResult)) value = guidResult;
                                else if (targetType == typeof(byte[]) && value is string sBytes) value = Convert.FromBase64String(sBytes);
                                else value = Convert.ChangeType(value, targetType);
                            }
                        }
                        catch (Exception convEx) when (convEx is FormatException || convEx is InvalidCastException || convEx is OverflowException)
                        {
                            FileLogger.Warning($"Could not convert value for column '{columnName}' to target type '{columnSchema.DataType}': {convEx.Message}");
                        }
                    }
                    values[columnName] = value;
                }
            }
            return values;
        }

        // Instance method - removed 'static' and 'Panel panel' parameter
        public void SetControlsEnabled(bool enabled)
        {
            foreach (Control control in GetAllControlsRecursive(this._detailPanel)) // Use instance field
            {
                string columnName = control.Name;
                if (string.IsNullOrEmpty(columnName)) continue;

                var columnSchema = this._tableSchema.GetColumn(columnName); // Use instance field
                this._tableConfig.DetailFormFields.TryGetValue(columnName, out var fieldConfig); // Use instance field
                bool isReadOnly = IsControlReadOnly(columnSchema, fieldConfig); // Call instance method overload

                bool shouldBeEnabled = enabled && !isReadOnly;

                // Check if the current control is an IsNull checkbox
                if (control is CheckBox isNullChk && isNullChk.Tag?.ToString() == "IsNullCheckBox") // First declaration of isNullChk
                {
                    var relatedControlName = isNullChk.Name.Replace("IsNullChk_", "");
                    var relatedColumnSchema = this._tableSchema.GetColumn(relatedControlName); // Use instance field
                    this._tableConfig.DetailFormFields.TryGetValue(relatedControlName, out var relatedFieldConfig); // Use instance field
                    bool relatedIsReadOnly = IsControlReadOnly(relatedColumnSchema, relatedFieldConfig); // Call instance method overload

                    isNullChk.Enabled = enabled && !relatedIsReadOnly && (relatedColumnSchema?.IsNullable ?? false);
                }
                // If it's not an IsNull checkbox, handle the main control
                else if (!(control is Label))
                {
                    control.Enabled = shouldBeEnabled;

                    // If disabling, ensure IsNull checkbox doesn't leave main control disabled
                    if (!shouldBeEnabled)
                    {
                        // Find the corresponding IsNull checkbox again
                        // Rename the variable here to avoid conflict
                        var correspondingIsNullChk = this._detailPanel.Controls.Find($"IsNullChk_{columnName}", true).FirstOrDefault() as CheckBox; // Renamed variable
                        if (correspondingIsNullChk != null && correspondingIsNullChk.Checked) // Use renamed variable
                        {
                            // If disabling edit mode while control is nulled, keep it disabled
                            control.Enabled = false;
                        }
                    }
                }
            }
        }

        // Instance method overload (was static before)
        private bool IsControlReadOnly(ColumnSchema column, DetailFormFieldDefinition fieldConfig)
        {
            if (column == null) return true;

            if (fieldConfig?.ReadOnly.HasValue ?? false) return fieldConfig.ReadOnly.Value;

            if (column.IsIdentity || column.IsComputed || column.IsTimestamp || column.IsPrimaryKey) return true;

            return false;
        }

        // REMOVED the static overload: IsControlReadOnly(string columnName, DataRowView rowView, Panel panel)

        // Static helper - OK to keep static as it doesn't depend on instance state
        private static IEnumerable<Control> GetAllControlsRecursive(Control container)
        {
            var controls = container.Controls.Cast<Control>();
            return controls.SelectMany(ctrl => GetAllControlsRecursive(ctrl)).Concat(controls);
        }

        // Instance method (could be static, but consistent)
        private bool IsComplexType(string dataType)
        {
            string lowerType = dataType.ToLower();
            return lowerType == "xml" || lowerType == "geography" || lowerType == "geometry" || lowerType == "hierarchyid";
        }

        // Static helper - OK to keep static
        private static string FormatDisplayValue(object value)
        {
            if (value is DateTime dt) return dt.ToString(Constants.DefaultDateTimeFormat);
            return value.ToString();
        }

        // Static helper - OK to keep static
        private static Type GetClrType(string sqlDataType)
        {
            switch (sqlDataType.ToLower())
            {
                case "bigint": return typeof(long);
                case "binary": case "image": case "rowversion": case "timestamp": case "varbinary": return typeof(byte[]);
                case "bit": return typeof(bool);
                case "char": case "nchar": case "ntext": case "nvarchar": case "text": case "varchar": case "xml": return typeof(string);
                case "date": case "datetime": case "datetime2": case "smalldatetime": return typeof(DateTime);
                case "datetimeoffset": return typeof(DateTimeOffset);
                case "decimal": case "money": case "numeric": case "smallmoney": return typeof(decimal);
                case "float": return typeof(double);
                case "int": return typeof(int);
                case "real": return typeof(float);
                case "smallint": return typeof(short);
                case "time": return typeof(TimeSpan);
                case "tinyint": return typeof(byte);
                case "uniqueidentifier": return typeof(Guid);
                default: return typeof(object);
            }
        }
    }
}
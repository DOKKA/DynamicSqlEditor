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

        public async Task BuildFormAsync(EventHandler valueChangedHandler)
        {
            _detailPanel.Controls.Clear();
            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                Padding = new Padding(10)
            };
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var fieldsToDisplay = GetFieldsToDisplay();

            foreach (var fieldInfo in fieldsToDisplay)
            {
                var column = fieldInfo.Column;
                var fieldConfig = fieldInfo.Config;

                Label label = ControlFactory.CreateLabel(column, fieldConfig);
                Control control = ControlFactory.CreateControl(column, fieldConfig);
                CheckBox isNullCheckBox = ControlFactory.CreateIsNullCheckBox(column);

                control.Name = column.ColumnName;
                control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                control.Margin = new Padding(3);
                label.Margin = new Padding(3, 6, 3, 3);

                if (control is ComboBox comboBox)
                {
                    ConfigureForeignKeyComboBox(comboBox, column.ColumnName);
                }

                if (control is TextBoxBase txt) txt.TextChanged += valueChangedHandler;
                else if (control is CheckBox chk && chk != isNullCheckBox) chk.CheckedChanged += valueChangedHandler;
                else if (control is DateTimePicker dtp) dtp.ValueChanged += valueChangedHandler;
                else if (control is ComboBox cmb) cmb.SelectedIndexChanged += valueChangedHandler;
                else if (control is NullableDateTimePicker ndtp) ndtp.ValueChanged += valueChangedHandler;

                if (isNullCheckBox != null)
                {
                    isNullCheckBox.Name = $"IsNullChk_{column.ColumnName}";
                    isNullCheckBox.Margin = new Padding(3, 6, 3, 3);
                    isNullCheckBox.CheckedChanged += valueChangedHandler;
                }

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

        private List<(ColumnSchema Column, DetailFormFieldDefinition Config)> GetFieldsToDisplay()
        {
            var displayList = new List<(ColumnSchema Column, DetailFormFieldDefinition Config)>();

            foreach (var col in this._tableSchema.Columns.OrderBy(c => c.OrdinalPosition))
            {
                this._tableConfig.DetailFormFields.TryGetValue(col.ColumnName, out var fieldConfig);

                bool isVisibleByDefault = !col.IsTimestamp && !IsComplexType(col.DataType);
                bool isVisible = fieldConfig?.Visible ?? isVisibleByDefault;

                if (isVisible)
                {
                    int order = fieldConfig?.Order ?? col.OrdinalPosition + 1000;
                    displayList.Add((col, fieldConfig ?? new DetailFormFieldDefinition { ColumnName = col.ColumnName, Order = order }));
                }
            }

            return displayList.OrderBy(item => item.Config.Order).ToList();
        }

        private void ConfigureForeignKeyComboBox(ComboBox comboBox, string fkColumnName)
        {
            FKLookupDefinition lookupConfig = null;

            if (this._tableConfig.FKLookups.TryGetValue(fkColumnName, out var explicitConfig))
            {
                lookupConfig = explicitConfig;
            }
            else
            {
                var discoveredFk = this._tableSchema.GetForeignKey(fkColumnName);
                if (discoveredFk != null)
                {
                    string displayColumn = FindBestDisplayColumn(discoveredFk.ReferencedTable);
                    if (displayColumn != null)
                    {
                        lookupConfig = new FKLookupDefinition
                        {
                            FKColumnName = fkColumnName,
                            ReferencedTable = discoveredFk.ReferencedTable.FullName.Replace("[", "").Replace("]", ""),
                            DisplayColumn = displayColumn,
                            ValueColumn = discoveredFk.ReferencedColumn.ColumnName
                        };
                    }
                    else
                    {
                        FileLogger.Warning($"Could not determine display column using heuristics for FK '{fkColumnName}' referencing '{discoveredFk.ReferencedTable.DisplayName}'. ComboBox will not be populated.");
                        comboBox.Enabled = false;
                        comboBox.Items.Add("Lookup Error");
                        return;
                    }
                }
            }

            if (lookupConfig == null)
            {
                FileLogger.Warning($"Control for '{fkColumnName}' is ComboBox, but no FKLookup configuration or discoverable FK relationship found.");
                comboBox.Enabled = false;
                comboBox.Items.Add("Config Error");
                return;
            }

            LoadComboBoxDataAsync(comboBox, lookupConfig, fkColumnName);
        }

        private async void LoadComboBoxDataAsync(ComboBox comboBox, FKLookupDefinition lookupConfig, string fkColumnName)
        {
            try
            {
                comboBox.Enabled = false;
                comboBox.DataSource = null;
                comboBox.Items.Clear();
                comboBox.Items.Add("Loading...");
                comboBox.SelectedIndex = 0;

                DataTable lookupData = await this._dataViewManager.GetLookupDataAsync(lookupConfig);

                string valueMember = lookupConfig.ValueColumn ?? GetPrimaryKeyColumnName(lookupConfig.ReferencedTable);
                if (string.IsNullOrEmpty(valueMember))
                {
                    throw new InvalidOperationException($"Could not determine ValueMember for ComboBox '{fkColumnName}'.");
                }

                comboBox.DataSource = lookupData;
                comboBox.DisplayMember = lookupConfig.DisplayColumn;
                comboBox.ValueMember = valueMember;
                comboBox.SelectedIndex = -1;
                comboBox.Enabled = true;
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Failed to populate ComboBox for FK '{fkColumnName}' using lookup '{lookupConfig.ReferencedTable}'.", ex);
                comboBox.DataSource = null;
                comboBox.Items.Clear();
                comboBox.Items.Add("Data Load Error");
                comboBox.Enabled = false;
            }
        }

        private string FindBestDisplayColumn(TableSchema referencedTable)
        {
            var heuristicOrder = this._globalConfig.DefaultFKDisplayHeuristic;

            foreach (string heuristic in heuristicOrder)
            {
                if (heuristic.Contains("*"))
                {
                    string pattern = heuristic.Replace(".", @"\.").Replace("*", ".*");
                    try
                    {
                        var match = referencedTable.Columns.FirstOrDefault(c => System.Text.RegularExpressions.Regex.IsMatch(c.ColumnName, $"^{pattern}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
                        if (match != null) return match.ColumnName;
                    }
                    catch (ArgumentException regexEx)
                    {
                        FileLogger.Warning($"Invalid regex pattern generated from heuristic '{heuristic}': {regexEx.Message}");
                    }
                }
                else
                {
                    var match = referencedTable.Columns.FirstOrDefault(c => c.ColumnName.Equals(heuristic, StringComparison.OrdinalIgnoreCase));
                    if (match != null) return match.ColumnName;
                }
            }

            var firstStringCol = referencedTable.Columns.FirstOrDefault(c => c.DataType.ToLower().Contains("char") || c.DataType.ToLower().Contains("text"));
            if (firstStringCol != null)
            {
                FileLogger.Info($"FK heuristic fallback: Using first string column '{firstStringCol.ColumnName}' for table '{referencedTable.DisplayName}'.");
                return firstStringCol.ColumnName;
            }

            if (referencedTable.PrimaryKeys.Count == 1)
            {
                FileLogger.Info($"FK heuristic fallback: Using single primary key column '{referencedTable.PrimaryKeys[0].Column.ColumnName}' for table '{referencedTable.DisplayName}'.");
                return referencedTable.PrimaryKeys[0].Column.ColumnName;
            }

            FileLogger.Warning($"FK heuristic failed for table '{referencedTable.DisplayName}'. No suitable display column found.");
            return null;
        }

        private string GetPrimaryKeyColumnName(string fullTableName)
        {
            try
            {
                string schema = null;
                string table = fullTableName;
                if (fullTableName.Contains("."))
                {
                    var parts = fullTableName.Split('.');
                    schema = parts[0].Trim('[', ']');
                    table = parts[1].Trim('[', ']');
                }
                else
                {
                    schema = "dbo";
                }

                var referencedTableSchema = this._stateManager.AvailableTables.FirstOrDefault(t =>
                    t.TableName.Equals(table, StringComparison.OrdinalIgnoreCase) &&
                    t.SchemaName.Equals(schema, StringComparison.OrdinalIgnoreCase));

                if (referencedTableSchema == null)
                {
                    FileLogger.Error($"Could not find schema information for referenced table '{fullTableName}' in StateManager.");
                    return null;
                }

                if (referencedTableSchema.PrimaryKeys.Count == 1)
                {
                    return referencedTableSchema.PrimaryKeys[0].Column.ColumnName;
                }
                else if (referencedTableSchema.PrimaryKeys.Count == 0)
                {
                    FileLogger.Warning($"Referenced table '{fullTableName}' has no primary key defined in schema.");
                    return null;
                }
                else
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

        public void PopulateControls(DataRowView rowView)
        {
            if (rowView == null)
            {
                ClearControls();
                return;
            }

            foreach (Control control in GetAllControlsRecursive(this._detailPanel))
            {
                string columnName = control.Name;
                if (string.IsNullOrEmpty(columnName) || !rowView.Row.Table.Columns.Contains(columnName))
                {
                    if (control is CheckBox isNullChk && isNullChk.Tag?.ToString() == "IsNullCheckBox")
                    {
                        string relatedColumnName = isNullChk.Name.Replace("IsNullChk_", "");
                        if (rowView.Row.Table.Columns.Contains(relatedColumnName))
                        {
                            isNullChk.Checked = rowView[relatedColumnName] == DBNull.Value;
                            var relatedControl = this._detailPanel.Controls.Find(relatedColumnName, true).FirstOrDefault();
                            if (relatedControl != null) relatedControl.Enabled = !isNullChk.Checked;
                        }
                    }
                    continue;
                }

                object value = rowView[columnName];
                var columnSchema = this._tableSchema.GetColumn(columnName);
                this._tableConfig.DetailFormFields.TryGetValue(columnName, out var fieldConfig);
                bool isReadOnly = IsControlReadOnly(columnSchema, fieldConfig);

                try
                {
                    if (control is TextBoxBase txt)
                    {
                        txt.Text = value == DBNull.Value ? string.Empty : value.ToString();
                        txt.ReadOnly = isReadOnly;
                    }
                    else if (control is CheckBox chk && chk.Tag?.ToString() != "IsNullCheckBox")
                    {
                        chk.Checked = value != DBNull.Value && Convert.ToBoolean(value);
                        chk.Enabled = !isReadOnly;
                    }
                    else if (control is DateTimePicker dtp)
                    {
                        if (value != DBNull.Value) dtp.Value = Convert.ToDateTime(value);
                        dtp.Enabled = !isReadOnly;
                    }
                    else if (control is NullableDateTimePicker ndtp)
                    {
                        ndtp.Value = (value == DBNull.Value) ? (DateTime?)null : Convert.ToDateTime(value);
                        ndtp.Enabled = !isReadOnly;
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
                        cmb.Enabled = !isReadOnly;
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

        public void ClearControls()
        {
            foreach (Control control in GetAllControlsRecursive(this._detailPanel))
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
                    var relatedControl = this._detailPanel.Controls.Find(isNullChk.Name.Replace("IsNullChk_", ""), true).FirstOrDefault();
                    if (relatedControl != null) relatedControl.Enabled = true;
                }
            }
        }

        public Dictionary<string, object> GetControlValues()
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (Control control in GetAllControlsRecursive(this._detailPanel))
            {
                string columnName = control.Name;
                if (string.IsNullOrEmpty(columnName) || values.ContainsKey(columnName)) continue;

                var isNullChk = this._detailPanel.Controls.Find($"IsNullChk_{columnName}", true).FirstOrDefault() as CheckBox;
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
                    var columnSchema = this._tableSchema.GetColumn(columnName);
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

        public void SetControlsEnabled(bool enabled)
        {
            foreach (Control control in GetAllControlsRecursive(this._detailPanel))
            {
                string columnName = control.Name;
                if (string.IsNullOrEmpty(columnName)) continue;

                var columnSchema = this._tableSchema.GetColumn(columnName);
                this._tableConfig.DetailFormFields.TryGetValue(columnName, out var fieldConfig);
                bool isReadOnly = IsControlReadOnly(columnSchema, fieldConfig);

                bool shouldBeEnabled = enabled && !isReadOnly;

                if (control is CheckBox isNullChk && isNullChk.Tag?.ToString() == "IsNullCheckBox")
                {
                    var relatedControlName = isNullChk.Name.Replace("IsNullChk_", "");
                    var relatedColumnSchema = this._tableSchema.GetColumn(relatedControlName);
                    this._tableConfig.DetailFormFields.TryGetValue(relatedControlName, out var relatedFieldConfig);
                    bool relatedIsReadOnly = IsControlReadOnly(relatedColumnSchema, relatedFieldConfig);

                    isNullChk.Enabled = enabled && !relatedIsReadOnly && (relatedColumnSchema?.IsNullable ?? false);
                }
                else if (!(control is Label))
                {
                    control.Enabled = shouldBeEnabled;

                    if (!shouldBeEnabled)
                    {
                        var correspondingIsNullChk = this._detailPanel.Controls.Find($"IsNullChk_{columnName}", true).FirstOrDefault() as CheckBox;
                        if (correspondingIsNullChk != null && correspondingIsNullChk.Checked)
                        {
                            control.Enabled = false;
                        }
                    }
                }
            }
        }

        private bool IsControlReadOnly(ColumnSchema column, DetailFormFieldDefinition fieldConfig)
        {
            if (column == null) return true;

            if (fieldConfig?.ReadOnly.HasValue ?? false) return fieldConfig.ReadOnly.Value;

            if (column.IsIdentity || column.IsComputed || column.IsTimestamp || column.IsPrimaryKey) return true;

            return false;
        }

        private static IEnumerable<Control> GetAllControlsRecursive(Control container)
        {
            var controls = container.Controls.Cast<Control>();
            return controls.SelectMany(ctrl => GetAllControlsRecursive(ctrl)).Concat(controls);
        }

        private bool IsComplexType(string dataType)
        {
            string lowerType = dataType.ToLower();
            return lowerType == "xml" || lowerType == "geography" || lowerType == "geometry" || lowerType == "hierarchyid";
        }

        private static string FormatDisplayValue(object value)
        {
            if (value is DateTime dt) return dt.ToString(Constants.DefaultDateTimeFormat);
            return value.ToString();
        }

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
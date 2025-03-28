// File: DynamicSqlEditor/UI/Dialogs/FilterInputDialog.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DynamicSqlEditor.Common;
using DynamicSqlEditor.Configuration.Models;
// Removed: using DynamicSqlEditor.Core; // Can remove if using fully qualified names

namespace DynamicSqlEditor.UI.Dialogs
{
    public partial class FilterInputDialog : Form
    {
        private readonly FilterDefinition _filter;
        // Use fully qualified name for the field
        private readonly DynamicSqlEditor.Core.DataViewManager _dataViewManager;
        private readonly Dictionary<string, Control> _inputControls = new Dictionary<string, Control>();

        // Use fully qualified name for the constructor parameter
        public FilterInputDialog(FilterDefinition filter, DynamicSqlEditor.Core.DataViewManager dataViewManager)
        {
            InitializeComponent();
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _dataViewManager = dataViewManager ?? throw new ArgumentNullException(nameof(dataViewManager)); // Assign the correct type
            this.Text = $"Input for Filter: {filter.Label}";

            BuildInputControls();
        }

        private void BuildInputControls()
        {
            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(10)
            };
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            string[] requiredParams = _filter.RequiresInput.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string reqParamInfo in requiredParams)
            {
                string paramName = reqParamInfo.Trim();
                string lookupType = null;
                if (paramName.Contains(":"))
                {
                    var parts = paramName.Split(':');
                    paramName = parts[0].Trim();
                    lookupType = parts[1].Trim();
                }

                Label label = new Label { Text = paramName + ":", AutoSize = true, Margin = new Padding(3, 6, 3, 3) };
                Control inputControl;

                if (!string.IsNullOrEmpty(lookupType))
                {
                    // Assume lookupType corresponds to a TableConfig FKLookup definition name or a table name
                    var comboBox = new ComboBox { Name = paramName, DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Anchor = AnchorStyles.Left | AnchorStyles.Right };
                    inputControl = comboBox;
                    // Load lookup data asynchronously after form is shown
                    this.Shown += async (s, e) => await LoadLookupDataAsync(comboBox, lookupType);
                }
                else
                {
                    // Default to TextBox for non-lookup inputs
                    inputControl = new TextBox { Name = paramName, Width = 200, Anchor = AnchorStyles.Left | AnchorStyles.Right };
                }
                inputControl.Margin = new Padding(3);

                tableLayout.RowCount++;
                tableLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                tableLayout.Controls.Add(label, 0, tableLayout.RowCount - 1);
                tableLayout.Controls.Add(inputControl, 1, tableLayout.RowCount - 1);

                _inputControls.Add(paramName, inputControl);
            }

            // Add button panel below the table layout
            tableLayout.RowCount++;
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, buttonPanel.Height + 10)); // Add space for buttons
            tableLayout.Controls.Add(buttonPanel, 0, tableLayout.RowCount - 1);
            tableLayout.SetColumnSpan(buttonPanel, 2); // Span buttons across both columns
            buttonPanel.Dock = DockStyle.Fill; // Make button panel fill its cell


            this.Controls.Add(tableLayout);
            // Adjust form size based on content
            this.ClientSize = new Size(350, tableLayout.GetPreferredSize(new Size(350, 0)).Height + 10); // Calculate preferred height
            this.MinimumSize = new Size(300, this.ClientSize.Height); // Set minimum size
        }

        private async Task LoadLookupDataAsync(ComboBox comboBox, string lookupType)
        {
            // Find FKLookup definition matching the lookupType (could be FK column name or custom name)
            // This requires searching through TableConfigs - might need access to StateManager or pass more context.
            // Simplified: Assume lookupType is directly a ReferencedTable name (Schema.Table) for now.
            // A real implementation needs a way to resolve lookupType to a FKLookupDefinition.

            // Placeholder: Assume lookupType is "Schema.TableName"
            var pseudoLookupConfig = new FKLookupDefinition
            {
                // Need to guess Display/Value columns based on heuristics or require explicit config
                ReferencedTable = lookupType,
                DisplayColumn = await FindBestDisplayColumnHeuristicAsync(lookupType), // Needs heuristic logic
                ValueColumn = await GetPrimaryKeyColumnNameAsync(lookupType) // Needs PK lookup
            };

            if (string.IsNullOrEmpty(pseudoLookupConfig.DisplayColumn) || string.IsNullOrEmpty(pseudoLookupConfig.ValueColumn))
            {
                FileLogger.Error($"Cannot configure lookup ComboBox for filter parameter '{comboBox.Name}'. Could not determine Display/Value columns for '{lookupType}'.");
                comboBox.Items.Add("Lookup Error");
                comboBox.Enabled = false;
                return;
            }


            try
            {
                comboBox.Enabled = false; // Disable while loading
                comboBox.DataSource = null;
                comboBox.Items.Clear();
                comboBox.Items.Add("Loading...");
                comboBox.SelectedIndex = 0;

                DataTable lookupData = await _dataViewManager.GetLookupDataAsync(pseudoLookupConfig);

                comboBox.DataSource = lookupData;
                comboBox.DisplayMember = pseudoLookupConfig.DisplayColumn;
                comboBox.ValueMember = pseudoLookupConfig.ValueColumn;
                comboBox.SelectedIndex = -1;
                // comboBox.Items.Remove("Loading..."); // Remove placeholder if DataSource is used - DataSource replaces Items
                comboBox.Enabled = true;
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Failed to load lookup data for filter parameter '{comboBox.Name}' (LookupType: {lookupType})", ex);
                comboBox.DataSource = null;
                comboBox.Items.Clear();
                comboBox.Items.Add("Load Error");
                comboBox.Enabled = false;
            }
        }

        // Helper methods duplicated from DetailFormBuilder - need centralization
        // These helpers now rely on _dataViewManager which is the correct type
        private async Task<string> FindBestDisplayColumnHeuristicAsync(string fullTableName)
        {
            // Needs access to GlobalConfig heuristics and schema provider/cache
            // Placeholder implementation
            var heuristics = Constants.DefaultFKHeuristic.Split(',').ToList(); // Use default constant
                                                                               // Need schema for fullTableName...
                                                                               // For now, just return "Name" as a guess
                                                                               // A better approach would involve getting the TableSchema via StateManager
            return "Name"; // VERY basic placeholder
        }
        private async Task<string> GetPrimaryKeyColumnNameAsync(string fullTableName)
        {
            try
            {
                string query = @"
                    SELECT TOP 1 COLUMN_NAME
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                    WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + QUOTENAME(CONSTRAINT_NAME)), 'IsPrimaryKey') = 1
                    AND TABLE_SCHEMA = PARSENAME(@TableName, 2) AND TABLE_NAME = PARSENAME(@TableName, 1);";
                var param = SqlParameterHelper.CreateParameter("@TableName", fullTableName);
                // Access DbManager via the correctly typed _dataViewManager
                object result = await _dataViewManager.DbManager.ExecuteScalarAsync(query, new List<SqlParameter> { param });
                return result?.ToString();
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Failed to get primary key for table {fullTableName} during filter dialog setup", ex);
                return null;
            }
        }


        public Dictionary<string, object> GetInputValues()
        {
            var values = new Dictionary<string, object>();
            foreach (var kvp in _inputControls)
            {
                string paramName = kvp.Key;
                Control control = kvp.Value;
                object value = null;

                if (control is TextBox txt) value = txt.Text;
                else if (control is ComboBox cmb) value = cmb.SelectedValue; // Use SelectedValue
                // Add other control types if needed

                values.Add(paramName, value ?? DBNull.Value); // Use DBNull if value is null
            }
            return values;
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            // Basic validation: Ensure required fields are filled
            foreach (var kvp in _inputControls)
            {
                Control control = kvp.Value;
                bool isEmpty = false;
                if (control is TextBox txt && string.IsNullOrWhiteSpace(txt.Text)) isEmpty = true;
                else if (control is ComboBox cmb && cmb.SelectedIndex == -1) isEmpty = true;

                if (isEmpty)
                {
                    MessageBox.Show($"Please provide a value for '{kvp.Key}'.", "Input Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    control.Focus();
                    return; // Prevent closing dialog
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        #region Designer Code
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Panel buttonPanel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.buttonPanel = new System.Windows.Forms.Panel();
            this.buttonPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // okButton
            //
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.okButton.Location = new System.Drawing.Point(172, 10);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 23);
            this.okButton.TabIndex = 0;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            //
            // cancelButton
            //
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(253, 10);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 1;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            //
            // buttonPanel
            //
            this.buttonPanel.Controls.Add(this.okButton);
            this.buttonPanel.Controls.Add(this.cancelButton);
            this.buttonPanel.Dock = System.Windows.Forms.DockStyle.Bottom; // Will be placed in TableLayoutPanel cell
            this.buttonPanel.Location = new System.Drawing.Point(0, 0); // Positioned by TableLayoutPanel
            this.buttonPanel.MinimumSize = new System.Drawing.Size(0, 45);
            this.buttonPanel.Name = "buttonPanel";
            this.buttonPanel.Size = new System.Drawing.Size(340, 45);
            this.buttonPanel.TabIndex = 2;
            //
            // FilterInputDialog
            //
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true; // Allow form to resize based on content
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(340, 100); // Initial size, will be adjusted
            // Controls added dynamically in BuildInputControls
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FilterInputDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Filter Input";
            this.buttonPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout(); // Ensure AutoSize works correctly

        }
        #endregion
    }
}
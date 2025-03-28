using System;
using System.Linq;
using System.Windows.Forms;
using DynamicSqlEditor.Common;
using DynamicSqlEditor.Core;
using DynamicSqlEditor.Schema.Models;

namespace DynamicSqlEditor.UI
{
    public partial class MainForm : Form
    {
        private readonly StateManager _stateManager;

        public MainForm()
        {
            InitializeComponent();
            _stateManager = new StateManager();
            _stateManager.ConnectionChanged += StateManager_ConnectionChanged;
            _stateManager.SchemaRefreshed += StateManager_SchemaRefreshed;

            this.Load += MainForm_Load;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Maximized;
            UpdateStatus("Initializing...");
            if (_stateManager.Initialize())
            {
                 UpdateStatus($"Connected to {_stateManager.CurrentDatabaseName}. Ready.");
            }
            else
            {
                 UpdateStatus("Connection failed. Please check configuration or logs.");
                 MessageBox.Show("Failed to connect to the database based on configuration. Please check AppConfig.dsl and logs.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void StateManager_ConnectionChanged(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => StateManager_ConnectionChanged(sender, e)));
                return;
            }

            if (_stateManager.IsConnected)
            {
                UpdateStatus($"Connected to {_stateManager.CurrentDatabaseName}. Refreshing schema...");
                // Schema refresh will be triggered by Initialize or explicit call
            }
            else
            {
                UpdateStatus("Disconnected.");
                ClearTableMenu();
            }
        }

        private void StateManager_SchemaRefreshed(object sender, EventArgs e)
        {
             if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => StateManager_SchemaRefreshed(sender, e)));
                return;
            }
            PopulateTableMenu();
             if (_stateManager.IsConnected) UpdateStatus($"Connected to {_stateManager.CurrentDatabaseName}. Ready.");
        }

        private void PopulateTableMenu()
        {
            ClearTableMenu();

            if (_stateManager.AvailableTables == null || !_stateManager.AvailableTables.Any())
            {
                tablesToolStripMenuItem.Enabled = false;
                return;
            }

            tablesToolStripMenuItem.Enabled = true;
            var groupedTables = _stateManager.AvailableTables
                                             .OrderBy(t => t.SchemaName)
                                             .ThenBy(t => t.TableName)
                                             .GroupBy(t => t.SchemaName);

            foreach (var group in groupedTables)
            {
                ToolStripMenuItem schemaMenuItem;
                if (groupedTables.Count() > 1 || !string.Equals(group.Key, "dbo", StringComparison.OrdinalIgnoreCase))
                {
                    schemaMenuItem = new ToolStripMenuItem(group.Key);
                    tablesToolStripMenuItem.DropDownItems.Add(schemaMenuItem);
                }
                else
                {
                    // Add directly to main menu if only one schema (or only dbo)
                    schemaMenuItem = tablesToolStripMenuItem;
                }

                foreach (var table in group)
                {
                    var tableMenuItem = new ToolStripMenuItem(table.TableName) { Tag = table };
                    tableMenuItem.Click += TableMenuItem_Click;
                    schemaMenuItem.DropDownItems.Add(tableMenuItem);
                }
            }
        }

        private void ClearTableMenu()
        {
             // Remove all except potentially placeholder items if needed
             tablesToolStripMenuItem.DropDownItems.Clear();
        }


        private void TableMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is TableSchema tableSchema)
            {
                OpenDataViewForm(tableSchema);
            }
        }

        private void OpenDataViewForm(TableSchema tableSchema)
        {
            // Check if already open
            foreach (Form form in this.MdiChildren)
            {
                if (form is DataViewForm dvf && dvf.TableSchema.FullName == tableSchema.FullName)
                {
                    dvf.Activate();
                    return;
                }
            }

            try
            {
                UpdateStatus($"Loading view for {tableSchema.DisplayName}...");
                var dataViewForm = new DataViewForm(_stateManager, tableSchema);
                dataViewForm.MdiParent = this;
                dataViewForm.StatusChanged += ChildForm_StatusChanged;
                dataViewForm.Show();
                // Status update will happen within DataViewForm Load/Async operations
            }
            catch (Exception ex)
            {
                 string errorMsg = $"Error opening view for {tableSchema.DisplayName}: {ex.Message}";
                 FileLogger.Error(errorMsg, ex);
                 MessageBox.Show(errorMsg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                 UpdateStatus($"Error loading {tableSchema.DisplayName}. Ready.");
            }
        }

         private void ChildForm_StatusChanged(object sender, string statusMessage)
        {
            UpdateStatus(statusMessage);
        }

        private void UpdateStatus(string message)
        {
            if (statusStrip.InvokeRequired)
            {
                statusStrip.Invoke(new Action(() => UpdateStatus(message)));
                return;
            }
            toolStripStatusLabel.Text = message;
        }


        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void cascadeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.LayoutMdi(MdiLayout.Cascade);
        }

        private void tileHorizontalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.LayoutMdi(MdiLayout.TileHorizontal);
        }

        private void tileVerticalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.LayoutMdi(MdiLayout.TileVertical);
        }

        private void closeAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
             // Use a copy of the collection to avoid issues while closing
            var children = this.MdiChildren.ToList();
            foreach (Form child in children)
            {
                child.Close();
                 // Check if close was cancelled (e.g., by unsaved changes prompt)
                if (child.Visible)
                {
                    // Optional: Stop closing others if one fails?
                    // break;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
             // Check for unsaved changes in children before closing main form
             foreach (Form child in this.MdiChildren)
             {
                 if (child is DataViewForm dvf && dvf.IsDirty)
                 {
                     var result = MessageBox.Show($"Form '{dvf.Text.TrimEnd('*')}' has unsaved changes. Save before closing application?",
                                                  "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                     if (result == DialogResult.Yes)
                     {
                         if (!dvf.SaveChanges()) // Attempt save
                         {
                             e.Cancel = true; // Cancel closing if save fails
                             return;
                         }
                     }
                     else if (result == DialogResult.Cancel)
                     {
                         e.Cancel = true; // Cancel closing application
                         return;
                     }
                     // If No, continue closing without saving this child
                 }
             }

            base.OnFormClosing(e);
        }
    }
}
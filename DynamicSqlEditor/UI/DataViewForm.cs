// File: DynamicSqlEditor/UI/DataViewForm.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using DynamicSqlEditor.Common;
using DynamicSqlEditor.Configuration.Models;
using DynamicSqlEditor.Core;
using DynamicSqlEditor.DataAccess;
using DynamicSqlEditor.Schema.Models;
using DynamicSqlEditor.UI.Builders;
using DynamicSqlEditor.UI.Controls;
using DynamicSqlEditor.UI.Dialogs;

namespace DynamicSqlEditor.UI
{
    public partial class DataViewForm : Form
    {
        private readonly StateManager _stateManager;
        private readonly TableConfig _tableConfig;
        private readonly GlobalConfig _globalConfig;
        private readonly DynamicSqlEditor.Core.DataViewManager _dataViewManager;
        private readonly CrudManager _crudManager;
        private readonly ConcurrencyHandler _concurrencyHandler;

        private BindingSource _bindingSource;
        private DetailFormBuilder _detailBuilder; // <-- Add field for the builder instance
        private bool _isDirty = false;
        private bool _isNewRecord = false;
        private bool _isLoading = false;
        private Dictionary<string, object> _originalKeyValues;

        public TableSchema TableSchema { get; }
        public event Action<object, string> StatusChanged;

        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty != value)
                {
                    _isDirty = value;
                    UpdateTitle();
                    saveButton.Enabled = _isDirty;
                }
            }
        }

        public DataViewForm(StateManager stateManager, TableSchema tableSchema)
        {
            InitializeComponent();

            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            TableSchema = tableSchema ?? throw new ArgumentNullException(nameof(tableSchema));
            _tableConfig = _stateManager.ConfigManager.GetTableConfig(TableSchema.SchemaName, TableSchema.TableName);
            _globalConfig = _stateManager.ConfigManager.CurrentConfig.Global;

            _dataViewManager = new DynamicSqlEditor.Core.DataViewManager(_stateManager.DbManager, TableSchema, _tableConfig);
            _crudManager = new CrudManager(_stateManager.DbManager, TableSchema);
            _concurrencyHandler = new ConcurrencyHandler(TableSchema);

            this.Text = TableSchema.DisplayName;
            _bindingSource = new BindingSource();

            // Wire up events
            this.Load += DataViewForm_Load;
            this.FormClosing += DataViewForm_FormClosing;
            mainDataGridView.SelectionChanged += MainDataGridView_SelectionChanged;
            mainDataGridView.ColumnHeaderMouseClick += MainDataGridView_ColumnHeaderMouseClick;
            mainDataGridView.DataError += MainDataGridView_DataError;
            _bindingSource.CurrentChanged += BindingSource_CurrentChanged;

            // Paging Control Events
            pagingControl.FirstPageClicked += async (s, e) => await NavigatePaging(_dataViewManager.FirstPageAsync);
            pagingControl.PreviousPageClicked += async (s, e) => await NavigatePaging(_dataViewManager.PreviousPageAsync);
            pagingControl.NextPageClicked += async (s, e) => await NavigatePaging(_dataViewManager.NextPageAsync);
            pagingControl.LastPageClicked += async (s, e) => await NavigatePaging(_dataViewManager.LastPageAsync);
            pagingControl.PageSizeChanged += async (s, newSize) => {
                _dataViewManager.PageSize = newSize;
                await LoadDataAsync(1);
            };


            // Detail Action Buttons
            newButton.Click += NewButton_Click;
            saveButton.Click += async (s, e) => await SaveButton_ClickAsync();
            deleteButton.Click += async (s, e) => await DeleteButton_ClickAsync();
            refreshButton.Click += async (s, e) => await RefreshButton_ClickAsync();

            // Related Tabs Lazy Loading
            relatedDataTabControl.SelectedIndexChanged += RelatedDataTabControl_SelectedIndexChanged;
        }

        private async void DataViewForm_Load(object sender, EventArgs e)
        {
            if (_isLoading) return;
            _isLoading = true;
            this.Cursor = Cursors.WaitCursor;
            OnStatusChanged($"Loading {TableSchema.DisplayName}...");

            try
            {
                // 1. Build Static UI Parts (Filters, Detail Panel Structure, Related Tabs Structure, Action Buttons)
                BuildFilterUI();
                await BuildDetailPanelAsync(); // This now assigns _detailBuilder
                BuildRelatedTabs();
                BuildActionButtons();

                // 2. Initial Data Load
                await LoadDataAsync(1);

                // 3. Set initial state
                SetEditMode(false); // Start in view mode
                IsDirty = false; // Ensure clean state initially
                saveButton.Enabled = false;
                deleteButton.Enabled = _bindingSource.Current != null;
            }
            catch (Exception ex)
            {
                HandleError($"Error loading data view for {TableSchema.DisplayName}", ex);
            }
            finally
            {
                _isLoading = false;
                this.Cursor = Cursors.Default;
                OnStatusChanged($"Ready - {TableSchema.DisplayName}");
            }
        }

        private void BuildFilterUI()
        {
            var filterBuilder = new FilterUIBuilder(filterPanel, _tableConfig);
            filterBuilder.BuildFilters(FilterComboBox_SelectedIndexChanged);
        }

        private async Task BuildDetailPanelAsync()
        {
            // Create and store the builder instance
            _detailBuilder = new DetailFormBuilder(detailPanel, TableSchema, _tableConfig, _globalConfig, _stateManager, _dataViewManager);
            await _detailBuilder.BuildFormAsync(Control_ValueChanged);
        }

        private void BuildRelatedTabs()
        {
            var relatedTabsBuilder = new RelatedTabsBuilder(relatedDataTabControl, TableSchema, _tableConfig, _stateManager.DbManager);
            relatedTabsBuilder.BuildTabs();
        }

        private void BuildActionButtons()
        {
            var actionButtonBuilder = new ActionButtonBuilder(actionButtonPanel, _tableConfig, _globalConfig);
            actionButtonBuilder.BuildButtons(ActionButton_Click);
        }

        private async Task LoadDataAsync(int pageNumber)
        {

            if (IsDirty && !PromptSaveChanges()) return;

            _isLoading = true; 
            this.Cursor = Cursors.WaitCursor;
            mainDataGridView.DataSource = null;
            OnStatusChanged($"Loading page {pageNumber} for {TableSchema.DisplayName}...");

            try
            {
                var (data, totalRecords) = await _dataViewManager.LoadDataAsync(pageNumber);

                _bindingSource.DataSource = data;
                mainDataGridView.DataSource = _bindingSource;

                ConfigureGridView();

                pagingControl.UpdatePagingInfo(_dataViewManager.CurrentPage, _dataViewManager.TotalPages, _dataViewManager.TotalRecords, _dataViewManager.PageSize);
                UpdateStatusAndControlStates();
            }
            catch (Exception ex)
            {
                HandleError($"Failed to load data for {TableSchema.DisplayName}", ex);
                pagingControl.UpdatePagingInfo(1, 0, 0, _dataViewManager.PageSize);
            }
            finally
            {
                _isLoading = false; 
                this.Cursor = Cursors.Default;
                OnStatusChanged($"Page {_dataViewManager.CurrentPage} of {_dataViewManager.TotalPages} ({_dataViewManager.TotalRecords} records) - {TableSchema.DisplayName}");
            }
        }

        private void ConfigureGridView()
        {
            mainDataGridView.AllowUserToAddRows = false;
            mainDataGridView.AllowUserToDeleteRows = false;
            mainDataGridView.ReadOnly = true;
            mainDataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            mainDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            mainDataGridView.MultiSelect = false;

            foreach (DataGridViewColumn gridCol in mainDataGridView.Columns)
            {
                if (_tableConfig.DetailFormFields.TryGetValue(gridCol.Name, out var fieldConfig) && !string.IsNullOrEmpty(fieldConfig.Label))
                {
                    gridCol.HeaderText = fieldConfig.Label;
                }

                if (gridCol.ValueType == typeof(DateTime))
                {
                    gridCol.DefaultCellStyle.Format = Constants.DefaultDateTimeFormat;
                }
                else if (gridCol.ValueType == typeof(decimal) || gridCol.ValueType == typeof(double) || gridCol.ValueType == typeof(float))
                {
                    gridCol.DefaultCellStyle.Format = "N2";
                }

                var schemaCol = TableSchema.GetColumn(gridCol.Name);
                bool isVisibleConfig = !_tableConfig.DetailFormFields.TryGetValue(gridCol.Name, out var cfg) || (cfg.Visible ?? true);
                bool isComplex = schemaCol != null && (schemaCol.DataType.ToLower() == "xml" || schemaCol.DataType.ToLower() == "geography" || schemaCol.DataType.ToLower() == "geometry" || schemaCol.DataType.ToLower() == "hierarchyid");

                gridCol.Visible = isVisibleConfig && !isComplex;
            }
        }

        private void UpdateStatusAndControlStates()
        {
            bool hasRecords = _dataViewManager.TotalRecords > 0;
            bool recordSelected = _bindingSource.Current != null;

            deleteButton.Enabled = recordSelected && !_isNewRecord;
            saveButton.Enabled = IsDirty;

            foreach (Control c in actionButtonPanel.Controls)
            {
                if (c is Button btn && btn.Tag is ActionButtonDefinition actionDef)
                {
                    btn.Enabled = !actionDef.RequiresSelection || recordSelected;
                }
            }
        }

        private void MainDataGridView_SelectionChanged(object sender, EventArgs e)
        {
            // Handled by BindingSource_CurrentChanged
        }

        private void BindingSource_CurrentChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;

            if (IsDirty && !PromptSaveChanges())
            {
                // User cancelled the save/discard prompt
                FileLogger.Warning("Selection changed while dirty, but user cancelled save/discard.");

                // Temporarily remove the event handler
                _bindingSource.CurrentChanged -= BindingSource_CurrentChanged;

                // Revert to previous selection
                if (_originalKeyValues != null)
                {
                    // Find the row with matching key values
                    foreach (DataRowView rowView in _bindingSource)
                    {
                        bool match = true;
                        foreach (var kvp in _originalKeyValues)
                        {
                            if (!rowView[kvp.Key].Equals(kvp.Value))
                            {
                                match = false;
                                break;
                            }
                        }

                        if (match)
                        {
                            _bindingSource.Position = _bindingSource.IndexOf(rowView);
                            break;
                        }
                    }
                }

                // Reattach the event handler
                _bindingSource.CurrentChanged += BindingSource_CurrentChanged;

                return; // Exit without updating UI
            }

            PopulateDetailPanel();
            ClearRelatedTabsData();
            LoadDataForVisibleRelatedTab();
            UpdateStatusAndControlStates();
        }

        // Calls instance method on _detailBuilder
        private void PopulateDetailPanel()
        {
            var currentView = _bindingSource.Current as DataRowView;
            _originalKeyValues = null;

            if (_detailBuilder == null) return; // Guard against calls before builder is created

            if (currentView == null || _isNewRecord)
            {
                _detailBuilder.ClearControls(); // Use instance method
                SetEditMode(true);
                return;
            }

            SetEditMode(false);
            _originalKeyValues = GetKeyValues(currentView);
            _detailBuilder.PopulateControls(currentView); // Use instance method
        }

        private Dictionary<string, object> GetKeyValues(DataRowView rowView)
        {
            var keyValues = new Dictionary<string, object>();
            if (rowView == null) return keyValues;

            foreach (var pk in TableSchema.PrimaryKeys)
            {
                if (rowView.Row.Table.Columns.Contains(pk.Column.ColumnName))
                {
                    keyValues[pk.Column.ColumnName] = rowView[pk.Column.ColumnName];
                }
                else
                {
                    FileLogger.Error($"Primary key column '{pk.Column.ColumnName}' not found in data source for table {TableSchema.DisplayName}.");
                }
            }
            return keyValues;
        }

        // Calls instance method on _detailBuilder
        private void SetEditMode(bool enabled)
        {
            if (_detailBuilder == null) return; // Guard

            _detailBuilder.SetControlsEnabled(enabled); // Use instance method
            mainDataGridView.Enabled = !enabled;
            filterPanel.Enabled = !enabled;
            pagingControl.Enabled = !enabled;
            newButton.Enabled = !enabled;
            deleteButton.Enabled = !enabled && _bindingSource.Current != null;
            refreshButton.Enabled = !enabled;
        }

        private void Control_ValueChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;

            if (sender is CheckBox isNullChk && isNullChk.Tag?.ToString() == "IsNullCheckBox")
            {
                var relatedControl = detailPanel.Controls.OfType<Control>().FirstOrDefault(c => c.Name == isNullChk.Name.Replace("IsNullChk_", ""));
                if (relatedControl != null)
                {
                    relatedControl.Enabled = !isNullChk.Checked;
                    if (isNullChk.Checked && relatedControl is ComboBox cmb) cmb.SelectedIndex = -1;
                    if (isNullChk.Checked && relatedControl is NullableDateTimePicker dtp) dtp.Value = null;
                }
            }

            if (!_isNewRecord && !IsDirty)
            {
                IsDirty = true;
            }
            else if (_isNewRecord)
            {
                IsDirty = true;
            }

            if (IsDirty && !_isNewRecord) SetEditMode(true);
        }


        private void NewButton_Click(object sender, EventArgs e)
        {
            if (IsDirty && !PromptSaveChanges()) return;
            if (_detailBuilder == null) return; // Guard

            _isNewRecord = true;
            IsDirty = false;
            _bindingSource.SuspendBinding();
            mainDataGridView.ClearSelection();
            mainDataGridView.Enabled = false;
            filterPanel.Enabled = false;
            pagingControl.Enabled = false;

            _detailBuilder.ClearControls(); // Use instance method
            SetEditMode(true);
            UpdateTitle();
            saveButton.Enabled = false;
            deleteButton.Enabled = false;
            refreshButton.Enabled = false;

            detailPanel.Controls.OfType<Control>().FirstOrDefault()?.Focus();
            OnStatusChanged($"Entering new record for {TableSchema.DisplayName}...");
        }

        private async Task SaveButton_ClickAsync()
        {
            if (!IsDirty || _detailBuilder == null) return; // Guard

            this.Cursor = Cursors.WaitCursor;
            OnStatusChanged($"Saving record for {TableSchema.DisplayName}...");

            try
            {
                var columnValues = _detailBuilder.GetControlValues(); // Use instance method

                if (!ValidateInput(columnValues))
                {
                    this.Cursor = Cursors.Default;
                    OnStatusChanged($"Validation failed. Please check input. - {TableSchema.DisplayName}");
                    return;
                }


                if (_isNewRecord)
                {
                    await _crudManager.InsertRecordAsync(columnValues);
                    OnStatusChanged($"New record saved successfully for {TableSchema.DisplayName}.");
                }
                else
                {
                    object originalTimestamp = _concurrencyHandler.GetTimestampValue(_bindingSource.Current as DataRowView);
                    int rowsAffected = await _crudManager.UpdateRecordAsync(columnValues, _originalKeyValues, originalTimestamp);
                    if (rowsAffected > 0)
                    {
                        OnStatusChanged($"Record updated successfully for {TableSchema.DisplayName}.");
                    }
                    else
                    {
                        OnStatusChanged($"Record update may not have completed (0 rows affected). - {TableSchema.DisplayName}");
                    }
                }

                _isNewRecord = false;
                IsDirty = false;
                SetEditMode(false);
                _bindingSource.ResumeBinding();

                await RefreshDataAsync(true);
            }
            catch (DBConcurrencyException ex)
            {
                HandleConcurrencyError(ex);
            }
            catch (Exception ex)
            {
                HandleError($"Error saving record for {TableSchema.DisplayName}", ex);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private bool ValidateInput(Dictionary<string, object> columnValues)
        {
            var errors = new List<string>();
            foreach (var col in TableSchema.Columns)
            {
                if (!col.IsNullable && !col.IsIdentity && !col.IsComputed && !col.IsTimestamp)
                {
                    if (!columnValues.TryGetValue(col.ColumnName, out var value) || value == null || value == DBNull.Value)
                    {
                        string labelText = col.ColumnName;
                        if (_tableConfig.DetailFormFields.TryGetValue(col.ColumnName, out var fieldCfg) && !string.IsNullOrEmpty(fieldCfg.Label))
                        {
                            labelText = fieldCfg.Label;
                        }
                        errors.Add($"'{labelText}' cannot be empty.");
                    }
                }

                if (col.MaxLength.HasValue && col.MaxLength > 0 && columnValues.TryGetValue(col.ColumnName, out var strValue) && strValue is string s && s.Length > col.MaxLength.Value)
                {
                    string labelText = col.ColumnName;
                    if (_tableConfig.DetailFormFields.TryGetValue(col.ColumnName, out var fieldCfg) && !string.IsNullOrEmpty(fieldCfg.Label)) labelText = fieldCfg.Label;
                    errors.Add($"'{labelText}' exceeds maximum length of {col.MaxLength.Value}.");
                }
            }

            if (errors.Any())
            {
                MessageBox.Show("Please correct the following errors:\n\n" + string.Join("\n", errors), "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }


        private async Task DeleteButton_ClickAsync()
        {
            var currentView = _bindingSource.Current as DataRowView;
            if (currentView == null || _isNewRecord) return;

            var result = MessageBox.Show("Are you sure you want to delete the current record?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            this.Cursor = Cursors.WaitCursor;
            OnStatusChanged($"Deleting record from {TableSchema.DisplayName}...");

            try
            {
                var keyValues = GetKeyValues(currentView);
                object originalTimestamp = _concurrencyHandler.GetTimestampValue(currentView);

                await _crudManager.DeleteRecordAsync(keyValues, originalTimestamp);

                IsDirty = false;
                OnStatusChanged($"Record deleted successfully from {TableSchema.DisplayName}.");

                await RefreshDataAsync(true);
            }
            catch (DBConcurrencyException ex)
            {
                HandleConcurrencyError(ex);
            }
            catch (Exception ex)
            {
                HandleError($"Error deleting record from {TableSchema.DisplayName}", ex);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private async Task RefreshButton_ClickAsync()
        {
            await RefreshDataAsync(false);
        }

        private async Task RefreshDataAsync(bool force = false)
        {
            if (!force && IsDirty && !PromptSaveChanges()) return;

            await LoadDataAsync(_dataViewManager.CurrentPage);
        }

        private async void FilterComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading || !(sender is ComboBox cmb) || cmb.SelectedItem == null) return;

            FilterDefinition selectedFilter = null;
            Dictionary<string, object> filterParams = null;

            if (cmb.SelectedItem is KeyValuePair<string, FilterDefinition> kvp)
            {
                selectedFilter = kvp.Value;
            }
            else if (cmb.SelectedItem.ToString() == FilterUIBuilder.ClearFilterText)
            {
                selectedFilter = null;
            }

            if (selectedFilter != null && !string.IsNullOrEmpty(selectedFilter.RequiresInput))
            {
                using (var inputDialog = new FilterInputDialog(selectedFilter, _dataViewManager))
                {
                    if (inputDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        filterParams = inputDialog.GetInputValues();
                    }
                    else
                    {
                        cmb.SelectedIndexChanged -= FilterComboBox_SelectedIndexChanged;
                        if (_dataViewManager.CurrentFilter != null)
                        {
                            var filterItem = cmb.Items.OfType<KeyValuePair<string, FilterDefinition>>()
                                .FirstOrDefault(item => item.Value == _dataViewManager.CurrentFilter);
                            cmb.SelectedItem = filterItem;
                        }
                        else
                        {
                            var clearItem = cmb.Items.OfType<string>()
                                .FirstOrDefault(item => item == FilterUIBuilder.ClearFilterText);
                            cmb.SelectedItem = clearItem;
                        }
                        cmb.SelectedIndexChanged += FilterComboBox_SelectedIndexChanged;
                        return;
                    }
                }
            }

            this.Cursor = Cursors.WaitCursor;
            OnStatusChanged($"Applying filter '{selectedFilter?.Label ?? "None"}'...");
            try
            {
                if (selectedFilter != null)
                {
                    await _dataViewManager.ApplyFilterAsync(selectedFilter, filterParams);
                }
                else
                {
                    await _dataViewManager.ClearFilterAsync();
                }
            }
            catch (Exception ex)
            {
                HandleError($"Error applying filter '{selectedFilter?.Label ?? "None"}'", ex);
                this.Cursor = Cursors.Default;
                OnStatusChanged($"Error applying filter. Ready. - {TableSchema.DisplayName}");
            }
        }


        private async void MainDataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (_isLoading || IsDirty) return;

            string columnName = mainDataGridView.Columns[e.ColumnIndex].DataPropertyName;

            this.Cursor = Cursors.WaitCursor;
            OnStatusChanged($"Sorting by {columnName}...");
            try
            {
                await _dataViewManager.ApplySortAsync(columnName);
            }
            catch (Exception ex)
            {
                HandleError($"Error sorting by column {columnName}", ex);
                this.Cursor = Cursors.Default;
                OnStatusChanged($"Error sorting. Ready. - {TableSchema.DisplayName}");
            }
        }

        private async Task NavigatePaging(Func<Task<(DataTable Data, int TotalRecords)>> navigationAction)
        {
            if (_isLoading) return;
            if (IsDirty && !PromptSaveChanges()) return;

            _isLoading = true;
            this.Cursor = Cursors.WaitCursor;
            mainDataGridView.DataSource = null;
            OnStatusChanged($"Navigating data for {TableSchema.DisplayName}...");

            try
            {
                var (data, totalRecords) = await navigationAction();

                _bindingSource.DataSource = data;
                mainDataGridView.DataSource = _bindingSource;

                pagingControl.UpdatePagingInfo(_dataViewManager.CurrentPage, _dataViewManager.TotalPages, _dataViewManager.TotalRecords, _dataViewManager.PageSize);
                UpdateStatusAndControlStates();
            }
            catch (Exception ex)
            {
                HandleError($"Failed to navigate data for {TableSchema.DisplayName}", ex);
            }
            finally
            {
                _isLoading = false;
                this.Cursor = Cursors.Default;
                OnStatusChanged($"Page {_dataViewManager.CurrentPage} of {_dataViewManager.TotalPages} ({_dataViewManager.TotalRecords} records) - {TableSchema.DisplayName}");
            }
        }

        private void RelatedDataTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadDataForVisibleRelatedTab();
        }

        private async void LoadDataForVisibleRelatedTab()
        {
            if (_isLoading || relatedDataTabControl.SelectedIndex <= 0)
            {
                return;
            }

            var selectedTab = relatedDataTabControl.SelectedTab;
            if (selectedTab == null || !(selectedTab.Tag is RelatedChildDefinition relatedConfig) || !(selectedTab.Controls[0] is DataGridView relatedGrid))
            {
                return;
            }

            if (relatedGrid.DataSource != null && relatedGrid.Rows.Count > 0)
            {
                return;
            }

            var currentMasterView = _bindingSource.Current as DataRowView;
            if (currentMasterView == null)
            {
                relatedGrid.DataSource = null;
                return;
            }

            this.Cursor = Cursors.WaitCursor;
            OnStatusChanged($"Loading related data: {relatedConfig.Label}...");
            relatedGrid.DataSource = null;

            try
            {
                var parentKeyValues = GetKeyValues(currentMasterView);
                if (parentKeyValues.Count == 0)
                {
                    FileLogger.Warning($"Cannot load related data for '{relatedConfig.Label}': Parent key values could not be determined.");
                    return;
                }

                DataTable relatedData = await _dataViewManager.GetRelatedDataAsync(relatedConfig, parentKeyValues);
                relatedGrid.DataSource = relatedData;

                relatedGrid.ReadOnly = true;
                relatedGrid.AllowUserToAddRows = false;
                relatedGrid.AllowUserToDeleteRows = false;
                relatedGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
                relatedGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                relatedGrid.MultiSelect = false;

                OnStatusChanged($"Loaded related data: {relatedConfig.Label}. Ready. - {TableSchema.DisplayName}");
            }
            catch (Exception ex)
            {
                HandleError($"Error loading related data for tab '{relatedConfig.Label}'", ex);
                OnStatusChanged($"Error loading related data: {relatedConfig.Label}. Ready. - {TableSchema.DisplayName}");
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void ClearRelatedTabsData()
        {
            foreach (TabPage tabPage in relatedDataTabControl.TabPages)
            {
                if (tabPage.Tag is RelatedChildDefinition && tabPage.Controls.Count > 0 && tabPage.Controls[0] is DataGridView relatedGrid)
                {
                    relatedGrid.DataSource = null;
                }
            }
        }

        private void ActionButton_Click(object sender, EventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is ActionButtonDefinition actionDef)) return;

            var currentView = _bindingSource.Current as DataRowView;
            if (actionDef.RequiresSelection && currentView == null)
            {
                MessageBox.Show("Please select a record before performing this action.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_globalConfig.DisableCustomActionExecution)
            {
                MessageBox.Show("Execution of custom actions is disabled by configuration.", "Action Disabled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                FileLogger.Warning($"Execution of ActionButton '{actionDef.Name}' blocked by DisableCustomActionExecution=True.");
                return;
            }

            try
            {
                string command = actionDef.Command;
                var rowData = currentView?.Row.Table.Columns.Cast<DataColumn>()
                                     .ToDictionary(col => col.ColumnName, col => currentView[col.ColumnName], StringComparer.OrdinalIgnoreCase)
                                     ?? new Dictionary<string, object>();

                command = Regex.Replace(command, @"\{(\w+)\}", match => {
                    string columnName = match.Groups[1].Value;
                    if (rowData.TryGetValue(columnName, out object value) && value != DBNull.Value && value != null)
                    {
                        string stringValue = value.ToString();
                        return stringValue.Contains(" ") ? $"\"{stringValue}\"" : stringValue;
                    }
                    return string.Empty;
                });

                OnStatusChanged($"Executing action: {actionDef.Label}...");
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = command.Split(' ')[0],
                    Arguments = command.Contains(" ") ? command.Substring(command.IndexOf(' ') + 1) : "",
                    UseShellExecute = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (!string.IsNullOrEmpty(actionDef.SuccessMessage))
                    {
                        MessageBox.Show(actionDef.SuccessMessage, "Action Started", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    OnStatusChanged($"Action '{actionDef.Label}' executed. Ready. - {TableSchema.DisplayName}");
                }
            }
            catch (Exception ex)
            {
                HandleError($"Error executing action '{actionDef.Label}'", ex);
                OnStatusChanged($"Error executing action '{actionDef.Label}'. Ready. - {TableSchema.DisplayName}");
            }
        }


        private bool PromptSaveChanges()
        {
            if (!IsDirty) return true;

            var result = MessageBox.Show($"Save changes to the current record in '{TableSchema.DisplayName}'?",
                                         "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                Task<bool> saveTask = Task.Run(async () => { await SaveButton_ClickAsync(); return !IsDirty; });
                return saveTask.Result;
            }
            else if (result == DialogResult.No)
            {
                IsDirty = false;
                _isNewRecord = false;
                _bindingSource.ResumeBinding();
                PopulateDetailPanel(); // Calls instance method on _detailBuilder
                SetEditMode(false);
                return true;
            }
            else
            {
                return false;
            }
        }

        private void DataViewForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!PromptSaveChanges())
            {
                e.Cancel = true;
            }
        }

        private void UpdateTitle()
        {
            string baseTitle = TableSchema.DisplayName;
            this.Text = IsDirty ? baseTitle + " " + Constants.DirtyFlagIndicator : baseTitle;
            if (_isNewRecord) this.Text += " (New)";
        }

        private void HandleError(string message, Exception ex)
        {
            FileLogger.Error(message, ex);
            MessageBox.Show($"{message}:\n{ex.Message}\n\nSee log file for details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void HandleConcurrencyError(DBConcurrencyException ex)
        {
            FileLogger.Error("Concurrency conflict detected.", ex);
            var result = MessageBox.Show($"{ex.Message}\n\nDo you want to reload the data? Reloading will discard your current changes.",
                                         "Concurrency Conflict", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                IsDirty = false;
                _isNewRecord = false;
                Task.Run(async () => await RefreshDataAsync(true));
            }
        }

        private void MainDataGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            FileLogger.Error($"DataGridView Error at ({e.ColumnIndex},{e.RowIndex}): {e.Exception.Message}", e.Exception);
            OnStatusChanged($"Display Error: {e.Exception.Message}");
            e.ThrowException = false;
        }

        protected virtual void OnStatusChanged(string message)
        {
            StatusChanged?.Invoke(this, message);
        }

        public bool SaveChanges()
        {
            if (!IsDirty) return true;
            Task<bool> saveTask = Task.Run(async () => { await SaveButton_ClickAsync(); return !IsDirty; });
            return saveTask.Result;
        }
    }
}
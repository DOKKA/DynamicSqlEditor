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
        private DetailFormBuilder _detailBuilder;
        private bool _isDirty = false;
        private bool _isNewRecord = false;
        private bool _isLoading = false;
        private bool _isPopulatingDetails = false;
        private Dictionary<string, object> _originalKeyValues;

        public TableSchema TableSchema { get; }
        public event Action<object, string> StatusChanged;


        public event EventHandler<RequestOpenDataViewEventArgs> RequestOpenDataView;

        public class RequestOpenDataViewEventArgs : EventArgs
        {
            public TableSchema TargetTableSchema { get; }
            public Dictionary<string, object> PrimaryKeyValues { get; }

            public RequestOpenDataViewEventArgs(TableSchema targetTableSchema, Dictionary<string, object> primaryKeyValues)
            {
                TargetTableSchema = targetTableSchema;
                PrimaryKeyValues = primaryKeyValues;
            }
        }

        protected virtual void OnRequestOpenDataView(RequestOpenDataViewEventArgs e)
        {
            RequestOpenDataView?.Invoke(this, e);
        }

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

        private readonly Dictionary<string, object> _initialPrimaryKeyValues;
        public DataViewForm(StateManager stateManager, TableSchema tableSchema, Dictionary<string, object> initialPrimaryKeyValues = null)
        {
            InitializeComponent();

            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            TableSchema = tableSchema ?? throw new ArgumentNullException(nameof(tableSchema));
            _tableConfig = _stateManager.ConfigManager.GetTableConfig(TableSchema.SchemaName, TableSchema.TableName);
            _globalConfig = _stateManager.ConfigManager.CurrentConfig.Global;

            _initialPrimaryKeyValues = initialPrimaryKeyValues;

            _dataViewManager = new Core.DataViewManager(_stateManager.DbManager, TableSchema, _tableConfig);
            _crudManager = new CrudManager(_stateManager.DbManager, TableSchema);
            _concurrencyHandler = new ConcurrencyHandler(TableSchema);

            this.Text = TableSchema.DisplayName;
            _bindingSource = new BindingSource();

            this.Load += DataViewForm_Load;
            this.FormClosing += DataViewForm_FormClosing;
            mainDataGridView.SelectionChanged += MainDataGridView_SelectionChanged;
            mainDataGridView.ColumnHeaderMouseClick += MainDataGridView_ColumnHeaderMouseClick;
            mainDataGridView.DataError += MainDataGridView_DataError;
            _bindingSource.CurrentChanged += BindingSource_CurrentChanged;

            pagingControl.FirstPageClicked += async (s, e) => await NavigatePaging(_dataViewManager.FirstPageAsync);
            pagingControl.PreviousPageClicked += async (s, e) => await NavigatePaging(_dataViewManager.PreviousPageAsync);
            pagingControl.NextPageClicked += async (s, e) => await NavigatePaging(_dataViewManager.NextPageAsync);
            pagingControl.LastPageClicked += async (s, e) => await NavigatePaging(_dataViewManager.LastPageAsync);
            pagingControl.PageSizeChanged += async (s, newSize) => {
                _dataViewManager.PageSize = newSize;
                await LoadDataAsync(1);
            };

            newButton.Click += NewButton_Click;
            saveButton.Click += async (s, e) => await SaveButton_ClickAsync();
            deleteButton.Click += async (s, e) => await DeleteButton_ClickAsync();
            refreshButton.Click += async (s, e) => await RefreshButton_ClickAsync();

            relatedDataTabControl.SelectedIndexChanged += RelatedDataTabControl_SelectedIndexChanged;
        }

        private async void DataViewForm_Load(object sender, EventArgs e)
        {
            if (_isLoading) return;
            _isLoading = true;
            this.Cursor = Cursors.WaitCursor;
            OnStatusChanged($"Initializing view for {TableSchema.DisplayName}...");

            try
            {
                BuildFilterUI();
                await BuildDetailPanelAsync();
                BuildRelatedTabs();
                AttachRelatedGridEventHandlers();
                BuildActionButtons();

                int initialPage = 1;
                if (_initialPrimaryKeyValues != null && _initialPrimaryKeyValues.Any())
                {
                    OnStatusChanged($"Calculating initial page for {TableSchema.DisplayName}...");
                    initialPage = await _dataViewManager.GetPageNumberForRowAsync(_initialPrimaryKeyValues);
                }

                await LoadDataAsync(initialPage);

                if (_initialPrimaryKeyValues != null && _initialPrimaryKeyValues.Any() && _bindingSource.Count > 0)
                {
                    SelectRowByPrimaryKey(_initialPrimaryKeyValues);
                }

            }
            catch (Exception ex)
            {
                HandleError($"Error loading data view for {TableSchema.DisplayName}", ex);
            }
            finally
            {
                _isLoading = false;
                this.Cursor = Cursors.Default;
            }
        }


        private void SelectRowByPrimaryKey(Dictionary<string, object> primaryKeyValues)
        {
            if (primaryKeyValues == null || !primaryKeyValues.Any() || _bindingSource.DataSource == null)
                return;

            OnStatusChanged($"Selecting specified record in {TableSchema.DisplayName}...");
            for (int i = 0; i < _bindingSource.Count; i++)
            {
                if (_bindingSource[i] is DataRowView rowView)
                {
                    bool match = true;
                    foreach (var pk in TableSchema.PrimaryKeys)
                    {
                        string pkColName = pk.Column.ColumnName;
                        if (!primaryKeyValues.TryGetValue(pkColName, out object targetValue) ||
                            !rowView.Row.Table.Columns.Contains(pkColName) ||
                            !Equals(rowView[pkColName], targetValue))
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        _bindingSource.Position = i;
                        mainDataGridView.FirstDisplayedScrollingRowIndex = i;
                        OnStatusChanged($"Record selected. Ready - {TableSchema.DisplayName}");
                        return;
                    }
                }
            }
            FileLogger.Warning($"Initial primary key values provided, but the corresponding row was not found on the loaded page ({_dataViewManager.CurrentPage}) for {TableSchema.DisplayName}.");
            OnStatusChanged($"Specified record not found on page {_dataViewManager.CurrentPage}. Ready - {TableSchema.DisplayName}");
        }

        private async void RelatedGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || !(sender is DataGridView relatedGrid))
                return;

            if (!(relatedGrid.Parent is TabPage tabPage) || !(tabPage.Tag is RelatedChildDefinition relatedDef))
                return;

            var dataRowView = relatedGrid.Rows[e.RowIndex].DataBoundItem as DataRowView;
            if (dataRowView == null)
                return;

            OnStatusChanged($"Looking up schema for {relatedDef.ChildTable}...");
            this.Cursor = Cursors.WaitCursor;

            try
            {
                string[] childTableParts = relatedDef.ChildTable.Split('.');
                string childSchemaName = childTableParts.Length > 1 ? childTableParts[0] : "dbo";
                string childTableName = childTableParts.Length > 1 ? childTableParts[1] : childTableParts[0];

                TableSchema childTableSchema = _stateManager.AvailableTables.FirstOrDefault(ts =>
                    ts.SchemaName.Equals(childSchemaName, StringComparison.OrdinalIgnoreCase) &&
                    ts.TableName.Equals(childTableName, StringComparison.OrdinalIgnoreCase));

                if (childTableSchema == null)
                {
                    HandleError($"Could not find schema information for table '{relatedDef.ChildTable}'.", null);
                    return;
                }

                if (!childTableSchema.PrimaryKeys.Any())
                {
                    MessageBox.Show($"Cannot navigate: The related table '{childTableSchema.DisplayName}' does not have a primary key defined.", "Navigation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var pkValues = new Dictionary<string, object>();
                foreach (var pk in childTableSchema.PrimaryKeys)
                {
                    string pkColumnName = pk.Column.ColumnName;
                    if (dataRowView.Row.Table.Columns.Contains(pkColumnName))
                    {
                        pkValues[pkColumnName] = dataRowView[pkColumnName];
                    }
                    else
                    {
                        HandleError($"Primary key column '{pkColumnName}' not found in the data for related table '{childTableSchema.DisplayName}'. Cannot navigate.", null);
                        return;
                    }
                }

                OnRequestOpenDataView(new RequestOpenDataViewEventArgs(childTableSchema, pkValues));
            }
            catch (Exception ex)
            {
                HandleError($"Error preparing navigation to related table '{relatedDef.ChildTable}'", ex);
            }
            finally
            {
                OnStatusChanged($"Ready - {TableSchema.DisplayName}");
                this.Cursor = Cursors.Default;
            }
        }

        private void AttachRelatedGridEventHandlers()
        {
            foreach (TabPage tabPage in relatedDataTabControl.TabPages)
            {
                if (tabPage.Tag is RelatedChildDefinition && tabPage.Controls.Count > 0 && tabPage.Controls[0] is DataGridView relatedGrid)
                {
                    relatedGrid.CellDoubleClick -= RelatedGrid_CellDoubleClick;
                    relatedGrid.CellDoubleClick += RelatedGrid_CellDoubleClick;
                }
            }
        }

        private void BuildFilterUI()
        {
            var filterBuilder = new FilterUIBuilder(filterPanel, _tableConfig);
            filterBuilder.BuildFilters(FilterComboBox_SelectedIndexChanged);
        }

        private async Task BuildDetailPanelAsync()
        {
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

            var flowPanel = actionButtonPanel.Controls.OfType<FlowLayoutPanel>().FirstOrDefault();
            if (flowPanel != null)
            {
                foreach (Control c in flowPanel.Controls)
                {
                    if (c is Button btn && btn.Tag is ActionButtonDefinition actionDef)
                    {
                        btn.Enabled = !actionDef.RequiresSelection || recordSelected;

                        if (_globalConfig.DisableCustomActionExecution)
                        {
                            btn.Enabled = false;
                        }
                    }
                }
            }
        }

        private void MainDataGridView_SelectionChanged(object sender, EventArgs e)
        {
        }

        private void BindingSource_CurrentChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;

            if (IsDirty && !PromptSaveChanges())
            {
                FileLogger.Warning("Selection changed while dirty, but user cancelled save/discard.");

                _bindingSource.CurrentChanged -= BindingSource_CurrentChanged;

                if (_originalKeyValues != null)
                {
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

                _bindingSource.CurrentChanged += BindingSource_CurrentChanged;

                return;
            }

            PopulateDetailPanel();
            ClearRelatedTabsData();
            LoadDataForVisibleRelatedTab();
            UpdateStatusAndControlStates();
        }

        private void PopulateDetailPanel()
        {
            var currentView = _bindingSource.Current as DataRowView;
            _originalKeyValues = null;

            if (_detailBuilder == null) return;

            _isPopulatingDetails = true;
            try
            {
                if (currentView == null || _isNewRecord)
                {
                    _detailBuilder.ClearControls();
                    SetEditMode(true);
                }
                else
                {
                    SetEditMode(false);
                    _originalKeyValues = GetKeyValues(currentView);
                    _detailBuilder.PopulateControls(currentView);
                }
            }
            finally
            {
                _isPopulatingDetails = false;
            }

            if (!_isNewRecord)
            {
                IsDirty = false;
            }
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

        private void SetEditMode(bool enabled)
        {
            if (_detailBuilder == null) return;

            _detailBuilder.SetControlsEnabled(enabled);
            mainDataGridView.Enabled = !enabled;
            filterPanel.Enabled = !enabled;
            pagingControl.Enabled = !enabled;
            newButton.Enabled = !enabled;
            deleteButton.Enabled = !enabled && _bindingSource.Current != null;
            refreshButton.Enabled = !enabled;
        }

        private void Control_ValueChanged(object sender, EventArgs e)
        {
            if (_isLoading || _isPopulatingDetails) return;

            if (sender is CheckBox isNullChk && isNullChk.Tag?.ToString() == "IsNullCheckBox")
            {
            }

            if (!_isNewRecord && !IsDirty)
            {
                IsDirty = true;
            }
            else if (_isNewRecord)
            {
                IsDirty = true;
                saveButton.Enabled = true;
            }

            if (IsDirty && !_isNewRecord)
            {
                SetEditMode(true);
            }
        }


        private void NewButton_Click(object sender, EventArgs e)
        {
            if (IsDirty && !PromptSaveChanges()) return;
            if (_detailBuilder == null) return;

            _isNewRecord = true;
            IsDirty = false;
            _bindingSource.SuspendBinding();
            mainDataGridView.ClearSelection();
            mainDataGridView.Enabled = false;
            filterPanel.Enabled = false;
            pagingControl.Enabled = false;

            _detailBuilder.ClearControls();
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
            if (!IsDirty || _detailBuilder == null) return;

            this.Cursor = Cursors.WaitCursor;
            OnStatusChanged($"Saving record for {TableSchema.DisplayName}...");

            try
            {
                var columnValues = _detailBuilder.GetControlValues();

                if (!ValidateInput(columnValues))
                {
                    this.Cursor = Cursors.Default;
                    OnStatusChanged($"Validation failed. Please check input. - {TableSchema.DisplayName}");
                    return;
                }

                bool isNew = _isNewRecord; // Store before potentially changing it

                if (isNew) // Use the stored value
                {
                    await _crudManager.InsertRecordAsync(columnValues);
                    OnStatusChanged($"New record saved successfully for {TableSchema.DisplayName}.");
                }
                else
                {
                    // Ensure _bindingSource.Current is valid before getting timestamp if possible
                    // Although _originalKeyValues should be sufficient for the update itself.
                    object originalTimestamp = null;
                    if (_bindingSource.Current is DataRowView currentView)
                    {
                        originalTimestamp = _concurrencyHandler.GetTimestampValue(currentView);
                    }
                    else if (!isNew) // Only warn if it was an update and current is null
                    {
                        FileLogger.Warning("Could not get current DataRowView to retrieve timestamp before update. Concurrency check might be affected if timestamp column exists.");
                    }

                    int rowsAffected = await _crudManager.UpdateRecordAsync(columnValues, _originalKeyValues, originalTimestamp);
                    if (rowsAffected > 0)
                    {
                        OnStatusChanged($"Record updated successfully for {TableSchema.DisplayName}.");
                    }
                    else
                    {
                        // This might happen due to concurrency even if no exception was thrown (e.g., no timestamp column)
                        OnStatusChanged($"Record update affected 0 rows. It might have been modified or deleted. - {TableSchema.DisplayName}");
                    }
                }

                // --- Reordered Section ---
                _isNewRecord = false; // Update state flags
                IsDirty = false;

                // Refresh the data source BEFORE updating UI edit mode
                await RefreshDataAsync(true);

                // Resume binding AFTER the data source is refreshed
                _bindingSource.ResumeBinding();

                // Now update the edit mode based on the refreshed state
                SetEditMode(false);
                // --- End Reordered Section ---

                // If it was a new record, try to select it after refresh
                if (isNew)
                {
                    // TODO: Implement logic to find and select the newly added row
                    // This might involve getting the new PK from InsertRecordAsync
                    // and then calling SelectRowByPrimaryKey or similar.
                    // For now, the refresh will just load the page it's on.
                }

            }
            catch (DBConcurrencyException ex)
            {
                HandleConcurrencyError(ex);
                // After handling concurrency, we might need to re-enable edit mode if user chose not to reload
                // Or ensure the state is consistent. For now, the handler forces a reload or keeps state.
            }
            catch (Exception ex)
            {
                HandleError($"Error saving record for {TableSchema.DisplayName}", ex);
                // If save failed, we should arguably remain in edit mode?
                // Or revert changes? Current logic exits edit mode in finally.
                // Consider adding logic here to keep edit mode enabled on error.
                // For now, let finally handle it, but it might be confusing for the user.
            }
            finally
            {
                // SetEditMode(false) is now called within the try block after refresh.
                // We might not need it in finally anymore, unless an error occurred
                // before the SetEditMode call in the try block.
                // Let's keep the cursor reset.
                this.Cursor = Cursors.Default;
                // UpdateStatus is called within the try/catch blocks now.
                UpdateStatusAndControlStates(); // Ensure button states are correct after save/refresh
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
            else if (cmb.SelectedItem is string selectedString && selectedString == FilterUIBuilder.ClearFilterText)
            {
                selectedFilter = null;
            }
            else
            {
                FileLogger.Warning($"Unexpected item type selected in filter ComboBox: {cmb.SelectedItem.GetType()}");
                return;
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
                        object itemToRestore = FilterUIBuilder.ClearFilterText;
                        if (_dataViewManager.CurrentFilter != null)
                        {
                            var currentKvp = cmb.Items.OfType<KeyValuePair<string, FilterDefinition>>()
                                                .FirstOrDefault(item => item.Value == _dataViewManager.CurrentFilter);
                            if (currentKvp.Key != null)
                            {
                                itemToRestore = currentKvp;
                            }
                        }
                        cmb.SelectedItem = itemToRestore;
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
                await LoadDataAsync(1);
            }
            catch (Exception ex)
            {
                HandleError($"Error applying filter '{selectedFilter?.Label ?? "None"}'", ex);
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
                // Execute the async save on the UI thread to avoid cross-thread
                // exceptions from accessing form controls.
                SaveButton_ClickAsync().GetAwaiter().GetResult();
                return !IsDirty;
            }
            else if (result == DialogResult.No)
            {
                IsDirty = false;
                _isNewRecord = false;
                _bindingSource.ResumeBinding();
                PopulateDetailPanel();
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
                // Refresh synchronously on the UI thread to keep control access safe
                RefreshDataAsync(true).GetAwaiter().GetResult();
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
            // Run the async save on the current (UI) thread
            SaveButton_ClickAsync().GetAwaiter().GetResult();
            return !IsDirty;
        }
    }
}
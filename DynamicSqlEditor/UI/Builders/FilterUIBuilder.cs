// File: DynamicSqlEditor/UI/Builders/FilterUIBuilder.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DynamicSqlEditor.Configuration.Models;

namespace DynamicSqlEditor.UI.Builders
{
    public class FilterUIBuilder
    {
        private readonly Panel _filterPanel;
        private readonly TableConfig _tableConfig;
        public const string ClearFilterText = "-- No Filter --";

        public FilterUIBuilder(Panel filterPanel, TableConfig tableConfig)
        {
            _filterPanel = filterPanel ?? throw new ArgumentNullException(nameof(filterPanel));
            _tableConfig = tableConfig ?? throw new ArgumentNullException(nameof(tableConfig));
        }

        public void BuildFilters(EventHandler filterChangedHandler)
        {
            _filterPanel.Controls.Clear();

            if (_tableConfig.Filters == null || !_tableConfig.Filters.Any())
            {
                return;
            }

            var filterLabel = new Label
            {
                Text = "Filter:",
                AutoSize = true,
                Location = new Point(5, 9),
                Anchor = AnchorStyles.Left
            };

            var filterComboBox = new ComboBox
            {
                Name = "filterComboBox",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(filterLabel.Right + 5, 5),
                Width = 250,
                Anchor = AnchorStyles.Left
            };

            // --- Start Modification ---

            // 1. Add items directly to the Items collection
            filterComboBox.Items.Clear();
            filterComboBox.Items.Add(ClearFilterText); // Add the clear string first

            var sortedFilters = _tableConfig.Filters
                                     .OrderBy(f => f.Value.Label)
                                     .Select(kvp => new KeyValuePair<string, FilterDefinition>(kvp.Key, kvp.Value))
                                     .ToList();

            foreach (var kvp in sortedFilters)
            {
                // Add the KeyValuePair itself. The Format event will handle display.
                filterComboBox.Items.Add(kvp);
            }

            // 2. Use the Format event to display the correct text
            filterComboBox.FormattingEnabled = true; // Important for Format event
            filterComboBox.Format += (s, e) => {
                if (e.ListItem is KeyValuePair<string, FilterDefinition> kvp)
                {
                    // Display the Label from the FilterDefinition
                    e.Value = kvp.Value.Label ?? kvp.Key; // Fallback to key if label is missing
                }
                else if (e.ListItem is string str)
                {
                    // Display the string itself (e.g., "-- No Filter --")
                    e.Value = str;
                }
                else if (e.ListItem != null)
                {
                    // Handle unexpected types if necessary
                    e.Value = e.ListItem.ToString();
                }
            };

            // 3. Remove DataSource, DisplayMember, ValueMember assignments
            // filterComboBox.DataSource = filterItems; // REMOVED
            // filterComboBox.DisplayMember = "Value.Label"; // REMOVED
            // filterComboBox.ValueMember = "Key"; // REMOVED

            // --- End Modification ---


            // Set default filter if specified
            object itemToSelect = ClearFilterText; // Default to clear text
            if (!string.IsNullOrEmpty(_tableConfig.DefaultFilterName))
            {
                // Find the KeyValuePair corresponding to the default filter name
                var defaultKvp = sortedFilters.FirstOrDefault(kvp => kvp.Key.Equals(_tableConfig.DefaultFilterName, StringComparison.OrdinalIgnoreCase));
                if (defaultKvp.Key != null) // Check if KeyValuePair was found (Key won't be null)
                {
                    itemToSelect = defaultKvp;
                }
            }
            filterComboBox.SelectedItem = itemToSelect;


            filterComboBox.SelectedIndexChanged += filterChangedHandler;

            _filterPanel.Controls.Add(filterLabel);
            _filterPanel.Controls.Add(filterComboBox);
        }
    }
}
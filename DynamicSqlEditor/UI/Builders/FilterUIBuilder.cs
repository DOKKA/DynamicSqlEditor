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
                // Optionally add a label indicating no filters are defined
                return;
            }

            var filterLabel = new Label
            {
                Text = "Filter:",
                AutoSize = true,
                Location = new Point(5, 9), // Adjust position as needed
                Anchor = AnchorStyles.Left
            };

            var filterComboBox = new ComboBox
            {
                Name = "filterComboBox",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(filterLabel.Right + 5, 5),
                Width = 250, // Adjust width as needed
                Anchor = AnchorStyles.Left
            };

            // Use KeyValuePair to store both name and definition
            var filterItems = new List<object> { ClearFilterText }; // Add "No Filter" option
            filterItems.AddRange(_tableConfig.Filters
                                     .OrderBy(f => f.Value.Label)
                                     .Select(kvp => new KeyValuePair<string, FilterDefinition>(kvp.Key, kvp.Value))
                                     .Cast<object>()
                                     .ToList());

            filterComboBox.DataSource = filterItems;
            filterComboBox.DisplayMember = "Value.Label"; // Display the Label property of FilterDefinition
            filterComboBox.ValueMember = "Key"; // Use the filter name as the value

            // Handle display for the "Clear Filter" string item
            filterComboBox.Format += (s, e) => {
                if (e.ListItem is KeyValuePair<string, FilterDefinition> kvp)
                {
                    e.Value = kvp.Value.Label;
                }
                else if (e.ListItem is string str)
                {
                    e.Value = str; // Display the string itself ("-- No Filter --")
                }
            };


            // Set default filter if specified
            if (!string.IsNullOrEmpty(_tableConfig.DefaultFilterName) && _tableConfig.Filters.ContainsKey(_tableConfig.DefaultFilterName))
            {
                 var defaultItem = filterItems.OfType<KeyValuePair<string, FilterDefinition>>()
                                             .FirstOrDefault(kvp => kvp.Key.Equals(_tableConfig.DefaultFilterName, StringComparison.OrdinalIgnoreCase));
                 if (defaultItem.Key != null) // Check if found
                 {
                     filterComboBox.SelectedItem = defaultItem;
                 }
                 else filterComboBox.SelectedItem = ClearFilterText; // Fallback
            }
            else
            {
                filterComboBox.SelectedItem = ClearFilterText; // Default to "No Filter"
            }


            filterComboBox.SelectedIndexChanged += filterChangedHandler;

            _filterPanel.Controls.Add(filterLabel);
            _filterPanel.Controls.Add(filterComboBox);
        }
    }
}
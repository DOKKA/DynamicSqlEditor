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

            filterComboBox.Items.Clear();
            filterComboBox.Items.Add(ClearFilterText);

            var sortedFilters = _tableConfig.Filters
                                     .OrderBy(f => f.Value.Label)
                                     .Select(kvp => new KeyValuePair<string, FilterDefinition>(kvp.Key, kvp.Value))
                                     .ToList();

            foreach (var kvp in sortedFilters)
            {
                filterComboBox.Items.Add(kvp);
            }

            filterComboBox.FormattingEnabled = true;
            filterComboBox.Format += (s, e) =>
            {
                if (e.ListItem is KeyValuePair<string, FilterDefinition> kvp)
                {
                    e.Value = kvp.Value.Label ?? kvp.Key;
                }
                else if (e.ListItem is string str)
                {
                    e.Value = str;
                }
                else if (e.ListItem != null)
                {
                    e.Value = e.ListItem.ToString();
                }
            };

            object itemToSelect = ClearFilterText;
            if (!string.IsNullOrEmpty(_tableConfig.DefaultFilterName))
            {
                var defaultKvp = sortedFilters.FirstOrDefault(kvp => kvp.Key.Equals(_tableConfig.DefaultFilterName, StringComparison.OrdinalIgnoreCase));
                if (defaultKvp.Key != null)
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
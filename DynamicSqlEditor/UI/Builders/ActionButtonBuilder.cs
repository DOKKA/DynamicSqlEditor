using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DynamicSqlEditor.Configuration.Models;

namespace DynamicSqlEditor.UI.Builders
{
    public class ActionButtonBuilder
    {
        private readonly Panel _actionButtonPanel;
        private readonly TableConfig _tableConfig;
        private readonly GlobalConfig _globalConfig;

        public ActionButtonBuilder(Panel actionButtonPanel, TableConfig tableConfig, GlobalConfig globalConfig)
        {
            _actionButtonPanel = actionButtonPanel ?? throw new ArgumentNullException(nameof(actionButtonPanel));
            _tableConfig = tableConfig ?? throw new ArgumentNullException(nameof(tableConfig));
            _globalConfig = globalConfig ?? throw new ArgumentNullException(nameof(globalConfig));
        }

        public void BuildButtons(EventHandler buttonClickHandler)
        {
            _actionButtonPanel.Controls.Clear();
            _actionButtonPanel.Visible = _tableConfig.ActionButtons.Any();

            if (!_actionButtonPanel.Visible) return;

            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, // Keep buttons on one line if possible
                AutoScroll = true // Add scroll if they don't fit
            };

            foreach (var kvp in _tableConfig.ActionButtons.OrderBy(b => b.Value.Label)) // Order alphabetically
            {
                var actionDef = kvp.Value;
                var button = new Button
                {
                    Text = actionDef.Label,
                    Tag = actionDef, // Store definition for click handler
                    AutoSize = true,
                    MinimumSize = new Size(80, 0), // Minimum width
                    Margin = new Padding(3),
                    Enabled = !actionDef.RequiresSelection // Initially enabled only if selection not required
                };

                // Check if execution is globally disabled
                if (_globalConfig.DisableCustomActionExecution)
                {
                    button.Enabled = false;
                    button.Text += " (Disabled)";
                    // Optionally add a tooltip explaining why it's disabled
                    var toolTip = new ToolTip();
                    toolTip.SetToolTip(button, "Custom action execution is disabled in configuration.");
                }
                else
                {
                    button.Click += buttonClickHandler;
                }

                flowPanel.Controls.Add(button);
            }

            _actionButtonPanel.Controls.Add(flowPanel);
        }
    }
}
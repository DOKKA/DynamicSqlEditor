using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DynamicSqlEditor.Configuration.Models;
using DynamicSqlEditor.DataAccess;
using DynamicSqlEditor.Schema.Models;

namespace DynamicSqlEditor.UI.Builders
{
    public class RelatedTabsBuilder
    {
        private readonly TabControl _tabControl;
        private readonly TableSchema _parentTableSchema;
        private readonly TableConfig _parentTableConfig;
        private readonly DatabaseManager _dbManager; // Needed for schema lookups if necessary

        public RelatedTabsBuilder(TabControl tabControl, TableSchema parentTableSchema, TableConfig parentTableConfig, DatabaseManager dbManager)
        {
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            _parentTableSchema = parentTableSchema ?? throw new ArgumentNullException(nameof(parentTableSchema));
            _parentTableConfig = parentTableConfig ?? throw new ArgumentNullException(nameof(parentTableConfig));
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
        }

        public void BuildTabs()
        {
            // Remove existing related tabs (keep index 0 - Details)
            while (_tabControl.TabPages.Count > 1)
            {
                _tabControl.TabPages.RemoveAt(1);
            }

            var relations = GetRelationsToDisplay();

            foreach (var relation in relations)
            {
                var tabPage = new TabPage(relation.Label)
                {
                    Tag = relation // Store the definition for later data loading
                };

                var relatedGrid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    ReadOnly = true,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                    Name = $"relatedGrid_{relation.RelationName}" // Unique name
                };

                tabPage.Controls.Add(relatedGrid);
                _tabControl.TabPages.Add(tabPage);
            }
        }

        private List<RelatedChildDefinition> GetRelationsToDisplay()
        {
            var relations = new List<RelatedChildDefinition>();
            var relationNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Add explicitly configured relations
            foreach (var kvp in _parentTableConfig.RelatedChildren)
            {
                if (relationNames.Add(kvp.Key)) // Avoid duplicates if config has same name twice
                {
                    // Use RelationName from config key
                    kvp.Value.RelationName = kvp.Key;
                    relations.Add(kvp.Value);
                }
            }

            // 2. Add discovered relations (FKs in other tables referencing this table's PK)
            // Avoid adding if a relation with the same ChildTable/ChildFKColumn is already configured
            foreach (var fk in _parentTableSchema.ReferencedByForeignKeys)
            {
                // Check if this relationship is already covered by config (based on child table and FK column)
                bool alreadyConfigured = relations.Any(r =>
                    r.ChildTable.Equals($"{fk.ReferencingTable.SchemaName}.{fk.ReferencingTable.TableName}", StringComparison.OrdinalIgnoreCase) &&
                    r.ChildFKColumn.Equals(fk.ReferencingColumn.ColumnName, StringComparison.OrdinalIgnoreCase));

                if (!alreadyConfigured)
                {
                    // Generate a default label and relation name
                    string relationName = $"FK_{fk.ReferencingTable.TableName}_{fk.ReferencingColumn.ColumnName}";
                    if (relationNames.Add(relationName)) // Ensure unique name
                    {
                         relations.Add(new RelatedChildDefinition
                         {
                             RelationName = relationName,
                             Label = fk.ReferencingTable.TableName, // Default label is child table name
                             ChildTable = $"{fk.ReferencingTable.SchemaName}.{fk.ReferencingTable.TableName}",
                             ChildFKColumn = fk.ReferencingColumn.ColumnName,
                             ParentPKColumn = fk.ReferencedColumn.ColumnName // The PK column in the parent table
                         });
                    }
                }
            }

            // Could add sorting here if needed, e.g., alphabetically by Label
            return relations.OrderBy(r => r.Label).ToList();
        }
    }
}
using System.Collections.Generic;
using System.Windows.Forms; // For SortOrder

namespace DynamicSqlEditor.Configuration.Models
{
    public class TableConfig
    {
        public string FullTableName { get; }
        public string CustomSelectQuery { get; set; }
        public string DefaultSortColumn { get; set; }
        public SortOrder DefaultSortDirection { get; set; } = SortOrder.Ascending;
        public string DefaultFilterName { get; set; }

        public Dictionary<string, FilterDefinition> Filters { get; } = new Dictionary<string, FilterDefinition>(System.StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, DetailFormFieldDefinition> DetailFormFields { get; } = new Dictionary<string, DetailFormFieldDefinition>(System.StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, FKLookupDefinition> FKLookups { get; } = new Dictionary<string, FKLookupDefinition>(System.StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ActionButtonDefinition> ActionButtons { get; } = new Dictionary<string, ActionButtonDefinition>(System.StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, RelatedChildDefinition> RelatedChildren { get; } = new Dictionary<string, RelatedChildDefinition>(System.StringComparer.OrdinalIgnoreCase);

        public TableConfig(string fullTableName)
        {
            FullTableName = fullTableName;
        }
    }
}
namespace DynamicSqlEditor.Common
{
    public static class Constants
    {
        public const string DefaultConfigFileName = "AppConfig.dsl";
        public const string ConfigDirectory = "Config";
        public const string LogDirectory = "Logs";
        public const int DefaultQueryTimeout = 60;
        public const string DefaultDateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        public const string DefaultDateFormat = "yyyy-MM-dd";
        public const string DirtyFlagIndicator = "*";

        public const string WherePlaceholder = "{WHERE}";
        public const string OrderByPlaceholder = "{ORDERBY}";
        public const string PagingPlaceholder = "{PAGING}";

        public const string DefaultFKHeuristic = "Name,Description,Title,*ID";

        public static class Sections
        {
            public const string Connection = "Connection";
            public const string Global = "Global";
            public const string TablePrefix = "Table:";
        }

        public static class Keys
        {
            public const string ConnectionString = "ConnectionString";
            public const string QueryTimeout = "QueryTimeout";
            public const string IncludeSchemas = "IncludeSchemas";
            public const string ExcludeTables = "ExcludeTables";
            public const string DefaultFKDisplayHeuristic = "DefaultFKDisplayHeuristic";
            public const string DisableCustomActionExecution = "DisableCustomActionExecution";
            public const string CustomSelectQuery = "CustomSelectQuery";
            public const string FilterPrefix = "Filter.";
            public const string FilterDefault = "Filter.Default";
            public const string DetailFormFieldPrefix = "DetailFormField.";
            public const string FKLookupPrefix = "FKLookup.";
            public const string ActionButtonPrefix = "ActionButton.";
            public const string RelatedChildPrefix = "RelatedChild.";
            public const string DefaultSortColumn = "DefaultSortColumn";
            public const string DefaultSortDirection = "DefaultSortDirection";
        }

        public static class Attributes
        {
            public const string Label = "Label";
            public const string WhereClause = "WhereClause";
            public const string RequiresInput = "RequiresInput";
            public const string FilterName = "FilterName";
            public const string Order = "Order";
            public const string ReadOnly = "ReadOnly";
            public const string Visible = "Visible";
            public const string ControlType = "ControlType";
            public const string ReferencedTable = "ReferencedTable";
            public const string DisplayColumn = "DisplayColumn";
            public const string ValueColumn = "ValueColumn";
            public const string ReferencedColumn = "ReferencedColumn";
            public const string Command = "Command";
            public const string RequiresSelection = "RequiresSelection";
            public const string SuccessMessage = "SuccessMessage";
            public const string ChildTable = "ChildTable";
            public const string ChildFKColumn = "ChildFKColumn";
            public const string ParentPKColumn = "ParentPKColumn";
            public const string ChildFilter = "ChildFilter";
        }

        public static class ControlTypes
        {
            public const string Default = "Default";
            public const string TextBox = "TextBox";
            public const string TextBoxMultiLine = "TextBoxMultiLine";
            public const string ComboBox = "ComboBox";
            public const string CheckBox = "CheckBox";
            public const string DateTimePicker = "DateTimePicker";
            public const string Label = "Label";
        }
    }
}
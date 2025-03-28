namespace DynamicSqlEditor.Configuration.Models
{
    public class FilterDefinition
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public string WhereClause { get; set; }
        public string RequiresInput { get; set; } // Format: "ParamName:LookupType" or "ParamName"
    }
}
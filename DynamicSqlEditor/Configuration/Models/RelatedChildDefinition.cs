namespace DynamicSqlEditor.Configuration.Models
{
    public class RelatedChildDefinition
    {
        public string RelationName { get; set; }
        public string Label { get; set; }
        public string ChildTable { get; set; } // Schema.TableName
        public string ChildFKColumn { get; set; }
        public string ParentPKColumn { get; set; } // Optional, defaults to PK of current table
        public string ChildFilter { get; set; } // Optional static WHERE clause for child query
    }
}
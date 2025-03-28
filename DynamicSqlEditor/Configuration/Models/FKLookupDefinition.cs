namespace DynamicSqlEditor.Configuration.Models
{
    public class FKLookupDefinition
    {
        public string FKColumnName { get; set; }
        public string ReferencedTable { get; set; } // Schema.TableName
        public string DisplayColumn { get; set; }
        public string ValueColumn { get; set; } // Optional, defaults to PK of ReferencedTable
        public string ReferencedColumn { get; set; } // Optional, defaults to ValueColumn or PK
    }
}
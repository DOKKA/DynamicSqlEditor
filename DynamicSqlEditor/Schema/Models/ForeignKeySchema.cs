namespace DynamicSqlEditor.Schema.Models
{
    public class ForeignKeySchema
    {
        public string ConstraintName { get; set; }
        public TableSchema ReferencingTable { get; set; } // The table containing the FK column (Child)
        public ColumnSchema ReferencingColumn { get; set; } // The FK column itself
        public TableSchema ReferencedTable { get; set; } // The table the FK points to (Parent)
        public ColumnSchema ReferencedColumn { get; set; } // The PK/Unique column being referenced
    }
}
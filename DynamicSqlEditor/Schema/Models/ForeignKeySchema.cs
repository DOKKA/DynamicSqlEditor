namespace DynamicSqlEditor.Schema.Models
{
    public class ForeignKeySchema
    {
        // --- Add these temporary properties ---
        public string ReferencingSchemaName { get; set; }
        public string ReferencingTableName { get; set; }
        public string ReferencingColumnName { get; set; }
        public string ReferencedSchemaName { get; set; }
        public string ReferencedTableName { get; set; }
        public string ReferencedColumnName { get; set; }
        // --- End of additions ---

        // Existing properties
        public string ConstraintName { get; set; }
        public TableSchema ReferencingTable { get; set; } // Keep this, will be linked later
        public ColumnSchema ReferencingColumn { get; set; } // Keep this, will be linked later
        public TableSchema ReferencedTable { get; set; } // Keep this, will be linked later
        public ColumnSchema ReferencedColumn { get; set; } // Keep this, will be linked later
    }
}
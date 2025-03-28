namespace DynamicSqlEditor.Schema.Models
{
    public class PrimaryKeySchema
    {
        // --- Add these temporary properties ---
        public string ParentTableSchemaName { get; set; }
        public string ParentTableName { get; set; }
        public string ColumnName { get; set; } // Store the column name
        // --- End of additions ---

        // Existing properties
        public TableSchema ParentTable { get; set; }
        public ColumnSchema Column { get; set; } // Keep this, will be linked later
        public string KeyName { get; set; }
        public int OrdinalPosition { get; set; } // For composite keys
    }
}
namespace DynamicSqlEditor.Schema.Models
{
    public class PrimaryKeySchema
    {
        public TableSchema ParentTable { get; set; }
        public ColumnSchema Column { get; set; }
        public string KeyName { get; set; }
        public int OrdinalPosition { get; set; } // For composite keys
    }
}
using System.Collections.Generic;
using System.Linq;

namespace DynamicSqlEditor.Schema.Models
{
    public class TableSchema
    {
        public string SchemaName { get; set; }
        public string TableName { get; set; }
        public bool IsView { get; set; }
        public List<ColumnSchema> Columns { get; } = new List<ColumnSchema>();
        public List<PrimaryKeySchema> PrimaryKeys { get; } = new List<PrimaryKeySchema>();
        public List<ForeignKeySchema> ForeignKeys { get; } = new List<ForeignKeySchema>(); // FKs defined IN this table
        public List<ForeignKeySchema> ReferencedByForeignKeys { get; } = new List<ForeignKeySchema>(); // FKs in OTHER tables referencing this one

        public string FullName => $"[{SchemaName}].[{TableName}]";
        public string DisplayName => $"{SchemaName}.{TableName}";

        public ColumnSchema GetColumn(string name) => Columns.FirstOrDefault(c => c.ColumnName.Equals(name, System.StringComparison.OrdinalIgnoreCase));
        public PrimaryKeySchema GetPrimaryKey(string columnName) => PrimaryKeys.FirstOrDefault(pk => pk.Column.ColumnName.Equals(columnName, System.StringComparison.OrdinalIgnoreCase));
        public ForeignKeySchema GetForeignKey(string columnName) => ForeignKeys.FirstOrDefault(fk => fk.ReferencingColumn.ColumnName.Equals(columnName, System.StringComparison.OrdinalIgnoreCase));
    }
}
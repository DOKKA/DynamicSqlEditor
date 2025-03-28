using System.Data; // For SqlDbType

namespace DynamicSqlEditor.Schema.Models
{
    public class ColumnSchema
    {
        // --- Add these temporary properties ---
        public string ParentTableSchemaName { get; set; }
        public string ParentTableName { get; set; }
        // --- End of additions ---

        // Existing properties
        public TableSchema ParentTable { get; set; } // Keep this, will be linked later
        public string ColumnName { get; set; }
        public int OrdinalPosition { get; set; }
        public string DataType { get; set; }
        public int? MaxLength { get; set; }
        public int? NumericPrecision { get; set; }
        public int? NumericScale { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; } // Convenience flag
        public bool IsForeignKey { get; set; } // Convenience flag
        public bool IsIdentity { get; set; }
        public bool IsComputed { get; set; }
        public bool IsTimestamp { get; set; } // rowversion or timestamp

        // Existing GetSqlDbType method...
        public SqlDbType GetSqlDbType()
        {
            // Basic mapping, needs refinement for specific types
            switch (DataType.ToLower())
            {
                case "bigint": return SqlDbType.BigInt;
                case "binary": return SqlDbType.Binary;
                case "bit": return SqlDbType.Bit;
                case "char": return SqlDbType.Char;
                case "date": return SqlDbType.Date;
                case "datetime": return SqlDbType.DateTime;
                case "datetime2": return SqlDbType.DateTime2;
                case "datetimeoffset": return SqlDbType.DateTimeOffset;
                case "decimal": return SqlDbType.Decimal;
                case "float": return SqlDbType.Float;
                case "geography": return SqlDbType.Udt; // Requires specific UDT handling
                case "geometry": return SqlDbType.Udt; // Requires specific UDT handling
                case "hierarchyid": return SqlDbType.Udt; // Requires specific UDT handling
                case "image": return SqlDbType.Image;
                case "int": return SqlDbType.Int;
                case "money": return SqlDbType.Money;
                case "nchar": return SqlDbType.NChar;
                case "ntext": return SqlDbType.NText;
                case "numeric": return SqlDbType.Decimal; // Often synonymous with decimal
                case "nvarchar": return SqlDbType.NVarChar;
                case "real": return SqlDbType.Real;
                case "rowversion":
                case "timestamp": return SqlDbType.Timestamp;
                case "smalldatetime": return SqlDbType.SmallDateTime;
                case "smallint": return SqlDbType.SmallInt;
                case "smallmoney": return SqlDbType.SmallMoney;
                case "sql_variant": return SqlDbType.Variant;
                case "text": return SqlDbType.Text;
                case "time": return SqlDbType.Time;
                case "tinyint": return SqlDbType.TinyInt;
                case "uniqueidentifier": return SqlDbType.UniqueIdentifier;
                case "varbinary": return SqlDbType.VarBinary;
                case "varchar": return SqlDbType.VarChar;
                case "xml": return SqlDbType.Xml;
                default: return SqlDbType.NVarChar; // Default fallback
            }
        }
    }
}
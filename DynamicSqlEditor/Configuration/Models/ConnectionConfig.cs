using DynamicSqlEditor.Common;

namespace DynamicSqlEditor.Configuration.Models
{
    public class ConnectionConfig
    {
        public string ConnectionString { get; set; }
        public int QueryTimeout { get; set; } = Constants.DefaultQueryTimeout;
    }
}
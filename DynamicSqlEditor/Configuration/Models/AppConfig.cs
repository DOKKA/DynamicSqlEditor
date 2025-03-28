using System.Collections.Generic;

namespace DynamicSqlEditor.Configuration.Models
{
    public class AppConfig
    {
        public ConnectionConfig Connection { get; set; } = new ConnectionConfig();
        public GlobalConfig Global { get; set; } = new GlobalConfig();
        public Dictionary<string, TableConfig> Tables { get; set; } = new Dictionary<string, TableConfig>(System.StringComparer.OrdinalIgnoreCase);
    }
}
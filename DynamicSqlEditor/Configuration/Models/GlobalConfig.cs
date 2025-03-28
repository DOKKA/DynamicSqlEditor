using System.Collections.Generic;
using System.Linq;
using DynamicSqlEditor.Common;

namespace DynamicSqlEditor.Configuration.Models
{
    public class GlobalConfig
    {
        public List<string> IncludeSchemas { get; set; } = new List<string>();
        public List<string> ExcludeTables { get; set; } = new List<string>();
        public List<string> DefaultFKDisplayHeuristic { get; set; } = Constants.DefaultFKHeuristic.Split(',').ToList();
        public bool DisableCustomActionExecution { get; set; } = false;
    }
}
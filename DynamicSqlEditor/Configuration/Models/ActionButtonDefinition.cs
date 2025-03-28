namespace DynamicSqlEditor.Configuration.Models
{
    public class ActionButtonDefinition
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public string Command { get; set; }
        public bool RequiresSelection { get; set; } = true;
        public string SuccessMessage { get; set; }
    }
}
namespace DynamicSqlEditor.Configuration.Models
{
    public class DetailFormFieldDefinition
    {
        public string ColumnName { get; set; }
        public int Order { get; set; } = 999;
        public string Label { get; set; }
        public bool? ReadOnly { get; set; } // Nullable bool to distinguish between not set and set to false
        public bool? Visible { get; set; } // Nullable bool
        public string ControlType { get; set; } // e.g., TextBox, ComboBox, DateTimePicker, Label
    }
}
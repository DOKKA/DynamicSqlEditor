using System;
using System.Windows.Forms;
using DynamicSqlEditor.Configuration.Models;
using DynamicSqlEditor.Schema.Models;
using DynamicSqlEditor.UI.Controls;

namespace DynamicSqlEditor.Common
{
    public static class ControlFactory
    {
        public static Control CreateControl(ColumnSchema column, DetailFormFieldDefinition fieldConfig)
        {
            string controlType = fieldConfig?.ControlType ?? Constants.ControlTypes.Default;

            if (controlType == Constants.ControlTypes.Label || (fieldConfig?.ReadOnly ?? false))
            {
                return new Label { AutoSize = true, Padding = new Padding(3, 6, 3, 3) };
            }

            if (controlType == Constants.ControlTypes.CheckBox || (controlType == Constants.ControlTypes.Default && column.DataType.ToLower() == "bit"))
            {
                return new CheckBox { AutoSize = true };
            }

            if (controlType == Constants.ControlTypes.DateTimePicker || (controlType == Constants.ControlTypes.Default && IsDateType(column.DataType)))
            {
                if (column.IsNullable)
                {
                    return new NullableDateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = Constants.DefaultDateTimeFormat, Width = 180 };
                }
                else
                {
                    return new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = Constants.DefaultDateTimeFormat, Width = 180 };
                }
            }

            if (controlType == Constants.ControlTypes.ComboBox)
            {
                 return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
            }

            if (controlType == Constants.ControlTypes.TextBoxMultiLine || (controlType == Constants.ControlTypes.Default && IsLongTextType(column.DataType, column.MaxLength)))
            {
                return new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 60, Width = 180 };
            }

            return new TextBox { Width = 180 };
        }

        public static Label CreateLabel(ColumnSchema column, DetailFormFieldDefinition fieldConfig)
        {
            string labelText = fieldConfig?.Label ?? column.ColumnName;
            return new Label { Text = labelText + ":", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleRight };
        }

        public static CheckBox CreateIsNullCheckBox(ColumnSchema column)
        {
            if (!column.IsNullable || column.DataType.ToLower() == "bit") return null;

            return new CheckBox { Text = "Is Null", AutoSize = true, Tag = "IsNullCheckBox" };
        }

        private static bool IsDateType(string dataType)
        {
            string lowerType = dataType.ToLower();
            return lowerType.Contains("date") || lowerType.Contains("time");
        }

        private static bool IsLongTextType(string dataType, int? maxLength)
        {
            string lowerType = dataType.ToLower();
            return lowerType.Contains("text") || lowerType.Contains("xml") || (lowerType.Contains("char") && (maxLength == -1 || maxLength > 255));
        }
    }
}
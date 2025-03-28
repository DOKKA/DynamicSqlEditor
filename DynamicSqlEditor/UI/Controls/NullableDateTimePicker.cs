using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace DynamicSqlEditor.UI.Controls
{
    public class NullableDateTimePicker : DateTimePicker
    {
        private DateTimePickerFormat _originalFormat = DateTimePickerFormat.Short;
        private string _originalCustomFormat = null;
        private bool _isNull = false;

        public NullableDateTimePicker() : base()
        {
            base.Format = DateTimePickerFormat.Custom;
            UpdateFormat();
        }

        [Browsable(true)]
        [Category("Behavior")]
        [Description("The DateTime value assigned to the control. Null if no date is selected.")]
        public new DateTime? Value
        {
            get { return _isNull ? (DateTime?)null : base.Value; }
            set
            {
                if (value == null)
                {
                    if (!_isNull) // Only update if changing state
                    {
                        _isNull = true;
                        _originalFormat = base.Format; // Store original format before changing
                        _originalCustomFormat = base.CustomFormat;
                        base.Format = DateTimePickerFormat.Custom;
                        base.CustomFormat = " "; // Display empty
                        OnValueChanged(EventArgs.Empty); // Notify change
                    }
                }
                else
                {
                    if (_isNull) // Restore format if was null
                    {
                         base.Format = _originalFormat;
                         base.CustomFormat = _originalCustomFormat;
                         _isNull = false;
                    }
                    base.Value = value.Value; // Set the actual value (will trigger base OnValueChanged)
                }
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new DateTimePickerFormat Format
        {
            get { return _originalFormat; }
            set
            {
                if (_originalFormat != value)
                {
                    _originalFormat = value;
                    if (!_isNull) // Apply immediately if not null
                    {
                        base.Format = value;
                        UpdateFormat();
                    }
                }
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new string CustomFormat
        {
            get { return _originalCustomFormat; }
            set
            {
                 if (_originalCustomFormat != value)
                 {
                     _originalCustomFormat = value;
                     if (!_isNull) // Apply immediately if not null
                     {
                         base.CustomFormat = value;
                         UpdateFormat();
                     }
                 }
            }
        }


        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Delete)
            {
                this.Value = null; // Set to null on Delete key press
            }
        }

        protected override void OnValueChanged(EventArgs eventargs)
        {
            // This override ensures that when the user picks a date (changing from null state),
            // the format is restored correctly before the base ValueChanged event fires.
            if (_isNull)
            {
                 base.Format = _originalFormat;
                 base.CustomFormat = _originalCustomFormat;
                 _isNull = false;
            }
            base.OnValueChanged(eventargs);
        }

        private void UpdateFormat()
        {
            if (base.Format == DateTimePickerFormat.Custom && string.IsNullOrWhiteSpace(base.CustomFormat))
            {
                base.CustomFormat = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
            }
        }
    }
}
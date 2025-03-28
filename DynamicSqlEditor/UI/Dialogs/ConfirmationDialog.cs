using System;
using System.Windows.Forms;

namespace DynamicSqlEditor.UI.Dialogs
{
    // Simple wrapper if needed, otherwise use MessageBox directly.
    public static class ConfirmationDialog
    {
        public static DialogResult Show(string message, string caption = "Confirm", MessageBoxButtons buttons = MessageBoxButtons.YesNo, MessageBoxIcon icon = MessageBoxIcon.Question)
        {
            // Ensure dialog shows on top if called from non-UI thread context (though ideally UI calls are marshalled)
            Form topForm = Application.OpenForms.Count > 0 ? Application.OpenForms[Application.OpenForms.Count - 1] : null;
            return MessageBox.Show(topForm ?? new Form { TopMost = true }, message, caption, buttons, icon);
        }
    }
}
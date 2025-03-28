using System;
using System.Windows.Forms;
using DynamicSqlEditor.Common;
using DynamicSqlEditor.UI;

namespace DynamicSqlEditor
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            GlobalExceptionHandler.Initialize();
            FileLogger.Initialize("Logs");

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                FileLogger.Error("Unhandled exception in Main.", ex);
                MessageBox.Show($"An unexpected error occurred: {ex.Message}\nPlease check the log file for details.", "Application Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                FileLogger.Shutdown();
            }
        }
    }
}
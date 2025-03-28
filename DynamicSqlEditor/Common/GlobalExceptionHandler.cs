using System;
using System.Threading;
using System.Windows.Forms;

namespace DynamicSqlEditor.Common
{
    public static class GlobalExceptionHandler
    {
        public static void Initialize()
        {
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            string message = "An unhandled non-UI exception occurred.";
            if (exception != null)
            {
                message = $"An unhandled non-UI exception occurred: {exception.Message}";
                FileLogger.Error(message, exception);
            }
            else
            {
                message = $"An unhandled non-UI exception occurred: {e.ExceptionObject}";
                FileLogger.Error(message);
            }

            MessageBox.Show(message + "\nPlease check the log file for details.", "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            string message = $"An unhandled UI exception occurred: {e.Exception.Message}";
            FileLogger.Error(message, e.Exception);
            MessageBox.Show(message + "\nPlease check the log file for details.", "UI Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
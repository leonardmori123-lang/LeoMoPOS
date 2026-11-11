using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LeoMoPOS
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            AppLog.DeleteOldLogs();
            Application.Run(new LoginForm());
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            AppLog.ReportUnexpected(e.Exception, false);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = null;
            if (e.ExceptionObject is Exception)
            {
                ex = (Exception)e.ExceptionObject;
            }
            AppLog.ReportUnexpected(ex, true);
        }
    }

    public static class AppLog
    {
        private const int KeepDays = 30;

        public static string Folder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LeoMoPOS", "Logs");
            }
        }

        public static string Error(string what, Exception ex)
        {
            string reference = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                               + "-" + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();

            try
            {
                Directory.CreateDirectory(Folder);
                string file = Path.Combine(Folder, "pos-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");

                StringBuilder text = new StringBuilder();
                text.AppendLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "] " + reference + " " + what);
                if (ex == null)
                {
                    text.AppendLine("(no exception details)");
                }
                else
                {
                    text.AppendLine(ex.ToString());
                }
                text.AppendLine();

                File.AppendAllText(file, text.ToString(), Encoding.UTF8);
            }
            catch
            {
            }

            return reference;
        }

        public static void ReportUnexpected(Exception ex, bool closing)
        {
            try
            {
                string eventName;
                if (closing)
                {
                    eventName = "Unhandled exception (program closing)";
                }
                else
                {
                    eventName = "Unhandled exception";
                }
                string reference = Error(eventName, ex);

                string closingNote;
                if (closing)
                {
                    closingNote = "The program has to close. ";
                }
                else
                {
                    closingNote = string.Empty;
                }

                MessageBox.Show(
                    "Something went wrong, and the problem was recorded." + Environment.NewLine + Environment.NewLine +
                    closingNote +
                    "If it keeps happening, tell the administrator and give this reference: " + reference,
                    "Enterprise POS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
            }
        }

        public static void DeleteOldLogs()
        {
            try
            {
                if (!Directory.Exists(Folder))
                {
                    return;
                }

                foreach (string file in Directory.GetFiles(Folder, "pos-*.log"))
                {
                    if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-KeepDays))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
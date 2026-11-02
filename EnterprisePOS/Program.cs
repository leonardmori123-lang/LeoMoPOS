using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace EnterprisePOS
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Any error nobody caught ends up here, not in Windows' own crash dialog.
            // This has to be set before the first window is created.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => AppLog.ReportUnexpected(e.Exception, false);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => AppLog.ReportUnexpected(e.ExceptionObject as Exception, true);

            AppLog.DeleteOldLogs();
            Application.Run(new LoginForm());
        }
    }

    // Writes errors to a text file for support, and shows the user a short message that
    // never contains SQL text, server names or connection strings.
    public static class AppLog
    {
        private const int KeepDays = 30;

        // %LocalAppData%\EnterprisePOS\Logs. Every Windows user can write there, and other users cannot read it.
        public static string Folder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EnterprisePOS", "Logs");
            }
        }

        // Adds an entry to today's log file (one file per day) and returns a short reference
        // the user can quote. The full exception, with its stack trace, goes into the file.
        public static string Error(string what, Exception ex)
        {
            string reference = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                               + "-" + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();

            try
            {
                Directory.CreateDirectory(Folder);
                string file = Path.Combine(Folder, "pos-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");

                var text = new StringBuilder();
                text.AppendLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "] " + reference + " " + what);
                text.AppendLine(ex == null ? "(no exception details)" : ex.ToString());
                text.AppendLine();

                File.AppendAllText(file, text.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Writing the log must never cause a second error.
            }

            return reference;
        }

        // The safe message for an error nobody planned for
        public static void ReportUnexpected(Exception ex, bool closing)
        {
            try
            {
                string reference = Error(closing ? "Unhandled exception (program closing)" : "Unhandled exception", ex);

                MessageBox.Show(
                    "Something went wrong, and the problem was recorded." + Environment.NewLine + Environment.NewLine +
                    (closing ? "The program has to close. " : string.Empty) +
                    "If it keeps happening, tell the administrator and give this reference: " + reference,
                    "Enterprise POS", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // Nothing more can be done here.
            }
        }

        // Rotation: log files older than 30 days are removed when the program starts.
        public static void DeleteOldLogs()
        {
            try
            {
                if (!Directory.Exists(Folder)) return;

                foreach (var file in Directory.GetFiles(Folder, "pos-*.log"))
                {
                    if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-KeepDays)) File.Delete(file);
                }
            }
            catch
            {
                // Old logs staying a little longer is harmless.
            }
        }
    }
}
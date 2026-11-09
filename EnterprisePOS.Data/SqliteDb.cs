using System;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Text;

namespace EnterprisePOS.Data
{
    public static class SqliteDb
    {
        private const string ScriptName = "PosDatabase.sql";

        private static readonly object createLock = new object();
        private static bool checkedThisRun = false;

        public static string DatabasePath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EnterprisePOS");
                return Path.Combine(folder, "pos.db");
            }
        }

        private static string ConnectionString
        {
            get
            {
                SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder();
                builder.DataSource = DatabasePath;
                builder.Version = 3;
                return builder.ConnectionString;
            }
        }

        public static SQLiteConnection Open()
        {
            try
            {
                NativeLibraries.LoadOnce();
                EnsureCreated();

                SQLiteConnection connection = new SQLiteConnection(ConnectionString);
                try
                {
                    connection.Open();

                    Execute(connection, "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 10000;");
                    return connection;
                }
                catch
                {
                    connection.Dispose();
                    throw;
                }
            }
            catch (SQLiteException ex)
            {
                throw new InvalidOperationException(
                    "Cannot open the program's database file. Close other copies of the program and try again.", ex);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    "Cannot create the program's database file. Check that this Windows account can write to its own AppData folder.", ex);
            }
        }

        public static void EnsureCreated()
        {
            NativeLibraries.LoadOnce();
            lock (createLock)
            {
                if (checkedThisRun && File.Exists(DatabasePath)) return;

                Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));

                using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();

                    Execute(connection, "PRAGMA busy_timeout = 10000;");
                    Execute(connection, "BEGIN IMMEDIATE;");
                    try
                    {
                        if (!TableExists(connection, "Users"))
                        {
                            Execute(connection, ReadScript());
                        }
                        Execute(connection, "COMMIT;");
                    }
                    catch
                    {
                        try
                        {
                            Execute(connection, "ROLLBACK;");
                        }
                        catch
                        {
                        }
                        throw;
                    }
                }

                checkedThisRun = true;
            }
        }

        public static void BeginWrite(SQLiteConnection connection)
        {
            Execute(connection, "BEGIN IMMEDIATE;");
        }

        public static void Commit(SQLiteConnection connection)
        {
            Execute(connection, "COMMIT;");
        }

        public static void Rollback(SQLiteConnection connection)
        {
            try
            {
                Execute(connection, "ROLLBACK;");
            }
            catch
            {
            }
        }
        public static string Describe()
        {
            using (SQLiteConnection connection = Open())
            {
                string text = "SQLite file:" + Environment.NewLine + DatabasePath + Environment.NewLine + Environment.NewLine;
                text += "Users: " + CountRows(connection, "Users") + Environment.NewLine;
                text += "Categories: " + CountRows(connection, "Categories") + Environment.NewLine;
                text += "Products: " + CountRows(connection, "Products") + Environment.NewLine;
                text += "Orders: " + CountRows(connection, "Orders");
                return text;
            }
        }

        private static void Execute(SQLiteConnection connection, string sql)
        {
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private static bool TableExists(SQLiteConnection connection, string tableName)
        {
            string sql = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name";
            using (SQLiteCommand command = new SQLiteCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@name", tableName);
                long count = Convert.ToInt64(command.ExecuteScalar());
                return count > 0;
            }
        }
        private static long CountRows(SQLiteConnection connection, string tableName)
        {
            using (SQLiteCommand command = new SQLiteCommand("SELECT COUNT(*) FROM " + tableName, connection))
            {
                return Convert.ToInt64(command.ExecuteScalar());
            }
        }
        private static string ReadScript()
        {
            Assembly assembly = typeof(SqliteDb).Assembly;

            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith(ScriptName, StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream stream = assembly.GetManifestResourceStream(name))
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }

            throw new InvalidOperationException(
                ScriptName + " is not inside the program. In Visual Studio, click the file and set Build Action to Embedded Resource.");
        }
    }
}
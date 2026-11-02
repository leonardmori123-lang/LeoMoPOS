using System;
using System.Configuration;
using System.Data.SqlClient;

namespace EnterprisePOS.Data
{
    // Reads the "EnterprisePOS" connection string from App.config
    // and hands out open connections. Every repository uses this.
    public static class Db
    {
        public static string ConnectionString
        {
            get
            {
                var setting = ConfigurationManager.ConnectionStrings["EnterprisePOS"];
                if (setting == null)
                    throw new InvalidOperationException("The \"EnterprisePOS\" connection string is missing from the configuration file.");
                return setting.ConnectionString;
            }
        }

        public static SqlConnection Open()
        {
            var connection = new SqlConnection(ConnectionString);
            try
            {
                connection.Open();
                return connection;
            }
            catch (SqlException ex)
            {
                connection.Dispose();

                // The screen gets a plain message. The original error stays attached for the log.
                throw new InvalidOperationException(
                    "Cannot reach the database. Check that SQL Server is running and the connection settings are correct, then try again.", ex);
            }
        }
    }
}
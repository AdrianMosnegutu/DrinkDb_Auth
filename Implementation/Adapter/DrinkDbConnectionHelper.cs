using System;
using System.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Protocols;

namespace DrinkDb_Auth.Adapter
{
    public static class DrinkDbConnectionHelper
    {
        /// <summary>
        /// Reads the DrinkDbConnection string from App.config, opens a SqlConnection, and returns it.
        /// </summary>
        /// <returns>An open SqlConnection object.</returns>
        public static SqlConnection GetConnection()
        {
            // Get the connection string from App.config
            string connectionString = ConfigurationManager.ConnectionStrings["DrinkDbConnection"].ConnectionString;

            // Create and open a new SqlConnection
            SqlConnection connection = new(connectionString);
            connection.Open();
            return connection;
        }
    }
}

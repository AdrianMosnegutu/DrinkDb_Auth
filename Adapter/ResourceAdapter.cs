using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Database;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Database
{
    public class ResourceAdapter : IResourceAdapter
    {
        private readonly string connectionString;

        public ResourceAdapter()
        {
            connectionString = ConfigurationManager.ConnectionStrings["DrinkDbConnection"].ConnectionString;
        }

        SqlConnection conn = new SqlConnection();
        SqlCommand cmd = new SqlCommand();




    }
}

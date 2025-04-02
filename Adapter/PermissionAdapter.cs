using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Adapter
{
    public class PermissionAdapter : IPermissionAdapter
    {
        private readonly string connectionString;

        public PermissionAdapter()
        {
            connectionString = ConfigurationManager.ConnectionStrings["DrinkDbConnection"].ConnectionString;
        }

        
    }
}

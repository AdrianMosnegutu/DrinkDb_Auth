using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Configuration;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Database;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Database
{
    public class RoleAdapter : IRoleAdapter
    {
        private readonly string connectionString;

        public RoleAdapter()
        {
            connectionString = ConfigurationManager.ConnectionStrings["DrinkDbConnection"].ConnectionString;
        }

        public void CreateResource(Roles resource)
        {
            string query = "INSERT INTO Resources (Name, Description) VALUES (@Name, @Description)";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Name", resource.Name);
                cmd.Parameters.AddWithValue("@Description", resource.Description);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateResource(Roles resource)
        {
            string query = "UPDATE Resources SET Name=@Name, Description=@Description WHERE Id=@Id";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Name", resource.Name);
                cmd.Parameters.AddWithValue("@Description", resource.Description);
                cmd.Parameters.AddWithValue("@Id", resource.Id);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteResource(Roles resource)
        {
            string query = "DELETE FROM Resources WHERE Id=@Id";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", resource.Id);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public Roles GetResourceById(int id)
        {
            Roles resource = null;
            string query = "SELECT Id, Name, Description FROM Resources WHERE Id=@Id";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        resource = new Roles
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Description = reader.GetString(2)
                        };
                    }
                }
            }
            return resource;
        }

        public List<Roles> GetResources()
        {
            List<Roles> resources = new List<Roles>();
            string query = "SELECT Id, Name, Description FROM Resources";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Roles resource = new Roles
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Description = reader.GetString(2)
                        };
                        resources.Add(resource);
                    }
                }
            }
            return resources;
        }

    }
}

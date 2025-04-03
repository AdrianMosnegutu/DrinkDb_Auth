using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
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

        public void CreatePermission(Permission permission)
        {
            string query = @"INSERT INTO Permissions (permissionName, resource, action)
                             VALUES (@PermissionName, @Role, @Action)";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@PermissionName", permission.PermissionName);
                cmd.Parameters.AddWithValue("@Role", permission.Resource);
                cmd.Parameters.AddWithValue("@Action", permission.Action);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdatePermission(Permission permission)
        {
            string query = @"UPDATE Permissions 
                             SET permissionName=@PermissionName, resource=@Role, action=@Action 
                             WHERE Id=@Id";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@PermissionName", permission.PermissionName);
                cmd.Parameters.AddWithValue("@Role", permission.Resource);
                cmd.Parameters.AddWithValue("@Action", permission.Action);
                cmd.Parameters.AddWithValue("@Id", permission.Id);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void DeletePermission(Permission permission)
        {
            string query = "DELETE FROM Permissions WHERE Id=@Id";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", permission.Id);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public Permission GetPermissionById(int id)
        {
            string query = "SELECT Id, permissionName, resource, action FROM Permissions WHERE Id=@Id";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Permission
                        {
                            Id = reader.GetInt32(0),
                            PermissionName = reader.GetString(1),
                            Resource = reader.GetString(2),
                            Action = reader.GetString(3)
                        };
                    }
                }
            }
            throw new Exception("Permission not found");
        }

        public List<Permission> GetPermissions()
        {
            List<Permission> permissions = new List<Permission>();
            string query = "SELECT Id, permissionName, resource, action FROM Permissions";
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Permission permission = new Permission
                        {
                            Id = reader.GetInt32(0),
                            PermissionName = reader.GetString(1),
                            Resource = reader.GetString(2),
                            Action = reader.GetString(3)
                        };
                        permissions.Add(permission);
                    }
                }
            }
            return permissions;
        }
    }
}

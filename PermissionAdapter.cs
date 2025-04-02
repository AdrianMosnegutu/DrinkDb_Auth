using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;
using DrinkDb_Auth.Adapter;

namespace DrinkDb_Auth.Database
{
    public class PermissionAdapter
    {
        private readonly string connectionString;

        public PermissionAdapter()
        {
            connectionString = ConfigurationManager.ConnectionStrings["DrinkDbConnection"].ConnectionString;
        }

        public void InsertPermission(Permission permission)
        {
            string query = @"INSERT INTO Permissions (permissionName, resource, action)
                             VALUES (@PermissionName, @Resource, @Action)";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@PermissionName", permission.PermissionName);
                cmd.Parameters.AddWithValue("@Resource", permission.Resource);
                cmd.Parameters.AddWithValue("@Action", permission.Action);

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public List<Permission> GetPermissions()
        {
            List<Permission> permissions = new List<Permission>();
            string query = "SELECT permissionId, permissionName, resource, action FROM Permissions";

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
                            PermissionId = reader.GetGuid(0),
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

        /*public Permission GetPermissionById(Guid permissionId)
        {
            Permission permission = null;
            string query = "SELECT permissionId, permissionName, resource, action FROM Permissions WHERE permissionId = @PermissionId";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@PermissionId", permissionId);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        permission = new Permission
                        {
                            PermissionId = reader.GetGuid(0),
                            PermissionName = reader.GetString(1),
                            Resource = reader.GetString(2),
                            Action = reader.GetString(3)
                        };
                    }
                }
            }
            return permission;
        }*/

        public void UpdatePermission(Permission permission)
        {
            string query = @"UPDATE Permissions 
                             SET permissionName = @PermissionName, resource = @Resource, action = @Action 
                             WHERE permissionId = @PermissionId";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@PermissionName", permission.PermissionName);
                cmd.Parameters.AddWithValue("@Resource", permission.Resource);
                cmd.Parameters.AddWithValue("@Action", permission.Action);
                cmd.Parameters.AddWithValue("@PermissionId", permission.PermissionId);

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void DeletePermission(Guid permissionId)
        {
            string query = "DELETE FROM Permissions WHERE permissionId = @PermissionId";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@PermissionId", permissionId);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}

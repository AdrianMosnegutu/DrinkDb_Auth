using System;
using Microsoft.Data.SqlClient;
using DrinkDb_Auth.Model;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace DrinkDb_Auth.Adapter
{
    public class UserAdapter : IUserAdapter
    {
        /// <summary>
        /// Calls your T-SQL function fnGetUserById(@userId) 
        /// which returns a row from the Users table.
        /// </summary>
        /// 
        public Users GetUserById(Guid userId)
        {
            using (SqlConnection conn = DrinkDbConnectionHelper.GetConnection())
            {
                string sql = "SELECT * FROM fnGetUserById(@userId);";
                using (SqlCommand cmd = new(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Users
                            {
                                UserId = reader.GetGuid(reader.GetOrdinal("userId")),
                                Username = reader.GetString(reader.GetOrdinal("userName")),
                                PasswordHash = reader.GetString(reader.GetOrdinal("passwordHash")),
                                TwoFASecret = reader.IsDBNull(reader.GetOrdinal("twoFASecret"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("twoFASecret")),
                            };
                        }
                        else
                        {
                            throw new Exception($"User with ID {userId} not found.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calls fnGetUserByUsername(@username).
        /// </summary>
        public Users GetUserByUsername(string username)
        {
            using (SqlConnection conn = DrinkDbConnectionHelper.GetConnection())
            {
                string sql = "SELECT * FROM fnGetUserByUsername(@username);";
                using (SqlCommand cmd = new(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Users
                            {
                                UserId = reader.GetGuid(reader.GetOrdinal("userId")),
                                Username = reader.GetString(reader.GetOrdinal("userName")),
                                PasswordHash = reader.GetString(reader.GetOrdinal("passwordHash")),
                                TwoFASecret = reader.IsDBNull(reader.GetOrdinal("twoFASecret"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("twoFASecret")),
                            };
                        }
                        else
                        {
                            throw new Exception($"User with username {username} not found.");
                        }
                    }
                }
            }
        }

        public bool UpdateUser(User user)
        {
            using SqlConnection conn = DrinkDbConnectionHelper.GetConnection();
            string sql = "UPDATE Users SET userName = @username, passwordHash = @passwordHash, twoFASecret = @twoFASecret WHERE userId = @userId;";
            using (SqlCommand cmd = new(sql, conn))
            {
                cmd.Parameters.AddWithValue("@userId", user.UserId);
                cmd.Parameters.AddWithValue("@username", user.Username);
                cmd.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
                cmd.Parameters.AddWithValue("@twoFASecret", user.TwoFASecret);
                return cmd.ExecuteNonQuery() > 0;
            }
        }
        public bool DeleteUser(Guid userId)
        {
            using SqlConnection conn = DrinkDbConnectionHelper.GetConnection();
            string sql = "DELETE FROM Users WHERE userId = @userId;";
            using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@userId", userId);
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool CreateUser(User user)
        {
            using SqlConnection conn = DrinkDbConnectionHelper.GetConnection();
            string sql = "INSERT INTO Users (userId, userName, passwordHash, twoFASecret) VALUES (@userId, @username, @passwordHash, @twoFASecret);";
            using SqlCommand cmd = new(sql, conn);
            cmd.Parameters.AddWithValue("@userId", user.UserId);
            cmd.Parameters.AddWithValue("@username", user.Username);
            cmd.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
            cmd.Parameters.AddWithValue("@twoFASecret", user.TwoFASecret);
            return cmd.ExecuteNonQuery() > 0;
        }

        private List<Permission> GetPermissionsForUser(Guid userId) 
        {
            List<Permission> permissions = new();

            // SQL query joining Users -> UserRoles -> Roles -> RolePermissions -> Permissions
            string sql = @"
        SELECT p.permissionId, p.permissionName, p.resource, p.action
        FROM Users u
        JOIN UserRoles ur ON u.userId = ur.userId
        JOIN Roles r ON ur.roleId = r.roleId
        JOIN RolePermissions rp ON r.roleId = rp.roleId
        JOIN Permissions p ON rp.permissionId = p.permissionId
        WHERE u.userId = @userId;
    ";

            using (SqlConnection conn = DrinkDbConnectionHelper.GetConnection())
            using (SqlCommand cmd = new(sql, conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);

                conn.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Permission permission = new()
                        {
                            PermissionId = reader.GetGuid(reader.GetOrdinal("permissionId")),
                            PermissionName = reader.GetString(reader.GetOrdinal("permissionName")),
                            Resource = reader.GetString(reader.GetOrdinal("resource")),
                            Action = reader.GetString(reader.GetOrdinal("action"))
                        };
                        permissions.Add(permission);
                    }
                }
            }

            return permissions;
        }

        public bool ValidateAction(Guid userId, string resource, string action)
        {
            bool result = false;
            string sql = "SELECT dbo.fnValidateAction(@userId, @resource, @action)";

            using (SqlConnection conn = DrinkDbConnectionHelper.GetConnection())
            using (SqlCommand cmd = new(sql, conn))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@resource", resource);
                cmd.Parameters.AddWithValue("@action", action);

                conn.Open();
                var scalarResult = cmd.ExecuteScalar();
                if (scalarResult != null && scalarResult != DBNull.Value)
                {
                    result = Convert.ToBoolean(scalarResult);
                }
            }

            return result;
        }


    }
}

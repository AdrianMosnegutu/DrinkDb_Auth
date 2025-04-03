using System;
using Microsoft.Data.SqlClient;
using DrinkDb_Auth.Model;
using System.Collections.Generic;

namespace DrinkDb_Auth.Adapter
{
    public class UserAdapter : IUserAdapter
    {
        /// <summary>
        /// Calls your T-SQL function fnGetUserById(@userId) 
        /// which returns a row from the Users table.
        /// </summary>
        /// 
        public User GetUserById(Guid userId)
        {
            using (SqlConnection conn = DrinkDbConnectionHelper.GetConnection())
            {
                string sql = "SELECT * FROM fnGetUserById(@userId);";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new User
                            {
                                UserId = reader.GetGuid(reader.GetOrdinal("userId")),
                                Username = reader.GetString(reader.GetOrdinal("userName")),
                                PasswordHash = reader.GetString(reader.GetOrdinal("passwordHash")),
                                TwoFASecret = reader.IsDBNull(reader.GetOrdinal("twoFASecret"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("twoFASecret")),
                                RoleId = reader.GetGuid(reader.GetOrdinal("roleId"))
                            };
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calls fnGetUserByUsername(@username).
        /// </summary>
        public User GetUserByUsername(string username)
        {
            using (SqlConnection conn = DrinkDbConnectionHelper.GetConnection())
            {
                string sql = "SELECT * FROM fnGetUserByUsername(@username);";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new User
                            {
                                UserId = reader.GetGuid(reader.GetOrdinal("userId")),
                                Username = reader.GetString(reader.GetOrdinal("userName")),
                                PasswordHash = reader.GetString(reader.GetOrdinal("passwordHash")),
                                TwoFASecret = reader.IsDBNull(reader.GetOrdinal("twoFASecret"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("twoFASecret")),
                                RoleId = reader.GetGuid(reader.GetOrdinal("roleId"))
                            };
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }

        private List<Permission> GetPermissionsForUser(Guid userId) { }

    }
}

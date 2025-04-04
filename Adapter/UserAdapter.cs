using System;
using Microsoft.Data.SqlClient;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.Adapter
{
    public class UserAdapter
    {
        /// <summary>
        /// Calls your T-SQL function fnGetUserById(@userId) 
        /// which returns a row from the Users table.
        /// </summary>
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

        /// <summary>
        /// Calls fnValidateAction(@userId, @resource, @action) which returns BIT.
        /// </summary>
        public bool ValidateAction(Guid userId, string resource, string action)
        {
            using (SqlConnection conn = DrinkDbConnectionHelper.GetConnection())
            {
                // The function returns BIT: 1 (true) or 0 (false)
                string sql = "SELECT dbo.fnValidateAction(@userId, @resource, @action) as Allowed;";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@resource", resource);
                    cmd.Parameters.AddWithValue("@action", action);

                    object result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        int allowed = Convert.ToInt32(result);
                        return (allowed == 1);
                    }
                    return false;
                }
            }
        }
    }
}

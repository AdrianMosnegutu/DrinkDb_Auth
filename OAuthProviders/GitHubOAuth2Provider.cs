using System;
using System.Configuration;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DrinkDb_Auth.OAuthProviders;
using Microsoft.Data.SqlClient;

namespace DrinkDb_Auth.OAuthProviders
{
    public class GitHubOAuth2Provider : GenericOAuth2Provider
    {
        public AuthResponse Authenticate(string userId, string token)
        {
            try
            {
                var (ghId, ghLogin, ghEmail) = FetchGitHubUserInfo(token);

                if (string.IsNullOrEmpty(ghLogin))
                {
                    return new AuthResponse
                    {
                        AuthSuccessful = false,
                        SessionToken = token,
                        NewAccount = false
                    };
                }

                // Check if a user exists by using the GitHub username.
                if (VerifyUserInDb(ghLogin))
                {
                    // User exists, so proceed.
                    return new AuthResponse
                    {
                        AuthSuccessful = true,
                        SessionToken = token,
                        NewAccount = false
                    };
                }
                else
                {
                    // User does not exist. Insert the new user.
                    Guid newUserId = CreateUserFromGitHub(ghLogin, ghEmail);
                    if (newUserId != Guid.Empty)
                    {
                        // Successfully inserted, so login is successful.
                        return new AuthResponse
                        {
                            AuthSuccessful = true,
                            SessionToken = token,
                            NewAccount = true
                        };
                    }
                    else
                    {
                        // Insertion failed.
                        return new AuthResponse
                        {
                            AuthSuccessful = false,
                            SessionToken = token,
                            NewAccount = false
                        };
                    }
                }
            }
            catch (Exception)
            {
                return new AuthResponse
                {
                    AuthSuccessful = false,
                    SessionToken = token,
                    NewAccount = false
                };
            }
        }

        private (string ghId, string ghLogin, string ghEmail) FetchGitHubUserInfo(string token)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
                client.DefaultRequestHeaders.Add("User-Agent", "DrinkDb_Auth-App");

                var response = client.GetAsync("https://api.github.com/user").Result;
                if (!response.IsSuccessStatusCode)
                    return (null, null, null);

                string userJson = response.Content.ReadAsStringAsync().Result;
                using (JsonDocument doc = JsonDocument.Parse(userJson))
                {
                    var root = doc.RootElement;
                    string ghId = root.GetProperty("id").GetRawText();
                    string ghLogin = root.GetProperty("login").GetString();
                    string ghEmail = null;
                    if (root.TryGetProperty("email", out JsonElement emailElement) && emailElement.ValueKind != JsonValueKind.Null)
                    {
                        ghEmail = emailElement.GetString();
                    }
                    return (ghId, ghLogin, ghEmail);
                }
            }
        }

        public static async Task<string> GetGitHubUsernameAsync(string token)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
                client.DefaultRequestHeaders.Add("User-Agent", "DrinkDb_Auth-App");
                var response = await client.GetAsync("https://api.github.com/user");
                if (!response.IsSuccessStatusCode)
                    return null;
                string userJson = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(userJson))
                {
                    var root = doc.RootElement;
                    return root.GetProperty("login").GetString();
                }
            }
        }

        private bool VerifyUserInDb(string ghLogin)
        {
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["DrinkDbConnection"].ConnectionString))
            {
                conn.Open();
                // Using the existing function fnGetUserByUsername.
                string sql = "SELECT COUNT(*) FROM fnGetUserByUsername(@username)";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@username", ghLogin.Trim());
                    int count = (int)cmd.ExecuteScalar();
                    Console.WriteLine($"User lookup count for '{ghLogin}': {count}");
                    return count > 0;
                }
            }
        }

        private Guid CreateUserFromGitHub(string ghLogin, string ghEmail)
        {
            try
            {
                Guid newUserId = Guid.NewGuid();
                string connectionString = ConfigurationManager.ConnectionStrings["DrinkDbConnection"].ConnectionString;
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Insert into Users table using the GitHub username.
                    // Note: passwordHash is left empty, and twoFASecret is NULL.
                    string sql = @"
                    INSERT INTO Users (userId, userName, passwordHash, twoFASecret, roleId) 
                    VALUES (@userId, @userName, @passwordHash, NULL, @roleId)";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", newUserId);
                        cmd.Parameters.AddWithValue("@userName", ghLogin.Trim());
                        cmd.Parameters.AddWithValue("@passwordHash", ""); // Placeholder since OAuth doesn't use it.
                        cmd.Parameters.AddWithValue("@roleId", GetDefaultRoleId(conn));
                        int result = cmd.ExecuteNonQuery();
                        Console.WriteLine($"Inserted new user {ghLogin}. Rows affected: {result}");
                        if (result > 0)
                            return newUserId;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating user: " + ex.Message);
            }
            return Guid.Empty;
        }

        private Guid GetDefaultRoleId(SqlConnection conn)
        {
            // Retrieve a valid roleId from the Roles table.
            string sql = "SELECT TOP 1 roleId FROM Roles ORDER BY roleName";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                object result = cmd.ExecuteScalar();
                if (result != null)
                {
                    return (Guid)result;
                }
            }
            throw new Exception("No default role found in Roles table.");
        }
    }
}

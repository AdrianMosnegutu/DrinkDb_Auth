using System;
using System.Configuration;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;
using DrinkDb_Auth.OAuthProviders;
using Microsoft.Data.SqlClient;

namespace DrinkDb_Auth.OAuthProviders
{
    public class GitHubOAuth2Provider : GenericOAuth2Provider
    {
        private readonly static UserAdapter userAdapter = new UserAdapter();
        public AuthResponse Authenticate(string? userId, string token)
        {
            try
            {
                var (ghId, ghLogin) = FetchGitHubUserInfo(token);

                if (string.IsNullOrEmpty(ghLogin))
                {
                    return new AuthResponse
                    {
                        AuthSuccessful = false,
                        OAuthToken = String.Empty,
                        SessionId = Guid.Empty,
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
                        OAuthToken = token,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    };
                }
                else
                {
                    // User does not exist. Insert the new user.
                    Guid newUserId = CreateUserFromGitHub(ghLogin);
                    if (newUserId != Guid.Empty)
                    {
                        // Successfully inserted, so login is successful.
                        return new AuthResponse
                        {
                            AuthSuccessful = true,
                            OAuthToken  = token,
                            SessionId = Guid.Empty,
                            NewAccount = true
                        };
                    }
                    else
                    {
                        // Insertion failed.
                        return new AuthResponse
                        {
                            AuthSuccessful = false,
                            OAuthToken  = token,
                            SessionId = Guid.Empty,
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
                    OAuthToken  = token,
                    SessionId = Guid.Empty,
                    NewAccount = false
                };
            }
        }

        private (string ghId, string ghLogin) FetchGitHubUserInfo(string token)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
                client.DefaultRequestHeaders.Add("User-Agent", "DrinkDb_Auth-App");

                var response = client.GetAsync("https://api.github.com/user").Result;
                if (!response.IsSuccessStatusCode)
                    throw new Exception("Failed to fetch user info from GitHub.");

                string userJson = response.Content.ReadAsStringAsync().Result;
                using (JsonDocument doc = JsonDocument.Parse(userJson))
                {
                    var root = doc.RootElement;
                    string ghId = root.GetProperty("id").GetRawText();
                    string? ghLogin = root.GetProperty("login").GetString();
                    if (ghLogin == null)
                    {
                        throw new Exception("GitHub login is null.");
                    }
                    return (ghId, ghLogin);
                }
            }
        }

        public static async Task<string?> GetGitHubUsernameAsync(string token)
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

        private Guid CreateUserFromGitHub(string ghLogin)
        {
            try
            {
                User newUser = new()
                {
                    UserId = Guid.NewGuid(),
                    Username = ghLogin.Trim(),
                    TwoFASecret = string.Empty,
                    PasswordHash = string.Empty,
                };
                userAdapter.CreateUser(newUser);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error creating user: " + ex.Message);
            }
            return Guid.Empty;
        }

    }
}

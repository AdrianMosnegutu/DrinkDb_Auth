using System;
using System.Configuration;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;
using Microsoft.Data.SqlClient;

namespace DrinkDb_Auth.OAuthProviders
{
    public class LinkedInOAuth2Provider : GenericOAuth2Provider
    {
        private readonly static UserAdapter userAdapter = new();
        private readonly static SessionAdapter sessionAdapter = new();
        /// <summary>
        /// Performs authentication using the access token, fetches user info via OpenID Connect, and stores/updates the user.
        /// </summary>
        public AuthenticationResponse Authenticate(string userId, string token)
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Add("User-Agent", "DrinkDb_Auth-App");
            var response = client.GetAsync("https://api.linkedin.com/v2/userinfo").Result;
            if (!response.IsSuccessStatusCode)
                throw new Exception("Failed to fetch user info from Linkedin.");

            string json = response.Content.ReadAsStringAsync().Result;

            using JsonDocument doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string id = root.GetProperty("sub").GetString() ?? throw new Exception("LinkedIn ID not found in response.");
            string name = root.GetProperty("name").GetString() ?? throw new Exception("LinkedIn name not found in response.");

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
            {
                Debug.WriteLine("LinkedIn ID or name is empty.");
                return new AuthenticationResponse
                {
                    AuthenticationSuccesfull = false,
                    OAuthenticationToken = string.Empty,
                    SessionId = Guid.Empty,
                    NewAccount = false
                };
            }

            var user = userAdapter.GetUserByUsername(name);
            if (user == null)
            {
                User newUser = new User
                {
                    Username = name,
                    PasswordHash = string.Empty,
                    UserId = Guid.NewGuid(),
                    TwoFASecret = string.Empty,
                };
                userAdapter.CreateUser(newUser);
                Session session = sessionAdapter.CreateSession(newUser.UserId);
                return new AuthenticationResponse
                {
                    AuthenticationSuccesfull = true,
                    OAuthenticationToken = token,
                    SessionId = session.sessionId,
                    NewAccount = true
                };
            }
            else
            {
                return new AuthenticationResponse
                {
                    AuthenticationSuccesfull = true,
                    OAuthenticationToken = token,
                    SessionId = user.UserId,
                    NewAccount = false
                };
            }
        }

        /// <summary>
        /// Calls the LinkedIn /v2/userinfo endpoint using OpenID Connect.
        /// Expected response sample:
        /// {
        ///     "sub": "782bbtaQ",
        ///     "name": "John Doe",
        ///     "given_name": "John",
        ///     "family_name": "Doe",
        ///     "picture": "https://.../100_100",
        ///     "locale": "en-US",
        ///     "email": "doe@email.com",
        ///     "email_verified": true
        /// }
        /// </summary>
        private (string lnId, string fullName, string email) FetchLinkedInUserInfo(string token)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                // Call the userinfo endpoint for OpenID Connect
                var response = client.GetAsync("https://api.linkedin.com/v2/userinfo").Result;
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Failed to fetch user info from LinkedIn. Status code: {response.StatusCode}");

                string json = response.Content.ReadAsStringAsync().Result;
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    // Use "sub" as LinkedIn's unique identifier
                    string lnId = root.GetProperty("sub").ToString() ?? throw new Exception("No LinkedIn ID found in the response.");
                    string fullName = root.GetProperty("name").ToString() ?? throw new Exception("No LinkedIn ID found in the response.");
                    string email = root.GetProperty("email").ToString() ?? throw new Exception("No email found in the response.");
                    return (lnId, fullName, email);
                }
            }
        }

        /// <summary>
        /// Stores or updates the user in the database.
        /// Uses the LinkedIn ID (lnId) as the unique value for userName.
        /// </summary>
        private bool StoreOrUpdateUserInDb(string lnId, string fullName, string email)
        {
            bool isNewAccount = false;
            string connectionString = ConfigurationManager.ConnectionStrings["DrinkDbConnection"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Check if a user with this lnId already exists (stored as userName)
                string checkQuery = "SELECT COUNT(*) FROM User WHERE userName = @lnId";
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@lnId", lnId);
                    int count = (int)checkCmd.ExecuteScalar();
                    if (count == 0)
                    {
                        // Insert a new user
                        string insertQuery = @"
                            INSERT INTO User (userId, userName, passwordHash, twoFASecret, roleId)
                            VALUES (NEWID(), @lnId, '', NULL, @roleId)";
                        using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@lnId", lnId);
                            insertCmd.Parameters.AddWithValue("@roleId", GetDefaultRoleId(conn));
                            int result = insertCmd.ExecuteNonQuery();
                            if (result > 0)
                                isNewAccount = true;
                        }
                    }
                }
            }
            return isNewAccount;
        }

        private Guid GetDefaultRoleId(SqlConnection conn)
        {
            string sql = "SELECT TOP 1 roleId FROM Roles ORDER BY roleName";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                object result = cmd.ExecuteScalar();
                if (result != null)
                    return (Guid)result;
            }
            throw new Exception("No default role found in Roles table.");
        }
    }
}

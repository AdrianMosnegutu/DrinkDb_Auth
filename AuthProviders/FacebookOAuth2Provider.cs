using System;
using System.Net.Http;
using System.Text.Json;
using DrinkDb_Auth.OAuthProviders;
using Microsoft.Data.SqlClient;
using System.Configuration;
using DrinkDb_Auth.Model;
using DrinkDb_Auth.Adapter;

namespace DrinkDb_Auth.OAuthProviders
{
    public class FacebookOAuth2Provider : GenericOAuth2Provider
    {
        private static readonly SessionAdapter sessionAdapter = new();
        public AuthResponse Authenticate(string userId, string token)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = $"https://graph.facebook.com/me?fields=id,name,email&access_token={token}";
                    HttpResponseMessage response = client.GetAsync(url).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        string json = response.Content.ReadAsStringAsync().Result;
                        var doc = JsonDocument.Parse(json).RootElement;

                        if (doc.TryGetProperty("id", out var idProp))
                        {
                            string fbId = idProp.GetString() ?? throw new Exception("Facebook ID is null.");
                            string fbName = doc.GetProperty("name").GetString() ?? throw new Exception("Facebook name is null.");
                            string fbEmail = doc.GetProperty("email").GetString() ?? throw new Exception("Facebook email is null.");

                            // store or update user in DB - UserService
                            bool isNewAccount = StoreOrUpdateUserInDb(fbId, fbName, fbEmail);

                            Session session = sessionAdapter.CreateSession(Guid.Parse(fbId));

                            return new AuthResponse
                            {
                                AuthSuccessful = true,
                                SessionId = session.sessionId,
                                OAuthToken = token,
                                NewAccount = isNewAccount
                            };
                        }
                    }
                    
                    return new AuthResponse
                    {
                        AuthSuccessful = false,
                        OAuthToken = token,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    };
                }
            }
            catch
            {
                return new AuthResponse
                {
                    AuthSuccessful = false,
                    OAuthToken = token,
                    SessionId = Guid.Empty,
                    NewAccount = false
                };
            }
        }

        private bool StoreOrUpdateUserInDb(string fbId, string fbName, string fbEmail)
        {
            bool isNewAccount = false;
            string connectionString = ConfigurationManager.ConnectionStrings["DrinkDbConnection"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string checkQuery = @"
                    SELECT COUNT(*) 
                    FROM User 
                    WHERE fbId = @fbId OR userName = @fbEmail;
                ";

                using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@fbId", fbId);
                    checkCmd.Parameters.AddWithValue("@fbEmail", fbEmail);

                    int count = (int)checkCmd.ExecuteScalar();

                    if (count == 0)
                    {
                        string insertQuery = @"
                            INSERT INTO User (userId, fbId, userName, email, passwordHash, roleId)
                            VALUES (NEWID(), @fbId, @fbName, @fbEmail, '', @roleId);
                        ";
                        using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@fbId", fbId);
                            insertCmd.Parameters.AddWithValue("@fbName", fbName);
                            insertCmd.Parameters.AddWithValue("@fbEmail", fbEmail);
                            insertCmd.Parameters.AddWithValue("@roleId", Guid.NewGuid());
                            insertCmd.ExecuteNonQuery();
                        }
                        isNewAccount = true;
                    }
                    else
                    {
                        string updateQuery = @"
                            UPDATE User
                            SET userName = @fbName, email = @fbEmail
                            WHERE fbId = @fbId;
                        ";
                        using (SqlCommand updateCmd = new SqlCommand(updateQuery, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@fbId", fbId);
                            updateCmd.Parameters.AddWithValue("@fbName", fbName);
                            updateCmd.Parameters.AddWithValue("@fbEmail", fbEmail);
                            updateCmd.ExecuteNonQuery();
                        }
                        isNewAccount = false;
                    }
                }
            }
            return isNewAccount;
        }
    }
}

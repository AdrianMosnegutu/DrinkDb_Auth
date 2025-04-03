using System;
using System.Net.Http;
using System.Text.Json;
using DrinkDb_Auth.OAuthProviders;
using Microsoft.Data.SqlClient;
using System.Configuration;

namespace DrinkDb_Auth.OAuthProviders
{
    public class FacebookOAuth2Provider : GenericOAuth2Provider
    {
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
                            string fbId = idProp.GetString();
                            string fbName = doc.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                            string fbEmail = doc.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;

                            // store or update user in DB - UserService
                            bool isNewAccount = StoreOrUpdateUserInDb(fbId, fbName, fbEmail);

                            return new AuthResponse
                            {
                                AuthSuccessful = true,
                                SessionToken = token,
                                NewAccount = isNewAccount
                            };
                        }
                    }
                    
                    return new AuthResponse
                    {
                        AuthSuccessful = false,
                        SessionToken = token,
                        NewAccount = false
                    };
                }
            }
            catch
            {
                return new AuthResponse
                {
                    AuthSuccessful = false,
                    SessionToken = token,
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
                    FROM Users 
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
                            INSERT INTO Users (userId, fbId, userName, email, passwordHash, roleId)
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
                            UPDATE Users
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

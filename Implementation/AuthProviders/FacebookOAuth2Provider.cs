using System;
using System.Net.Http;
using System.Text.Json;
using DrinkDb_Auth.OAuthProviders;
using Microsoft.Data.SqlClient;
using System.Configuration;
using DrinkDb_Auth.Model;
using DrinkDb_Auth.Adapter;
using Windows.Networking.Sockets;

namespace DrinkDb_Auth.OAuthProviders
{
    public class FacebookOAuth2Provider : GenericOAuth2Provider
    {
        private static readonly SessionAdapter sessionAdapter = new();
        public AuthenticationResponse Authenticate(string userId, string token)
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

                            // store or update user in DB - UserService
                            bool isNewAccount = StoreOrUpdateUserInDb(fbId, fbName);

                            User user = userAdapter.GetUserByUsername(fbName) ?? throw new Exception("User not found");

                            Session session = sessionAdapter.CreateSession(user.UserId);

                            return new AuthenticationResponse
                            {
                                AuthenticationSuccessful = true,
                                SessionId = session.SessionId,
                                OAuthToken = token,
                                NewAccount = isNewAccount
                            };
                        }
                    }
                    
                    return new AuthenticationResponse
                    {
                        AuthenticationSuccessful = false,
                        OAuthToken = token,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    };
                }
            }
            catch
            {
                return new AuthenticationResponse
                {
                    AuthenticationSuccessful = false,
                    OAuthToken = token,
                    SessionId = Guid.Empty,
                    NewAccount = false
                };
            }
        }

        private static readonly UserAdapter userAdapter = new();
        private bool StoreOrUpdateUserInDb(string fbId, string fbName)
        {
            var user = userAdapter.GetUserByUsername(fbName);

            if (user == null)
            {
                userAdapter.CreateUser(new User
                {
                    UserId = Guid.NewGuid(),
                    Username = fbName,
                    PasswordHash = string.Empty,
                    TwoFASecret = string.Empty
                });
                return true;
            }
            else
            {
                user.Username = fbName;
                userAdapter.UpdateUser(user);
                return false;
            }

        }
    }
}

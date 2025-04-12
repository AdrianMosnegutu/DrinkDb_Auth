using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;

namespace DrinkDb_Auth.OAuthProviders
{
    public class GitHubOAuth2Provider : GenericOAuth2Provider
    {
        private readonly static UserAdapter UserAdapter = new ();
        private readonly static SessionAdapter SessionAdapter = new ();

        public AuthenticationResponse Authenticate(string? userId, string token)
        {
            try
            {
                var (gitHubId, gitHubLogin) = FetchGitHubUserInfo(token);

                if (string.IsNullOrEmpty(gitHubLogin))
                {
                    return new AuthenticationResponse
                    {
                        AuthenticationSuccesfull = false,
                        OAuthenticationToken = string.Empty,
                        SessionId = Guid.Empty,
                        NewAccount = false
                    };
                }

                // Check if a user exists by using the GitHub username.
                if (VerifyUserInDb(gitHubLogin))
                {
                    // User exists, so proceed.
                    User user = UserAdapter.GetUserByUsername(gitHubLogin) ?? throw new Exception("User not found");

                    Session session = SessionAdapter.CreateSession(user.UserId);

                    return new AuthenticationResponse
                    {
                        AuthenticationSuccesfull = true,
                        OAuthenticationToken = token,
                        SessionId = session.sessionId,
                        NewAccount = false
                    };
                }
                else
                {
                    // User does not exist. Insert the new user.
                    Guid newUserId = CreateUserFromGitHub(gitHubLogin);
                    if (newUserId != Guid.Empty)
                    {
                        // Successfully inserted, so login is successful.
                        Session session = SessionAdapter.CreateSession(newUserId);
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
                        // Insertion failed.
                        return new AuthenticationResponse
                        {
                            AuthenticationSuccesfull = false,
                            OAuthenticationToken = token,
                            SessionId = Guid.Empty,
                            NewAccount = false
                        };
                    }
                }
            }
            catch (Exception)
            {
                return new AuthenticationResponse
                {
                    AuthenticationSuccesfull = false,
                    OAuthenticationToken = token,
                    SessionId = Guid.Empty,
                    NewAccount = false
                };
            }
        }

        private (string gitHubId, string gitHubLogin) FetchGitHubUserInfo(string token)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
                client.DefaultRequestHeaders.Add("User-Agent", "DrinkDb_Auth-App");

                var response = client.GetAsync("https://api.github.com/user").Result;
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Failed to fetch user info from GitHub.");
                }

                string userJson = response.Content.ReadAsStringAsync().Result;
                using (JsonDocument userDocument = JsonDocument.Parse(userJson))
                {
                    var root = userDocument.RootElement;
                    string gitHubId = root.GetProperty("id").GetRawText();
                    string? gitHubLogin = root.GetProperty("login").GetString();
                    if (gitHubLogin == null)
                    {
                        throw new Exception("GitHub login is null.");
                    }
                    return (gitHubId, gitHubLogin);
                }
            }
        }

        // TODO delete function since it has 0 references
        public static async Task<string?> GetGitHubUsernameAsync(string token)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
                client.DefaultRequestHeaders.Add("User-Agent", "DrinkDb_Auth-App");
                var response = await client.GetAsync("https://api.github.com/user");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                string userJson = await response.Content.ReadAsStringAsync();
                using (JsonDocument userDocument = JsonDocument.Parse(userJson))
                {
                    var root = userDocument.RootElement;
                    return root.GetProperty("login").GetString();
                }
            }
        }

        private bool VerifyUserInDb(string gitHubLogin)
        {
            try
            {
                User? user = UserAdapter.GetUserByUsername(gitHubLogin);
                if (user != null)
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error verifying user: " + exception.Message);
            }
            return false;
        }

        private Guid CreateUserFromGitHub(string gitHubLogin)
        {
            try
            {
                User newUser = new ()
                {
                    UserId = Guid.NewGuid(),
                    Username = gitHubLogin.Trim(),
                    TwoFASecret = string.Empty,
                    PasswordHash = string.Empty,
                };
                UserAdapter.CreateUser(newUser);
                return newUser.UserId;
            }
            catch (Exception exception)
            {
                Console.WriteLine("Error creating user: " + exception.Message);
            }
            return Guid.Empty;
        }
    }
}

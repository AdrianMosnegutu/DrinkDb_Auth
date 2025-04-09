using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.AuthProviders;
using DrinkDb_Auth.Model;
using DrinkDb_Auth.OAuthProviders;
using Microsoft.UI.Xaml;

namespace DrinkDb_Auth.Service
{
    public enum OAuthService
    {
        Google,
        Facebook,
        Twitter,
        GitHub,
        LinkedIn
    }

    public class AuthenticationService
    {
        private static readonly SessionAdapter SessionAdapter = new ();
        private static readonly UserAdapter UserAdapter = new ();
        private LinkedInLocalOAuthServer linkedinLocalServer;
        private GitHubLocalOAuthServer githubLocalServer;
        private FacebookLocalOAuthServer facebookLocalServer;

        public AuthenticationService()
        {
            githubLocalServer = new GitHubLocalOAuthServer("http://localhost:8890/");
            _ = githubLocalServer.StartAsync();

            facebookLocalServer = new FacebookLocalOAuthServer("http://localhost:8888/");
            _ = facebookLocalServer.StartAsync();

            linkedinLocalServer = new LinkedInLocalOAuthServer("http://localhost:8891/");
            _ = linkedinLocalServer.StartAsync();
        }

        public async Task<AuthResponse> AuthWithOAuth(Window window, OAuthService selectedService)
        {
            var authResponse = selectedService switch
            {
                OAuthService.Google => await AuthenticateWithGoogleAsync(window),
                OAuthService.Facebook => await AuthenticateWithFacebookAsync(),
                OAuthService.Twitter => await AuthenticateWithTwitterAsync(window),
                OAuthService.GitHub => await AuthenticateWithGitHubAsync(),
                OAuthService.LinkedIn => await AuthenticateWithLinkedInAsync(),
                _ => throw new ArgumentException("Invalid OAuth service selected"),
            };

            if (authResponse.AuthSuccessful)
            {
                App.CurrentSessionId = authResponse.SessionId;
                Session session = SessionAdapter.GetSession(App.CurrentSessionId);
                App.CurrentUserId = session.userId;
            }

            return authResponse;
        }

        public void Logout()
        {
            SessionAdapter.EndSession(App.CurrentSessionId);
            App.CurrentSessionId = Guid.Empty;
            App.CurrentUserId = Guid.Empty;
        }

        public User GetUser(Guid sessionId)
        {
            Session session = SessionAdapter.GetSession(sessionId);
            return UserAdapter.GetUserById(session.userId) ?? throw new UserNotFoundException("User not found");
        }

        public static AuthResponse AuthWithUserPass(string username, string password)
        {
            try
            {
                if (BasicAuthenticationProvider.Authenticate(username, password))
                {
                    User user = UserAdapter.GetUserByUsername(username) ?? throw new UserNotFoundException("User not found");
                    Session session = SessionAdapter.CreateSession(user.UserId);
                    return new AuthResponse
                    {
                        AuthSuccessful = true,
                        NewAccount = false,
                        OAuthToken = string.Empty,
                        SessionId = session.sessionId,
                    };
                }
                else
                {
                    return new AuthResponse
                    {
                        AuthSuccessful = false,
                        NewAccount = false,
                        OAuthToken = string.Empty,
                        SessionId = Guid.Empty,
                    };
                }
            }
            catch (UserNotFoundException)
            {
                // create user
                User user = new ()
                {
                    Username = username,
                    PasswordHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password)) ?? throw new Exception("Hashing failed")),
                    UserId = Guid.NewGuid(),
                    TwoFASecret = string.Empty
                };
                UserAdapter.CreateUser(user);
                Session session = SessionAdapter.CreateSession(user.UserId);
                return new AuthResponse
                {
                    AuthSuccessful = true,
                    NewAccount = true,
                    OAuthToken = string.Empty,
                    SessionId = session.sessionId,
                };
            }
            throw new Exception("Unexpected error during authentication");
        }

        private static async Task<AuthResponse> AuthenticateWithGitHubAsync()
        {
            var gitHubHelper = new GitHubOAuthHelper();
            return await gitHubHelper.AuthenticateAsync();
        }

        private static async Task<AuthResponse> AuthenticateWithGoogleAsync(Window window)
        {
            var googleProvider = new GoogleOAuth2Provider();
            return await googleProvider.SignInWithGoogleAsync(window);
        }

        private static async Task<AuthResponse> AuthenticateWithFacebookAsync()
        {
            var faceBookHelper = new FacebookOAuthHelper();
            return await faceBookHelper.AuthenticateAsync();
        }

        private static async Task<AuthResponse> AuthenticateWithTwitterAsync(Window window)
        {
            var twitterProvider = new TwitterOAuth2Provider();
            return await twitterProvider.SignInWithTwitterAsync(window);
        }

        private static async Task<AuthResponse> AuthenticateWithLinkedInAsync()
        {
            var linkedInHelper = new LinkedInOAuthHelper(
                clientId: "86j0ikb93jm78x",
                clientSecret: "WPL_AP1.pg2Bd1XhCi821VTG.+hatTA==",
                redirectUri: "http://localhost:8891/auth",
                scope: "openid profile email");
            return await linkedInHelper.AuthenticateAsync();
        }
    }
}

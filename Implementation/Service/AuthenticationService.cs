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
        private static ISessionAdapter sessionAdapter;
        private static IUserAdapter userAdapter;
        private ILinkedInLocalOAuthServer linkedinLocalServer;
        private IGitHubLocalOAuthServer githubLocalServer;
        private IFacebookLocalOAuthServer facebookLocalServer;

        public AuthenticationService()
        {
            githubLocalServer = new GitHubLocalOAuthServer("http://localhost:8890/");
            _ = githubLocalServer.StartAsync();

            facebookLocalServer = new FacebookLocalOAuthServer("http://localhost:8888/");
            _ = facebookLocalServer.StartAsync();

            linkedinLocalServer = new LinkedInLocalOAuthServer("http://localhost:8891/");
            _ = linkedinLocalServer.StartAsync();

            sessionAdapter = new SessionAdapter();

            userAdapter = new UserAdapter();
        }

        public AuthenticationService(ILinkedInLocalOAuthServer linkedinLocalServer, IGitHubLocalOAuthServer githubLocalServer, IFacebookLocalOAuthServer facebookLocalServer, IUserAdapter userAdapter, ISessionAdapter sessionAdapter)
        {
            this.linkedinLocalServer = linkedinLocalServer;
            this.githubLocalServer = githubLocalServer;
            this.facebookLocalServer = facebookLocalServer;
            AuthenticationService.userAdapter = userAdapter;
            AuthenticationService.sessionAdapter = sessionAdapter;
            _ = githubLocalServer.StartAsync();
            _ = facebookLocalServer.StartAsync();
            _ = linkedinLocalServer.StartAsync();
        }

        public async Task<AuthResponse> AuthWithOAuth(Window window, OAuthService selectedService, object authProvider)
        {
            var authResponse = selectedService switch
            {
                OAuthService.Google => await AuthenticateWithGoogleAsync(window, authProvider as IGoogleOAuth2Provider),
                OAuthService.Facebook => await AuthenticateWithFacebookAsync(authProvider as IFacebookOAuthHelper),
                OAuthService.Twitter => await AuthenticateWithTwitterAsync(window, authProvider as ITwitterOAuth2Provider),
                OAuthService.GitHub => await AuthenticateWithGitHubAsync(authProvider as IGitHubOAuthHelper),
                OAuthService.LinkedIn => await AuthenticateWithLinkedInAsync(authProvider as ILinkedInOAuthHelper),
                _ => throw new ArgumentException("Invalid OAuth service selected"),
            };

            if (authResponse.AuthSuccessful)
            {
                App.CurrentSessionId = authResponse.SessionId;
                Session session = sessionAdapter.GetSession(App.CurrentSessionId);
                App.CurrentUserId = session.userId;
            }

            return authResponse;
        }

        public void Logout()
        {
            sessionAdapter.EndSession(App.CurrentSessionId);
            App.CurrentSessionId = Guid.Empty;
            App.CurrentUserId = Guid.Empty;
        }

        public User GetUser(Guid sessionId)
        {
            Session session = sessionAdapter.GetSession(sessionId);
            return userAdapter.GetUserById(session.userId) ?? throw new UserNotFoundException("User not found");
        }

        public static AuthResponse AuthWithUserPass(string username, string password)
        {
            try
            {
                if (BasicAuthenticationProvider.Authenticate(username, password))
                {
                    User user = userAdapter.GetUserByUsername(username) ?? throw new UserNotFoundException("User not found");
                    Session session = sessionAdapter.CreateSession(user.UserId);
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
                userAdapter.CreateUser(user);
                Session session = sessionAdapter.CreateSession(user.UserId);
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

        private static async Task<AuthResponse> AuthenticateWithGitHubAsync(IGitHubOAuthHelper gitHubHelper)
        {
            return await gitHubHelper.AuthenticateAsync();
        }

        private static async Task<AuthResponse> AuthenticateWithGoogleAsync(Window window, IGoogleOAuth2Provider googleProvider)
        {
            return await googleProvider.SignInWithGoogleAsync(window);
        }

        private static async Task<AuthResponse> AuthenticateWithFacebookAsync(IFacebookOAuthHelper faceBookHelper)
        {
            return await faceBookHelper.AuthenticateAsync();
        }

        private static async Task<AuthResponse> AuthenticateWithTwitterAsync(Window window, ITwitterOAuth2Provider twitterProvider)
        {
            return await twitterProvider.SignInWithTwitterAsync(window);
        }

        private static async Task<AuthResponse> AuthenticateWithLinkedInAsync(ILinkedInOAuthHelper linkedInHelper)
        {
            return await linkedInHelper.AuthenticateAsync();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
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
        private static readonly SessionAdapter _sessionAdapter = new();
        private static readonly UserAdapter _userAdapter = new();
        private LinkedInLocalOAuthServer _linkedinLocalServer;
        private GitHubLocalOAuthServer _githubLocalServer;
        private FacebookLocalOAuthServer _facebookLocalServer;

        public AuthenticationService()
        {
            _githubLocalServer = new GitHubLocalOAuthServer("http://localhost:8890/");
            _ = _githubLocalServer.StartAsync();

            _facebookLocalServer = new FacebookLocalOAuthServer("http://localhost:8888/");
            _ = _facebookLocalServer.StartAsync();

            _linkedinLocalServer = new LinkedInLocalOAuthServer("http://localhost:8891/");
            _ = _linkedinLocalServer.StartAsync();
        }

        public async Task<AuthResponse> AuthWithOAuth(Window windowRef, OAuthService selectedService)
        {
            var authResponse = selectedService switch
            {
                OAuthService.Google => await AuthenticateWithGoogleAsync(windowRef),
                OAuthService.Facebook => await AuthenticateWithFacebookAsync(),
                OAuthService.Twitter => await AuthenticateWithTwitterAsync(windowRef),
                OAuthService.GitHub => await AuthenticateWithGitHubAsync(),
                OAuthService.LinkedIn => await AuthenticateWithLinkedInAsync(),
                _ => throw new ArgumentException("Invalid OAuth service selected"),
            };

            if (authResponse.AuthSuccessful)
            {
                App.CurrentSessionId = authResponse.SessionId;
                Session session = _sessionAdapter.GetSession(App.CurrentSessionId);
                App.CurrentUserId = session.userId;
            }

            return authResponse;
        }
        public void Logout()
        {
            _sessionAdapter.EndSession(App.CurrentSessionId);
            App.CurrentSessionId = Guid.Empty;
            App.CurrentUserId = Guid.Empty;
        }
        public User GetUser(Guid sessionId)
        {
            Session sess = _sessionAdapter.GetSession(sessionId);
            return _userAdapter.GetUserById(sess.userId) ?? throw new UserNotFoundException("User not found");
        }
        public static AuthResponse AuthWithUserPass(string username, string password)
        {
            try
            {
                if (BasicAuthenticationProvider.Authenticate(username, password))
                {
                    User user = _userAdapter.GetUserByUsername(username) ?? throw new UserNotFoundException("User not found");
                    Session sess = _sessionAdapter.CreateSession(user.UserId);
                    return new AuthResponse
                    {
                        AuthSuccessful = true,
                        NewAccount = false,
                        OAuthToken = string.Empty,
                        SessionId = sess.sessionId,
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
                //create user
                User user = new()
                {
                    Username = username,
                    PasswordHash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password)).ToString() ?? throw new Exception("Hashing failed"),
                    UserId = Guid.NewGuid()
                };
                _userAdapter.CreateUser(user);
                Session sess = _sessionAdapter.CreateSession(user.UserId);
                return new AuthResponse
                {
                    AuthSuccessful = true,
                    NewAccount = true,
                    OAuthToken = string.Empty,
                    SessionId = sess.sessionId,
                };
            }
            throw new Exception("Unexpected error during authentication");

        }

        private static async Task<AuthResponse> AuthenticateWithGitHubAsync()
        {
            var ghHelper = new GitHubOAuthHelper();
            return await ghHelper.AuthenticateAsync();
        }

        private static async Task<AuthResponse> AuthenticateWithGoogleAsync(Window window)
        {
            var googleProvider = new GoogleOAuth2Provider();
            return await googleProvider.SignInWithGoogleAsync(window);
        }

        private static async Task<AuthResponse> AuthenticateWithFacebookAsync()
        {
            var fbHelper = new FacebookOAuthHelper();
            return await fbHelper.AuthenticateAsync();
        }

        private static async Task<AuthResponse> AuthenticateWithTwitterAsync(Window window)
        {
            var twitterProvider = new TwitterOAuth2Provider();
            return await twitterProvider.SignInWithTwitterAsync(window);
        }
        private static async Task<AuthResponse> AuthenticateWithLinkedInAsync()
        {
            var lnHelper = new LinkedInOAuthHelper(
                clientId: "86j0ikb93jm78x",
                clientSecret: "WPL_AP1.pg2Bd1XhCi821VTG.+hatTA==",
                redirectUri: "http://localhost:8891/auth",
                scope: "openid profile email"
            );
            return await lnHelper.AuthenticateAsync();
        }
    }
}

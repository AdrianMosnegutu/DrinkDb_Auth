using DrinkDb_Auth.Adapter;
using DrinkDb_Auth.Model;
using DrinkDb_Auth.OAuthProviders;
using Microsoft.UI.Xaml;

namespace Tests
{
    public class MockSessionAdapter : ISessionAdapter
    {
        public Session CreateSession(Guid userId)
        {
            return Session.createSessionWithIds(userId, userId);
        }

        public bool EndSession(Guid sessionId)
        {
            throw new NotImplementedException();
        }

        public Session GetSession(Guid sessionId)
        {
            throw new NotImplementedException();
        }
    }

    public class MockGoogleAuthProvider : IGoogleOAuth2Provider
    {
        public Guid MockId { get; set; }
        public static Guid SubToGuid(string sub)
        {
            throw new NotImplementedException();
        }

        public AuthResponse Authenticate(string userId, string token)
        {
            throw new NotImplementedException();
        }

        public Task<AuthResponse> ExchangeCodeForTokenAsync(string code)
        {
            throw new NotImplementedException();
        }

        public string GetAuthorizationUrl()
        {
            throw new NotImplementedException();
        }

        public Task<AuthResponse> SignInWithGoogleAsync(Window parentWindow)
        {
            AuthResponse mockResponse = new AuthResponse
            {
                AuthSuccessful = false,
                NewAccount = false,
                OAuthToken = string.Empty,
                SessionId = MockId,
            };

            return Task.FromResult(mockResponse);
        }
    }

    public class MockUserAdapter : IUserAdapter
    {
        public bool CreateUser(User user)
        {
            throw new NotImplementedException();
        }

        public User? GetUserById(Guid userId)
        {
            throw new NotImplementedException();
        }

        public User? GetUserByUsername(string username)
        {
            throw new NotImplementedException();
        }

        public bool ValidateAction(Guid userId, string resource, string action)
        {
            throw new NotImplementedException();
        }
    }

    public class MockLinkedInServer : ILinkedInLocalOAuthServer
    {
        public static event Action<string>? OnCodeReceived;

        public Task StartAsync()
        {
            return Task.CompletedTask;
        }

        public void Stop()
        {
            /* does nothing */
        }
    }

    public class MockGitHubServer : IGitHubLocalOAuthServer
    {
        public static event Action<string>? OnCodeReceived;

        public Task StartAsync()
        {
            return Task.CompletedTask;
        }

        public void Stop()
        {
            /* does nothing */
        }
    }

    public class MockFacebookServer : IFacebookLocalOAuthServer
    {
        public static event Action<string>? OnTokenReceived;

        public Task StartAsync()
        {
            return Task.CompletedTask;
        }

        public void Stop()
        {
            /* does nothing */
        }
    }
}

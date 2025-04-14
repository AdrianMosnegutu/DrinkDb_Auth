using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace DrinkDb_Auth.OAuthProviders
{
    public interface IGoogleOAuth2Provider
    {
        static abstract Guid SubToGuid(string sub);
        AuthResponse Authenticate(string userId, string token);
        Task<AuthResponse> ExchangeCodeForTokenAsync(string code);
        string GetAuthorizationUrl();
        Task<AuthResponse> SignInWithGoogleAsync(Window parentWindow);
    }
}
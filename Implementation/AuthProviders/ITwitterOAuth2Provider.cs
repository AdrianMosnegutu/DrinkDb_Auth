using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace DrinkDb_Auth.OAuthProviders
{
    public interface ITwitterOAuth2Provider
    {
        static abstract Guid SubToGuid(string sub);
        Task<AuthResponse> ExchangeCodeForTokenAsync(string code);
        string GetAuthorizationUrl();
        Task<AuthResponse> SignInWithTwitterAsync(Window parentWindow);
    }
}
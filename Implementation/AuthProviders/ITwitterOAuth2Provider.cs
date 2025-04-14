using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace DrinkDb_Auth.OAuthProviders
{
    public interface ITwitterOAuth2Provider
    {
        Task<AuthenticationResponse> ExchangeCodeForTokenAsync(string code);
        string GetAuthorizationUrl();
        Task<AuthenticationResponse> SignInWithTwitterAsync(Window parentWindow);
    }
}
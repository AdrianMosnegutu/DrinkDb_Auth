using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace DrinkDb_Auth.OAuthProviders
{
    public interface IGoogleOAuth2Provider
    {
        AuthenticationResponse Authenticate(string userId, string token);
        Task<AuthenticationResponse> ExchangeCodeForTokenAsync(string code);
        string GetAuthorizationUrl();
        Task<AuthenticationResponse> SignInWithGoogleAsync(Window parentWindow);
    }
}
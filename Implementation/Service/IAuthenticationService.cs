using System;
using System.Threading.Tasks;
using DrinkDb_Auth.Model;
using DrinkDb_Auth.OAuthProviders;
using Microsoft.UI.Xaml;

namespace DrinkDb_Auth.Service
{
    public interface IAuthenticationService
    {
        Task<AuthenticationResponse> AuthWithOAuth(Window window, OAuthService selectedService);
        User GetUser(Guid sessionId);
        void Logout();
    }
} 
using DrinkDb_Auth.OAuthProviders;
using System.Threading.Tasks;

namespace DrinkDb_Auth.AuthProviders.Facebook
{
    public interface IFacebookOAuthHelper
    {
        Task<AuthenticationResponse> AuthenticateAsync();
    }
}
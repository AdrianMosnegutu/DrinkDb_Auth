using System.Threading.Tasks;

namespace DrinkDb_Auth.OAuthProviders
{
    public interface IFacebookOAuthHelper
    {
        Task<AuthResponse> AuthenticateAsync();
    }
}
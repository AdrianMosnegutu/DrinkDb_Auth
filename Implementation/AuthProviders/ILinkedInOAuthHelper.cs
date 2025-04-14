using System.Threading.Tasks;

namespace DrinkDb_Auth.OAuthProviders
{
    public interface ILinkedInOAuthHelper
    {
        Task<AuthResponse> AuthenticateAsync();
    }
}
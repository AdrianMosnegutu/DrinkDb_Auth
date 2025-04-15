using System.Threading.Tasks;

namespace DrinkDb_Auth.OAuthProviders
{
    public interface ILinkedInOAuthHelper
    {
        Task<AuthenticationResponse> AuthenticateAsync();
    }
}
using System;
using System.Threading.Tasks;

namespace DrinkDb_Auth.OAuthProviders
{
    public interface IGitHubLocalOAuthServer
    {
        static abstract event Action<string>? OnCodeReceived;

        Task StartAsync();
        void Stop();
    }
}
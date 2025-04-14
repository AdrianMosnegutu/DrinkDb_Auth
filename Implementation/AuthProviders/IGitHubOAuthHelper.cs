﻿using System.Threading.Tasks;

namespace DrinkDb_Auth.OAuthProviders
{
    public interface IGitHubOAuthHelper
    {
        Task<AuthenticationResponse> AuthenticateAsync();
    }
}
namespace DrinkDb_Auth.OAuthProviders
{
    /// <summary>
    /// A generic OAuth2 contract that various providers (Facebook, Google, etc.) can implement.
    /// </summary>
    public interface GenericOAuth2Provider
    {
        /// <summary>
        /// Authenticates a user using the provided token.
        /// Returns an AuthResponse indicating success or failure.
        /// </summary>
        /// <param name="userId">The user identifier (if applicable to your flow).</param>
        /// <param name="token">The OAuth2 token (e.g. an access token) to validate or use.</param>
        /// <returns>An AuthResponse with the authentication result.</returns>
        AuthResponse Authenticate(string userId, string token);

        /// <summary>
        /// Returns an authorization URL for the user to visit (3rd party sign-in page).
        /// </summary>
        /// <param name="userId">Optional user ID if needed in the URL.</param>
        /// <returns>A full URL to the OAuth2 provider's authorization endpoint.</returns>
        string GetAuthorizationURL(string userId);

        /// <summary>
        /// Exchanges an authorization code for a token (e.g. final access token).
        /// </summary>
        /// <param name="authCode">The auth code from the OAuth2 callback.</param>
        /// <returns>A string containing the new token (or you could return another AuthResponse).</returns>
        string ExchangeAuthCodeForToken(string authCode);
    }
}

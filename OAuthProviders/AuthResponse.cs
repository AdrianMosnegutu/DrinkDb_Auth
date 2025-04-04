namespace DrinkDb_Auth.OAuthProviders
{
  
    /// Represents the result of an OAuth2 authentication attempt.
    public class AuthResponse
    {
        /// True if authentication succeeded, false otherwise.
        public required bool AuthSuccessful { get; set; }

        /// The session token (e.g., access token) returned by the OAuth2 provider.
        public required string SessionToken { get; set; }

        /// Indicates whether this authentication created a brand new account.
        public required bool NewAccount { get; set; }
    }
}

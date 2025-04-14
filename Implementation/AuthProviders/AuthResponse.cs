using System;

namespace DrinkDb_Auth.OAuthProviders
{
    /// Represents the result of an OAuth2 authentication attempt.
    public class AuthResponse
    {
        /// True if authentication succeeded, false otherwise.
        public required bool AuthSuccessful { get; set; }

        /// The session id
        public required Guid SessionId { get; set; }

        /// The session token from the OAuth provider.

        public required string? OAuthToken { get; set; }

        /// Indicates whether this authentication created a brand new account.
        public required bool NewAccount { get; set; }

        public override bool Equals(object other)
        {
            var otherResponse = other as AuthResponse;
            if (otherResponse == null)
            {
                return false;
            }
            return AuthSuccessful == otherResponse.AuthSuccessful && SessionId == otherResponse.SessionId && OAuthToken == otherResponse.OAuthToken && NewAccount == otherResponse.NewAccount;
        }
    }
}

using System;

namespace DrinkDb_Auth.Model
{
    public class User
    {
        public required Guid UserId { get; set; }
        public required string Username { get; set; }
        public string? PasswordHash { get; set; }
        public string? TwoFASecret { get; set; }
    }
}

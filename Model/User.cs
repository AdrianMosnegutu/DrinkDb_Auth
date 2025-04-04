using System;

namespace DrinkDb_Auth.Model
{
    public class User
    {
        public Guid UserId { get; set; }
        public required string Username { get; set; }
        public required string PasswordHash { get; set; }
        public required string TwoFASecret { get; set; }
    }
}

using System;

namespace DrinkDb_Auth.Model
{
    public class User
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string TwoFASecret { get; set; }
        public Guid RoleId { get; set; }
    }
}

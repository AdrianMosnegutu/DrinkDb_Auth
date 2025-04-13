using System;
using DrinkDb_Auth.Service;

namespace DrinkDb_Auth.Model
{
    public class User
    {
<<<<<<< Updated upstream
        private static readonly UserService UserService = new ();
=======
        private static readonly UserService _userService = new ();
>>>>>>> Stashed changes

        public required Guid UserId { get; set; }
        public required string Username { get; set; }
        public required string PasswordHash { get; set; }
        public required string? TwoFASecret { get; set; }
        public bool ValidateAction(string resource, string action)
        {
            if (string.IsNullOrEmpty(resource))
            {
                throw new ArgumentException("Role cannot be null or empty.", nameof(resource));
            }
            if (string.IsNullOrEmpty(action))
            {
                throw new ArgumentException("Action cannot be null or empty.", nameof(action));
            }
<<<<<<< Updated upstream
            return UserService.ValidateAction(UserId, resource, action);
=======
            return _userService.IsUserAuthorized(UserId, resource, action);
>>>>>>> Stashed changes
        }
    }
}

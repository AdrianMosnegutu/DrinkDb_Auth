using System;
using DrinkDb_Auth.Adapter;

namespace DrinkDb_Auth.Model
{
    public class Users
    {
        private static readonly UserAdapter _userAdapter = new();

        public required Guid UserId { get; set; }
        public required string Username { get; set; }
        public required string PasswordHash { get; set; }
        public string? TwoFASecret { get; set; }
        public Guid RoleId { get; set; }

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
            return _userAdapter.ValidateActionForUser(UserId, resource, action);
        }
    }
}
